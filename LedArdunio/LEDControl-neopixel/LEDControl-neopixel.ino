// PTL 固件 — Adafruit_NeoPixel 版（ESP32-S3 推荐）
//
// 与 FastLED 的区别：
// FastLED rmt_5 在 setup 时为每条灯带永久占用 1 个 RMT 通道（S3 仅 4 个 → 崩溃）。
// Adafruit_NeoPixel（IDF 5）在 show() 时复用 1 个 RMT 通道，按引脚轮流发送 → 可驱动 25 路。
//
// Arduino IDE：开发板选 ESP32S3 Dev Module，安装库 Adafruit NeoPixel
// 打开本文件夹 LEDControl-neopixel 作为独立工程编译上传（勿与 LEDControl-new 同目录混编）

#include <Adafruit_NeoPixel.h>
#include <ctype.h>
#include <errno.h>

#if defined(CONFIG_IDF_TARGET_ESP32S3) || defined(ARDUINO_ESP32S3_DEV)
#include "esp32-hal-rgb-led.h"
#define HAS_STATUS_LED 1
#define STATUS_LED_PIN 48
#else
#define HAS_STATUS_LED 0
#endif

#define NUM_STRIPS 25
#define LEDS_PER_STRIP 75
#define SERIAL_BAUD 115200

// S3：25 路独立 GPIO（避开 0/43/44/19/20/48）
const uint8_t DATA_PINS[NUM_STRIPS] = {
  4, 5, 6, 7, 8, 9, 10, 11, 12, 13,
  14, 15, 16, 17, 18, 21, 35, 36, 37, 38,
  39, 40, 41, 42, 47
};

const uint16_t MAX_BUFFER_SIZE = 96;
const uint8_t MAX_PARSE_TOKENS = 8;
const bool AUTO_SHOW_ON_SET = true;
const uint8_t DEFAULT_BRIGHTNESS = 200;

Adafruit_NeoPixel *strips[NUM_STRIPS] = {nullptr};
bool stripDirty[NUM_STRIPS] = {false};

char inputBuffer[MAX_BUFFER_SIZE];
uint16_t bufferIndex = 0;
bool inFrame = false;
bool frameOverflow = false;

#if HAS_STATUS_LED
const uint16_t STATUS_FLASH_MS = 500;
const uint32_t STATUS_OFF = Adafruit_NeoPixel::Color(0, 0, 0);
const uint32_t STATUS_CMD = Adafruit_NeoPixel::Color(0, 0, 64);   // 蓝：SET/SHOW/CLEAR
const uint32_t STATUS_PING = Adafruit_NeoPixel::Color(0, 64, 0);    // 绿：PING
const uint32_t STATUS_ERROR = Adafruit_NeoPixel::Color(64, 0, 0);   // 红：错误
uint32_t statusRevertAt = 0;

void statusPixelWrite(uint32_t color) {
  rgbLedWrite(STATUS_LED_PIN, (color >> 16) & 0xFF, (color >> 8) & 0xFF, color & 0xFF);
}

void statusOff() {
  statusRevertAt = 0;
  statusPixelWrite(STATUS_OFF);
}

void statusFlash(uint32_t color) {
  statusRevertAt = millis() + STATUS_FLASH_MS;
  statusPixelWrite(color);
}

void statusTick() {
  if (statusRevertAt != 0 && millis() >= statusRevertAt) {
    statusOff();
  }
}
#endif

bool parseLongSafe(const char *s, long &outValue) {
  if (s == nullptr) return false;
  while (isspace(static_cast<unsigned char>(*s))) s++;
  if (*s == '\0') return false;

  char *endPtr = nullptr;
  errno = 0;
  long v = strtol(s, &endPtr, 10);
  if (errno != 0 || endPtr == s) return false;

  while (*endPtr != '\0') {
    if (!isspace(static_cast<unsigned char>(*endPtr))) return false;
    endPtr++;
  }
  outValue = v;
  return true;
}

bool parseUint8Safe(const char *s, uint8_t &outValue) {
  long v = 0;
  if (!parseLongSafe(s, v) || v < 0 || v > 255) return false;
  outValue = static_cast<uint8_t>(v);
  return true;
}

bool parseSetPayload(char *payload, uint8_t &strip, uint8_t &index, uint8_t &r, uint8_t &g, uint8_t &b) {
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
  if (stripLong < 1 || stripLong > NUM_STRIPS) return false;
  if (indexLong < 0 || indexLong >= LEDS_PER_STRIP) return false;
  if (!parseUint8Safe(tokens[base + 2], r) ||
      !parseUint8Safe(tokens[base + 3], g) ||
      !parseUint8Safe(tokens[base + 4], b)) {
    return false;
  }

  strip = static_cast<uint8_t>(stripLong);
  index = static_cast<uint8_t>(indexLong);
  return true;
}

void showDirtyStrips() {
  for (uint8_t i = 0; i < NUM_STRIPS; i++) {
    if (stripDirty[i] && strips[i] != nullptr) {
      strips[i]->show();
      stripDirty[i] = false;
    }
  }
}

void showAllStrips() {
  for (uint8_t i = 0; i < NUM_STRIPS; i++) {
    if (strips[i] != nullptr) {
      strips[i]->show();
    }
  }
  for (uint8_t i = 0; i < NUM_STRIPS; i++) {
    stripDirty[i] = false;
  }
}

void clearAllStrips() {
  for (uint8_t i = 0; i < NUM_STRIPS; i++) {
    if (strips[i] != nullptr) {
      strips[i]->clear();
      stripDirty[i] = true;
    }
  }
}

void reportError(const __FlashStringHelper *msg) {
  Serial.println(msg);
#if HAS_STATUS_LED
  statusFlash(STATUS_ERROR);
#endif
}

void handleCommand(char *payload) {
  if (payload == nullptr || payload[0] == '\0') {
    reportError(F("ERR:EMPTY"));
    return;
  }

  if (strcmp(payload, "OFF") == 0 || strcmp(payload, "CLEAR") == 0) {
    clearAllStrips();
    showAllStrips();
#if HAS_STATUS_LED
    statusFlash(STATUS_CMD);
#endif
    Serial.println(F("OK:CLEAR"));
    return;
  }

  if (strcmp(payload, "SHOW") == 0) {
#if HAS_STATUS_LED
    statusFlash(STATUS_CMD);
#endif
    showDirtyStrips();
    Serial.println(F("OK:SHOW"));
    return;
  }

  if (strcmp(payload, "PING") == 0) {
    Serial.println(F("PONG"));
#if HAS_STATUS_LED
    statusFlash(STATUS_PING);
#endif
    return;
  }

  uint8_t strip = 0;
  uint8_t index = 0;
  uint8_t r = 0;
  uint8_t g = 0;
  uint8_t b = 0;
  if (parseSetPayload(payload, strip, index, r, g, b)) {
    const uint8_t idx = static_cast<uint8_t>(strip - 1);
    if (strips[idx] == nullptr) {
      reportError(F("ERR:MAP"));
      return;
    }

    strips[idx]->setPixelColor(index, strips[idx]->Color(r, g, b));
    stripDirty[idx] = true;

    if (AUTO_SHOW_ON_SET) {
      strips[idx]->show();
      stripDirty[idx] = false;
    }

    Serial.println(F("OK:SET"));
#if HAS_STATUS_LED
    statusFlash(STATUS_CMD);
#endif
    return;
  }

  reportError(F("ERR:CMD"));
}

void initStrips() {
  for (uint8_t i = 0; i < NUM_STRIPS; i++) {
    strips[i] = new Adafruit_NeoPixel(LEDS_PER_STRIP, DATA_PINS[i], NEO_GRB + NEO_KHZ800);
    if (strips[i] == nullptr) {
      Serial.print(F("ERR:MEM strip "));
      Serial.println(i + 1);
      continue;
    }
    strips[i]->begin();
    strips[i]->setBrightness(DEFAULT_BRIGHTNESS);
    strips[i]->clear();
    strips[i]->show();
    stripDirty[i] = false;
  }
}

void setup() {
  Serial.begin(SERIAL_BAUD);
  Serial.setTimeout(5);

  initStrips();

#if HAS_STATUS_LED
  statusOff();
#endif

  Serial.println(F("PTL ESP32-S3 NeoPixel Ready."));
  Serial.println(F("Mode: 25 independent strips x 75 LEDs (shared RMT on show)."));
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

    if (!inFrame) continue;

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
