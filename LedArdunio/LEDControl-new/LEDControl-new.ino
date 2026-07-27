#include <FastLED.h>
#include <ctype.h>
#include <errno.h>
#include <limits.h>

#if defined(CONFIG_IDF_TARGET_ESP32S3) || defined(ARDUINO_ESP32S3_DEV) || defined(ARDUINO_ESP32S3_BOX) || defined(ARDUINO_ESP32S3_BOX_LITE)
#include "esp32-hal-rgb-led.h"
#endif

// ====== PTL 通用配置 ======
#define TOTAL_LOGICAL_STRIPS 25
#define PHYSICAL_LEDS_PER_STRIP 75
#define SERIAL_BAUD 115200

// 当前上位机逐条发送 SET 且不追加 SHOW，因此必须自动刷新。
const bool AUTO_SHOW_ON_SET = true;

// ====== 按编译目标自动选板 ======
// Arduino IDE 选 ESP32S3 Dev Module 时走 S3 配置；选 ESP32 Dev Module 时走 V1 配置。
#if defined(CONFIG_IDF_TARGET_ESP32S3) || defined(ARDUINO_ESP32S3_DEV) || defined(ARDUINO_ESP32S3_BOX) || defined(ARDUINO_ESP32S3_BOX_LITE)

#define BOARD_NAME "ESP32-S3"
// S3 硬件 RMT 发送通道仅 4 个；FastLED rmt_5 每路 addLeds 占 1 通道。
#define PHYSICAL_OUTPUT_STRIPS 4
#define HAS_STATUS_LED 1
#define STATUS_LED_PIN 48

#define STRIP_PIN_1 4
#define STRIP_PIN_2 5
#define STRIP_PIN_3 6
#define STRIP_PIN_4 7

const uint8_t LOGICAL_TO_PHYSICAL[TOTAL_LOGICAL_STRIPS] = {
  0, 1, 2, 3, 0, 1, 2, 3, 0, 1,
  2, 3, 0, 1, 2, 3, 0, 1, 2, 3,
  0, 1, 2, 3, 0
};

#else

#define BOARD_NAME "ESP32 DevKit V1"
#define PHYSICAL_OUTPUT_STRIPS 8
#define HAS_STATUS_LED 0

// V1 + FastLED(RMT) 稳定上限约 8 路；其余逻辑路镜像映射
#define STRIP_PIN_1 16
#define STRIP_PIN_2 17
#define STRIP_PIN_3 18
#define STRIP_PIN_4 19
#define STRIP_PIN_5 21
#define STRIP_PIN_6 22
#define STRIP_PIN_7 23
#define STRIP_PIN_8 25

const uint8_t LOGICAL_TO_PHYSICAL[TOTAL_LOGICAL_STRIPS] = {
  0, 1, 2, 3, 4, 5, 6, 7, 0, 1,
  2, 3, 4, 5, 6, 7, 0, 1, 2, 3,
  4, 5, 6, 7, 0
};

#endif

static_assert(PHYSICAL_OUTPUT_STRIPS <= 25, "PHYSICAL_OUTPUT_STRIPS overflow");
static_assert(TOTAL_LOGICAL_STRIPS == PHYSICAL_OUTPUT_STRIPS || PHYSICAL_OUTPUT_STRIPS < TOTAL_LOGICAL_STRIPS,
              "logical/physical mapping config invalid");

const uint16_t MAX_BUFFER_SIZE = 96;
const uint8_t MAX_PARSE_TOKENS = 8;

CRGB leds[PHYSICAL_OUTPUT_STRIPS][PHYSICAL_LEDS_PER_STRIP];
char inputBuffer[MAX_BUFFER_SIZE];
uint16_t bufferIndex = 0;
bool inFrame = false;
bool frameOverflow = false;

#if HAS_STATUS_LED
CRGB statusLed;
const CRGB STATUS_READY = CRGB(0, 48, 0);
const CRGB STATUS_SET = CRGB(0, 0, 64);
const CRGB STATUS_PING = CRGB(0, 64, 0);
const CRGB STATUS_ERROR = CRGB(64, 0, 0);
uint32_t statusRevertAt = 0;
bool statusPendingShow = false;

// IO48 板载 WS2818：用 Arduino-ESP32 自带 rgbLedWrite，不占 FastLED RMT 通道。
static void statusPixelWrite(const CRGB &color) {
  rgbLedWrite(STATUS_LED_PIN, color.r, color.g, color.b);
}

void statusApply(const CRGB &color, bool refreshNow) {
  statusLed = color;
  if (refreshNow) {
    statusPixelWrite(color);
  }
}

void statusSetReady(bool refreshNow = true) {
  statusRevertAt = 0;
  statusPendingShow = false;
  statusApply(STATUS_READY, refreshNow);
}

void statusFlash(const CRGB &color, uint16_t durationMs, bool refreshNow) {
  statusRevertAt = millis() + durationMs;
  statusApply(color, refreshNow);
}

void statusTick() {
  if (statusRevertAt != 0 && millis() >= statusRevertAt) {
    statusSetReady(true);
  }
}
#endif

bool parseLongSafe(const char *s, long &outValue) {
  if (s == nullptr) {
    return false;
  }

  while (isspace(static_cast<unsigned char>(*s))) {
    s++;
  }
  if (*s == '\0') {
    return false;
  }

  char *endPtr = nullptr;
  errno = 0;
  long v = strtol(s, &endPtr, 10);
  if (errno != 0 || endPtr == s) {
    return false;
  }

  while (*endPtr != '\0') {
    if (!isspace(static_cast<unsigned char>(*endPtr))) {
      return false;
    }
    endPtr++;
  }

  outValue = v;
  return true;
}

bool parseUint8Safe(const char *s, uint8_t &outValue) {
  long v = 0;
  if (!parseLongSafe(s, v) || v < 0 || v > 255) {
    return false;
  }
  outValue = static_cast<uint8_t>(v);
  return true;
}

bool parseSetPayload(char *payload, uint8_t &logicalStrip, uint8_t &index, uint8_t &r, uint8_t &g, uint8_t &b) {
  char *tokens[MAX_PARSE_TOKENS];
  uint8_t tokenCount = 0;

  char *token = strtok(payload, ",");
  while (token != nullptr && tokenCount < MAX_PARSE_TOKENS) {
    tokens[tokenCount++] = token;
    token = strtok(nullptr, ",");
  }

  uint8_t base = 0;
  if (tokenCount == 6 && strcmp(tokens[0], "SET") == 0) {
    base = 1;
  } else if (tokenCount == 5) {
    base = 0;
  } else {
    return false;
  }

  long stripLong = 0;
  long indexLong = 0;
  if (!parseLongSafe(tokens[base + 0], stripLong) || !parseLongSafe(tokens[base + 1], indexLong)) {
    return false;
  }
  if (stripLong < 1 || stripLong > TOTAL_LOGICAL_STRIPS) {
    return false;
  }
  if (indexLong < 0 || indexLong >= PHYSICAL_LEDS_PER_STRIP) {
    return false;
  }

  if (!parseUint8Safe(tokens[base + 2], r) ||
      !parseUint8Safe(tokens[base + 3], g) ||
      !parseUint8Safe(tokens[base + 4], b)) {
    return false;
  }

  logicalStrip = static_cast<uint8_t>(stripLong);
  index = static_cast<uint8_t>(indexLong);
  return true;
}

void reportError(const __FlashStringHelper *msg) {
  Serial.println(msg);
#if HAS_STATUS_LED
  statusFlash(STATUS_ERROR, 300, true);
#endif
}

void handleCommand(char *payload) {
  if (payload == nullptr || payload[0] == '\0') {
    reportError(F("ERR:EMPTY"));
    return;
  }

  if (strcmp(payload, "OFF") == 0 || strcmp(payload, "CLEAR") == 0) {
    FastLED.clear();
    FastLED.show();
#if HAS_STATUS_LED
    statusSetReady();
#endif
    Serial.println(F("OK:CLEAR"));
    return;
  }

  if (strcmp(payload, "SHOW") == 0) {
#if HAS_STATUS_LED
    if (statusPendingShow) {
      statusPendingShow = false;
      statusFlash(STATUS_SET, 120, false);
      statusPixelWrite(STATUS_SET);
    }
#endif
    FastLED.show();
    Serial.println(F("OK:SHOW"));
    return;
  }

  if (strcmp(payload, "PING") == 0) {
    Serial.println(F("PONG"));
#if HAS_STATUS_LED
    statusFlash(STATUS_PING, 150, true);
#endif
    return;
  }

  uint8_t logicalStrip = 0;
  uint8_t index = 0;
  uint8_t r = 0;
  uint8_t g = 0;
  uint8_t b = 0;
  if (parseSetPayload(payload, logicalStrip, index, r, g, b)) {
    const uint8_t physicalIndex = LOGICAL_TO_PHYSICAL[logicalStrip - 1];
    if (physicalIndex >= PHYSICAL_OUTPUT_STRIPS) {
      reportError(F("ERR:MAP"));
      return;
    }

    leds[physicalIndex][index] = CRGB(r, g, b);
    if (AUTO_SHOW_ON_SET) {
      FastLED.show();
    }
    Serial.println(F("OK:SET"));
#if HAS_STATUS_LED
    statusPendingShow = true;
#endif
    return;
  }

  reportError(F("ERR:CMD"));
}

void initLeds() {
#if PHYSICAL_OUTPUT_STRIPS >= 1
  FastLED.addLeds<WS2812B, STRIP_PIN_1, GRB>(leds[0], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 2
  FastLED.addLeds<WS2812B, STRIP_PIN_2, GRB>(leds[1], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 3
  FastLED.addLeds<WS2812B, STRIP_PIN_3, GRB>(leds[2], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 4
  FastLED.addLeds<WS2812B, STRIP_PIN_4, GRB>(leds[3], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 5
  FastLED.addLeds<WS2812B, STRIP_PIN_5, GRB>(leds[4], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 6
  FastLED.addLeds<WS2812B, STRIP_PIN_6, GRB>(leds[5], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 7
  FastLED.addLeds<WS2812B, STRIP_PIN_7, GRB>(leds[6], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 8
  FastLED.addLeds<WS2812B, STRIP_PIN_8, GRB>(leds[7], PHYSICAL_LEDS_PER_STRIP);
#endif

#if PHYSICAL_OUTPUT_STRIPS >= 9
  FastLED.addLeds<WS2812B, STRIP_PIN_9, GRB>(leds[8], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 10
  FastLED.addLeds<WS2812B, STRIP_PIN_10, GRB>(leds[9], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 11
  FastLED.addLeds<WS2812B, STRIP_PIN_11, GRB>(leds[10], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 12
  FastLED.addLeds<WS2812B, STRIP_PIN_12, GRB>(leds[11], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 13
  FastLED.addLeds<WS2812B, STRIP_PIN_13, GRB>(leds[12], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 14
  FastLED.addLeds<WS2812B, STRIP_PIN_14, GRB>(leds[13], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 15
  FastLED.addLeds<WS2812B, STRIP_PIN_15, GRB>(leds[14], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 16
  FastLED.addLeds<WS2812B, STRIP_PIN_16, GRB>(leds[15], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 17
  FastLED.addLeds<WS2812B, STRIP_PIN_17, GRB>(leds[16], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 18
  FastLED.addLeds<WS2812B, STRIP_PIN_18, GRB>(leds[17], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 19
  FastLED.addLeds<WS2812B, STRIP_PIN_19, GRB>(leds[18], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 20
  FastLED.addLeds<WS2812B, STRIP_PIN_20, GRB>(leds[19], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 21
  FastLED.addLeds<WS2812B, STRIP_PIN_21, GRB>(leds[20], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 22
  FastLED.addLeds<WS2812B, STRIP_PIN_22, GRB>(leds[21], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 23
  FastLED.addLeds<WS2812B, STRIP_PIN_23, GRB>(leds[22], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 24
  FastLED.addLeds<WS2812B, STRIP_PIN_24, GRB>(leds[23], PHYSICAL_LEDS_PER_STRIP);
#endif
#if PHYSICAL_OUTPUT_STRIPS >= 25
  FastLED.addLeds<WS2812B, STRIP_PIN_25, GRB>(leds[24], PHYSICAL_LEDS_PER_STRIP);
#endif
}

void setup() {
  Serial.begin(SERIAL_BAUD);
  Serial.setTimeout(5);

  FastLED.setMaxPowerInVoltsAndMilliamps(5, 3000);
  initLeds();
  FastLED.clear(true);
#if HAS_STATUS_LED
  statusSetReady();
#endif

  Serial.print(F("PTL "));
  Serial.print(F(BOARD_NAME));
  Serial.println(F(" Ready."));
#if PHYSICAL_OUTPUT_STRIPS == TOTAL_LOGICAL_STRIPS
  Serial.println(F("Mode: 25 logical strips -> 25 physical outputs."));
#else
  Serial.print(F("Mode: 25 logical -> "));
  Serial.print(PHYSICAL_OUTPUT_STRIPS);
  Serial.println(F(" physical outputs (mirrored map)."));
#if defined(CONFIG_IDF_TARGET_ESP32S3) || defined(ARDUINO_ESP32S3_DEV)
  Serial.println(F("Note: S3 FastLED RMT max 4 channels; IO48 status via rgbLedWrite."));
#endif
#endif
  Serial.println(F("Cmd: <SET,s,i,r,g,b> <SHOW> <OFF> <PING>"));
}

void loop() {
#if HAS_STATUS_LED
  statusTick();
#endif

  while (Serial.available() > 0) {
    const char c = static_cast<char>(Serial.read());

    if (c == '<') {
      inFrame = true;
      frameOverflow = false;
      bufferIndex = 0;
      continue;
    }

    if (!inFrame) {
      continue;
    }

    if (c == '>') {
      inFrame = false;
      if (frameOverflow) {
        reportError(F("ERR:OVF"));
        continue;
      }

      inputBuffer[bufferIndex] = '\0';
      handleCommand(inputBuffer);
      continue;
    }

    if (bufferIndex < MAX_BUFFER_SIZE - 1) {
      inputBuffer[bufferIndex++] = c;
    } else {
      frameOverflow = true;
    }
  }
}
