// ============================================================
// Arduino Nano 1000 灯珠通断测试 — FastLED / WS2812B
// 上电即跑蓝色拖尾跑马灯，无串口控制
//
// Nano 只有约 2KB SRAM，无法同时缓存 1000 颗灯（约 3KB）。
// 做法：5 路共用一块 200 灯缓冲，每帧按路填充并单独 show。
//
// 布局：5 路 × 200 灯 = 1000（数据脚 D2..D6，与 LEDControl.ino 一致）
// Arduino IDE：开发板选 Arduino Nano，安装 FastLED
// ============================================================
#include <FastLED.h>

#define NUM_STRIPS 5
#define LEDS_PER_STRIP 200
#define TOTAL_LEDS (NUM_STRIPS * LEDS_PER_STRIP)

#define STRIP_PIN_1 2
#define STRIP_PIN_2 3
#define STRIP_PIN_3 4
#define STRIP_PIN_4 5
#define STRIP_PIN_5 6

#define DEFAULT_BRIGHTNESS 40
#define MARQUEE_INTERVAL_MS 30
#define MARQUEE_TRAIL 12
#define MAX_POWER_MA 4000

// 仅 200×3 = 600 字节，Nano 可承受
CRGB leds[LEDS_PER_STRIP];

uint16_t marqueePos = 0;
uint32_t lastMarqueeMs = 0;

void fillStripForHead(uint8_t strip, uint16_t headPos) {
  fill_solid(leds, LEDS_PER_STRIP, CRGB::Black);

  const uint16_t stripBase = static_cast<uint16_t>(strip) * LEDS_PER_STRIP;

  for (uint8_t t = 0; t < MARQUEE_TRAIL; t++) {
    const int16_t pos = static_cast<int16_t>(headPos) - static_cast<int16_t>(t);
    if (pos < 0) {
      continue;
    }

    const uint16_t absPos = static_cast<uint16_t>(pos);
    if (absPos < stripBase || absPos >= stripBase + LEDS_PER_STRIP) {
      continue;
    }

    const uint16_t index = absPos - stripBase;
    const uint8_t fade = static_cast<uint8_t>(255 - (t * (255 / MARQUEE_TRAIL)));
    leds[index] = CRGB(0, 0, fade);
  }
}

void showAllStrips(uint16_t headPos) {
  for (uint8_t s = 0; s < NUM_STRIPS; s++) {
    fillStripForHead(s, headPos);
    // 只刷新这一路，避免 5 路共用缓冲时互相覆盖
    FastLED[s].showLeds(DEFAULT_BRIGHTNESS);
  }
}

void setup() {
  FastLED.addLeds<WS2812B, STRIP_PIN_1, GRB>(leds, LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_2, GRB>(leds, LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_3, GRB>(leds, LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_4, GRB>(leds, LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, STRIP_PIN_5, GRB>(leds, LEDS_PER_STRIP);

  FastLED.setBrightness(DEFAULT_BRIGHTNESS);
  FastLED.setMaxPowerInVoltsAndMilliamps(5, MAX_POWER_MA);

  fill_solid(leds, LEDS_PER_STRIP, CRGB::Black);
  for (uint8_t s = 0; s < NUM_STRIPS; s++) {
    FastLED[s].showLeds(DEFAULT_BRIGHTNESS);
  }

  marqueePos = 0;
  lastMarqueeMs = millis();
}

void loop() {
  const uint32_t now = millis();
  if (now - lastMarqueeMs < MARQUEE_INTERVAL_MS) {
    return;
  }

  lastMarqueeMs = now;
  showAllStrips(marqueePos);
  marqueePos = static_cast<uint16_t>((marqueePos + 1) % TOTAL_LEDS);
}
