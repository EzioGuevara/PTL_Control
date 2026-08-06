// ============================================================
// Arduino Nano 单路常亮 — 无帧缓冲流式发送 WS2812B
//
// 不占用“灯数 × 3”的 RAM，逐颗写出同一颜色。
// 灯数主要受电源限制，1000+ 可以。
//
// 上电全亮；无需 FastLED / NeoPixel 库。
// 开发板：Arduino Nano（16MHz），数据脚默认 D2（必须是 D0–D7）
// ============================================================

#define DATA_PIN    2
#define TOTAL_LEDS  1000

#define COLOR_R  0
#define COLOR_G  0
#define COLOR_B  60

// 每隔几秒重发；改 0 则只在 setup 发一次
#define RESEND_MS  2000

#if (F_CPU != 16000000L)
#error "按 Nano 16MHz 编写，请确认开发板时钟。"
#endif

#if (DATA_PIN > 7)
#error "当前实现使用 PORTD（D0-D7）。改 D8+ 需换端口定义。"
#endif

#define LED_PORT  PORTD
#define LED_DDR   DDRD
#define LED_BIT   DATA_PIN
#define LED_MASK  (1 << LED_BIT)

/*
 * 16MHz：1 cycle = 62.5ns
 * WS2812 对“高电平宽度”敏感；位与位之间的低电平略长无妨。
 *   0 码高 ≈ 0.4µs（~6 cy），1 码高 ≈ 0.8µs（~13 cy）
 */
static void sendByte(uint8_t byte) {
  for (uint8_t i = 0; i < 8; i++) {
    if (byte & 0x80) {
      LED_PORT |= LED_MASK;
      __builtin_avr_delay_cycles(13);
      LED_PORT &= ~LED_MASK;
      __builtin_avr_delay_cycles(5);
    } else {
      LED_PORT |= LED_MASK;
      __builtin_avr_delay_cycles(5);
      LED_PORT &= ~LED_MASK;
      __builtin_avr_delay_cycles(13);
    }
    byte <<= 1;
  }
}

static void sendPixelGRB(uint8_t r, uint8_t g, uint8_t b) {
  sendByte(g);
  sendByte(r);
  sendByte(b);
}

static void showSolid(uint16_t count, uint8_t r, uint8_t g, uint8_t b) {
  noInterrupts();
  for (uint16_t i = 0; i < count; i++) {
    sendPixelGRB(r, g, b);
  }
  interrupts();
  delayMicroseconds(300);
}

void setup() {
  LED_DDR |= LED_MASK;
  LED_PORT &= ~LED_MASK;
  delay(20);
  showSolid(TOTAL_LEDS, COLOR_R, COLOR_G, COLOR_B);
}

void loop() {
#if RESEND_MS > 0
  delay(RESEND_MS);
  showSolid(TOTAL_LEDS, COLOR_R, COLOR_G, COLOR_B);
#endif
}
