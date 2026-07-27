#include <FastLED.h>

#define NUM_STRIPS 5
#define PHYSICAL_LEDS_PER_STRIP 74

CRGB leds[NUM_STRIPS][PHYSICAL_LEDS_PER_STRIP];

void setup() {
  // 基础初始化
  FastLED.addLeds<WS2812B, 2, GRB>(leds[0], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, 3, GRB>(leds[1], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, 4, GRB>(leds[2], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, 5, GRB>(leds[3], PHYSICAL_LEDS_PER_STRIP);
  FastLED.addLeds<WS2812B, 6, GRB>(leds[4], PHYSICAL_LEDS_PER_STRIP);

  FastLED.setBrightness(50); // 低亮度测试，保护你的 3A 电源
  FastLED.clear();
  
  // 强制给每条线染上不同的颜色
  fill_solid(leds[0], PHYSICAL_LEDS_PER_STRIP, CRGB::Red);
  fill_solid(leds[1], PHYSICAL_LEDS_PER_STRIP, CRGB::Green);
  fill_solid(leds[2], PHYSICAL_LEDS_PER_STRIP, CRGB::Blue);
  fill_solid(leds[3], PHYSICAL_LEDS_PER_STRIP, CRGB::Yellow);
  fill_solid(leds[4], PHYSICAL_LEDS_PER_STRIP, CRGB::Purple);

  FastLED.show(); 
}

void loop() {
  // 什么都不做，只管亮着
}