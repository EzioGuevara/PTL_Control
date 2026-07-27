// ============================================================
// PTL LED Control — Arduino Nano (FastLED / WS2812B)
// Protocol (与 PTLControl.Compat CommandService 对齐):
//   <Layer,Index,R,G,B>  单灯设置（可叠加，不清全屏；RGB=0,0,0 即关该灯）
//   <OFF> / <CLEAR>      全灭
// 兼容旧指令（手工调试用）:
//   <ON>                 全白
//   <MARQUEE>            板端蓝色跑马灯
//   <LLayer,Index>       清屏后只亮一颗绿
// ============================================================
#include <FastLED.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>

#define NUM_STRIPS 5
#define PHYSICAL_LEDS_PER_STRIP 74
#define SERIAL_BAUD 115200
#define MARQUEE_INTERVAL_MS 30
#define MAX_FRAME_LEN 48
#define DEFAULT_BRIGHTNESS 128
#define BOOT_CLEAR_TIMES 3

// 数据脚：Layer1..5 → D2..D6
#define STRIP_PIN_1 2
#define STRIP_PIN_2 3
#define STRIP_PIN_3 4
#define STRIP_PIN_4 5
#define STRIP_PIN_5 6

CRGB leds[NUM_STRIPS][PHYSICAL_LEDS_PER_STRIP];

char frameBuf[MAX_FRAME_LEN];
uint8_t frameLen = 0;
bool inFrame = false;
bool frameOverflow = false;

enum Mode { MODE_IDLE, MODE_MARQUEE };
Mode currentMode = MODE_IDLE;
uint16_t marqueePos = 0;
uint32_t lastMarqueeMs = 0;

bool parseIntToken(char *&p, long &outValue) {
  while (*p != '\0' && isspace(static_cast<unsigned char>(*p))) {
    p++;
  }
  if (*p == '\0') {
    return false;
  }

  char *endPtr = nullptr;
  long v = strtol(p, &endPtr, 10);
  if (endPtr == p) {
    return false;
  }

  p = endPtr;
  outValue = v;
  return true;
}

bool parseRgbSet(char *payload, int &layer, int &index, int &r, int &g, int &b) {
  // 期望: Layer,Index,R,G,B
  char *p = payload;
  long values[5];

  for (uint8_t i = 0; i < 5; i++) {
    if (!parseIntToken(p, values[i])) {
      return false;
    }
    while (*p != '\0' && isspace(static_cast<unsigned char>(*p))) {
      p++;
    }
    if (i < 4) {
      if (*p != ',') {
        return false;
      }
      p++;
    }
  }

  while (*p != '\0' && isspace(static_cast<unsigned char>(*p))) {
    p++;
  }
  if (*p != '\0') {
    return false;
  }

  if (values[0] < 1 || values[0] > NUM_STRIPS) {
    return false;
  }
  if (values[1] < 0 || values[1] >= PHYSICAL_LEDS_PER_STRIP) {
    return false;
  }
  if (values[2] < 0 || values[2] > 255 ||
      values[3] < 0 || values[3] > 255 ||
      values[4] < 0 || values[4] > 255) {
    return false;
  }

  layer = static_cast<int>(values[0]);
  index = static_cast<int>(values[1]);
  r = static_cast<int>(values[2]);
  g = static_cast<int>(values[3]);
  b = static_cast<int>(values[4]);
  return true;
}

bool parseLegacyL(char *payload, int &layer, int &index) {
  // 期望: L{layer},{index}
  if (payload[0] != 'L' && payload[0] != 'l') {
    return false;
  }

  char *p = payload + 1;
  long layerLong = 0;
  long indexLong = 0;
  if (!parseIntToken(p, layerLong)) {
    return false;
  }
  while (*p != '\0' && isspace(static_cast<unsigned char>(*p))) {
    p++;
  }
  if (*p != ',') {
    return false;
  }
  p++;
  if (!parseIntToken(p, indexLong)) {
    return false;
  }
  while (*p != '\0' && isspace(static_cast<unsigned char>(*p))) {
    p++;
  }
  if (*p != '\0') {
    return false;
  }

  if (layerLong < 1 || layerLong > NUM_STRIPS) {
    return false;
  }
  if (indexLong < 0 || indexLong >= PHYSICAL_LEDS_PER_STRIP) {
    return false;
  }

  layer = static_cast<int>(layerLong);
  index = static_cast<int>(indexLong);
  return true;
}

void allOff() {
  currentMode = MODE_IDLE;
  FastLED.clear();
  FastLED.show();
}

void handleCommand(char *payload) {
  if (payload == nullptr || payload[0] == '\0') {
    Serial.println(F("ERR:EMPTY"));
    return;
  }

  if (strcmp(payload, "OFF") == 0 || strcmp(payload, "CLEAR") == 0) {
    allOff();
    Serial.println(F("OK:OFF"));
    return;
  }

  if (strcmp(payload, "ON") == 0) {
    currentMode = MODE_IDLE;
    for (uint8_t s = 0; s < NUM_STRIPS; s++) {
      fill_solid(leds[s], PHYSICAL_LEDS_PER_STRIP, CRGB(255, 255, 255));
    }
    FastLED.show();
    Serial.println(F("OK:ON"));
    return;
  }

  if (strcmp(payload, "MARQUEE") == 0) {
    currentMode = MODE_MARQUEE;
    marqueePos = 0;
    lastMarqueeMs = millis();
    Serial.println(F("OK:MARQUEE"));
    return;
  }

  if (strcmp(payload, "PING") == 0) {
    Serial.println(F("PONG"));
    return;
  }

  int layer = 0;
  int index = 0;
  int r = 0;
  int g = 0;
  int b = 0;

  // 主协议：与 PTLControl 一致 <Layer,Index,R,G,B>
  if (parseRgbSet(payload, layer, index, r, g, b)) {
    currentMode = MODE_IDLE;
    leds[layer - 1][index] = CRGB(
        static_cast<uint8_t>(r),
        static_cast<uint8_t>(g),
        static_cast<uint8_t>(b));
    FastLED.show();
    Serial.println(F("OK:SET"));
    return;
  }

  // 旧协议：<LLayer,Index> 清屏后单点绿
  if (parseLegacyL(payload, layer, index)) {
    currentMode = MODE_IDLE;
    FastLED.clear();
    leds[layer - 1][index] = CRGB(0, 255, 0);
    FastLED.show();
    Serial.print(F("OK:L"));
    Serial.print(layer);
    Serial.print(',');
    Serial.println(index);
    return;
  }

  Serial.println(F("ERR:CMD"));
}

void initStrips() {
  FastLED.addLeds<WS2812B, STRIP_PIN_1, GRB>(leds[0], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_2, GRB>(leds[1], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_3, GRB>(leds[2], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_4, GRB>(leds[3], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_5, GRB>(leds[4], PHYSICAL_LEDS_PER_STRIP);

  FastLED.setBrightness(DEFAULT_BRIGHTNESS);
  // 5V 供电粗限流，降低整屏误亮时的冲击（可按电源能力调整）
  FastLED.setMaxPowerInVoltsAndMilliamps(5, 4000);

  for (uint8_t i = 0; i < BOOT_CLEAR_TIMES; i++) {
    FastLED.clear();
    FastLED.show();
    delay(20);
  }
}

void setup() {
  Serial.begin(SERIAL_BAUD);
  Serial.setTimeout(5);

  initStrips();

  Serial.println(F("PTL Nano Ready."));
  Serial.print(F("Strips="));
  Serial.print(NUM_STRIPS);
  Serial.print(F(" LEDs/strip="));
  Serial.print(PHYSICAL_LEDS_PER_STRIP);
  Serial.print(F(" Brightness="));
  Serial.println(DEFAULT_BRIGHTNESS);
  Serial.println(F("Cmd: <L,I,R,G,B> <OFF> <ON> <MARQUEE> <PING>"));
}

void loop() {
  while (Serial.available() > 0) {
    const char c = static_cast<char>(Serial.read());

    if (c == '<') {
      inFrame = true;
      frameOverflow = false;
      frameLen = 0;
      continue;
    }

    if (!inFrame) {
      continue;
    }

    if (c == '>') {
      inFrame = false;
      if (frameOverflow) {
        Serial.println(F("ERR:OVF"));
        continue;
      }
      frameBuf[frameLen] = '\0';
      handleCommand(frameBuf);
      continue;
    }

    // 忽略帧内控制字符，避免噪声污染
    if (c < 32) {
      continue;
    }

    if (frameLen < (MAX_FRAME_LEN - 1)) {
      frameBuf[frameLen++] = c;
    } else {
      frameOverflow = true;
    }
  }

  if (currentMode == MODE_MARQUEE) {
    const uint32_t now = millis();
    if (now - lastMarqueeMs >= MARQUEE_INTERVAL_MS) {
      lastMarqueeMs = now;
      FastLED.clear();
      for (uint8_t s = 0; s < NUM_STRIPS; s++) {
        leds[s][marqueePos] = CRGB(0, 0, 255);
      }
      FastLED.show();
      marqueePos = static_cast<uint16_t>((marqueePos + 1) % PHYSICAL_LEDS_PER_STRIP);
    }
  }
}
