// ============================================================
// ESP32 1000 灯珠通断测试 — FastLED / WS2812B
// 上电即跑蓝色拖尾跑马灯，无串口控制，专用于接线通断确认
//
// Arduino IDE：
//   - ESP32 Dev Module  → 8 路 × 125 灯 = 1000
//   - ESP32S3 Dev Module → 4 路 × 250 灯 = 1000
// 依赖库：FastLED
// ============================================================
#include <FastLED.h>

#define DEFAULT_BRIGHTNESS 40
#define MARQUEE_INTERVAL_MS 20
#define MARQUEE_TRAIL 12
#define MAX_POWER_MA 5000

#if defined(CONFIG_IDF_TARGET_ESP32S3) || defined(ARDUINO_ESP32S3_DEV) || \
    defined(ARDUINO_ESP32S3_BOX) || defined(ARDUINO_ESP32S3_BOX_LITE)

#define NUM_STRIPS 4
#define LEDS_PER_STRIP 250
#define STRIP_PIN_1 4
#define STRIP_PIN_2 5
#define STRIP_PIN_3 6
#define STRIP_PIN_4 7

#else

#define NUM_STRIPS 8
#define LEDS_PER_STRIP 125
#define STRIP_PIN_1 16
#define STRIP_PIN_2 17
#define STRIP_PIN_3 18
#define STRIP_PIN_4 19
#define STRIP_PIN_5 21
#define STRIP_PIN_6 22
#define STRIP_PIN_7 23
#define STRIP_PIN_8 25

#endif

#define TOTAL_LEDS (NUM_STRIPS * LEDS_PER_STRIP)

CRGB leds[NUM_STRIPS][LEDS_PER_STRIP];
uint16_t marqueePos = 0;
uint32_t lastMarqueeMs = 0;

void paintTrail(uint16_t headPos) {
  FastLED.clear();

  for (uint8_t t = 0; t < MARQUEE_TRAIL; t++) {
    const int16_t pos = static_cast<int16_t>(headPos) - static_cast<int16_t>(t);
    if (pos < 0) {
      continue;
    }

    const uint8_t strip = static_cast<uint8_t>(pos / LEDS_PER_STRIP);
    const uint16_t index = static_cast<uint16_t>(pos % LEDS_PER_STRIP);
    if (strip >= NUM_STRIPS) {
      continue;
    }

    const uint8_t fade = static_cast<uint8_t>(255 - (t * (255 / MARQUEE_TRAIL)));
    leds[strip][index] = CRGB(0, 0, fade);
  }
}

void setup() {
#if NUM_STRIPS >= 1
  FastLED.addLeds<WS2812B, STRIP_PIN_1, GRB>(leds[0], LEDS_PER_STRIP);
#endif
#if NUM_STRIPS >= 2
  FastLED.addLeds<WS2812B, STRIP_PIN_2, GRB>(leds[1], LEDS_PER_STRIP);
#endif
#if NUM_STRIPS >= 3
  FastLED.addLeds<WS2812B, STRIP_PIN_3, GRB>(leds[2], LEDS_PER_STRIP);
#endif
#if NUM_STRIPS >= 4
  FastLED.addLeds<WS2812B, STRIP_PIN_4, GRB>(leds[3], LEDS_PER_STRIP);
#endif
#if NUM_STRIPS >= 5
  FastLED.addLeds<WS2812B, STRIP_PIN_5, GRB>(leds[4], LEDS_PER_STRIP);
#endif
#if NUM_STRIPS >= 6
  FastLED.addLeds<WS2812B, STRIP_PIN_6, GRB>(leds[5], LEDS_PER_STRIP);
#endif
#if NUM_STRIPS >= 7
  FastLED.addLeds<WS2812B, STRIP_PIN_7, GRB>(leds[6], LEDS_PER_STRIP);
#endif
#if NUM_STRIPS >= 8
  FastLED.addLeds<WS2812B, STRIP_PIN_8, GRB>(leds[7], LEDS_PER_STRIP);
#endif

  FastLED.setBrightness(DEFAULT_BRIGHTNESS);
  FastLED.setMaxPowerInVoltsAndMilliamps(5, MAX_POWER_MA);
  FastLED.clear();
  FastLED.show();

  marqueePos = 0;
  lastMarqueeMs = millis();
}

void loop() {
  const uint32_t now = millis();
  if (now - lastMarqueeMs < MARQUEE_INTERVAL_MS) {
    return;
  }

  lastMarqueeMs = now;
  paintTrail(marqueePos);
  FastLED.show();
  marqueePos = static_cast<uint16_t>((marqueePos + 1) % TOTAL_LEDS);
}
