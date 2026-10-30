// Saksı Pilotu board firmware, Phase 1: measure, buffer, upload.
// Pumps, server config and commands arrive in Phase 2 (PRD §6, §8.3).

#include <Arduino.h>
#include <WiFi.h>
#include <esp_task_wdt.h>
#include <time.h>

#include "config.h"
#include "console.h"
#include "core.h"
#include "sensors.h"
#include "settings.h"
#include "uploader.h"

namespace {

Settings cfg;

// ~5.8 days of readings at 6 channels every 30 s, so a weekend Wi-Fi outage at the
// friend's house loses nothing. The buffer is RAM only: a power cut still loses it.
constexpr size_t kPsramBufferReadings = 100000;
constexpr size_t kInternalBufferReadings = 2048;
core::RingBuffer<core::Reading>* buffer = nullptr;

uint32_t lastMeasureMs = 0;
bool measuredOnce = false;
uint32_t wifiBeginMs = 0;
constexpr uint32_t kWifiRetryMs = 120000;

void setupBuffer() {
  size_t capacity = kInternalBufferReadings;
  core::Reading* storage = nullptr;
  if (psramFound()) {
    storage = static_cast<core::Reading*>(ps_malloc(kPsramBufferReadings * sizeof(core::Reading)));
    if (storage) capacity = kPsramBufferReadings;
  }
  if (!storage) storage = new core::Reading[capacity];
  buffer = new core::RingBuffer<core::Reading>(storage, capacity);
  log_i("Reading buffer: %u slots in %s", static_cast<unsigned>(capacity),
        capacity == kPsramBufferReadings ? "PSRAM" : "internal RAM");
}

void setupWatchdog() {
  // Arduino core 2.x (ESP-IDF 4.4) starts the task watchdog at 5 s with core 0's idle
  // task subscribed. An HTTPS upload can block loop() longer than that, so re-create it
  // with a longer timeout; IDF 4.4 has no reconfigure call, and deinit refuses while
  // any task is still subscribed.
  TaskHandle_t idle0 = xTaskGetIdleTaskHandleForCPU(0);
  esp_task_wdt_delete(idle0);
  esp_task_wdt_deinit();
  esp_task_wdt_init(kWatchdogTimeoutSec, true);  // true: reset the board, don't just log
  esp_task_wdt_add(idle0);
  esp_task_wdt_add(nullptr);  // this (loop) task
}

void beginWifi() {
  if (cfg.wifiSsid.isEmpty()) {
    log_w("No Wi-Fi set; use: wifi \"<ssid>\" \"<password>\"");
    return;
  }
  WiFi.disconnect();
  WiFi.begin(cfg.wifiSsid.c_str(), cfg.wifiPassword.c_str());
  wifiBeginMs = millis();
  log_i("Connecting to %s", cfg.wifiSsid.c_str());
}

void keepWifi() {
  // setAutoReconnect handles ordinary drops; this restarts a join that never succeeded
  // (router rebooting, wrong band at first try). The board is never restarted for Wi-Fi,
  // because that would empty the reading buffer.
  if (cfg.wifiSsid.isEmpty() || WiFi.status() == WL_CONNECTED) return;
  if (millis() - wifiBeginMs > kWifiRetryMs) beginWifi();
}

void measure() {
  time_t now = time(nullptr);
  for (size_t i = 0; i < core::kChannelCount; i++) {
    if (!cfg.channelEnabled(i)) continue;
    float value;
    if (!sensors::read(i, cfg, &value)) {
      log_w("%s: no reading", core::kChannels[i].code);
      continue;
    }
    buffer->push({static_cast<uint32_t>(now), value, static_cast<uint8_t>(i)});
  }
}

}  // namespace

void setup() {
  Serial.begin(115200);
  delay(1500);  // let the USB serial port enumerate so the boot log isn't lost
  Serial.printf("\nSaksi Pilotu firmware %s (reset reason %d). Type help.\n", FW_VERSION,
                static_cast<int>(esp_reset_reason()));

  setupWatchdog();
  settings::load(cfg);
  setupBuffer();
  sensors::begin();

  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  configTime(0, 0, "pool.ntp.org", "time.google.com", "time.cloudflare.com");  // UTC
  beginWifi();
}

void loop() {
  esp_task_wdt_reset();

  if (console::poll(cfg, *buffer)) beginWifi();
  keepWifi();

  uint32_t now = millis();
  bool clockValid = time(nullptr) >= static_cast<time_t>(core::kMinValidEpoch);
  if (clockValid && (!measuredOnce || now - lastMeasureMs >= cfg.intervalSec * 1000)) {
    measure();
    measuredOnce = true;
    lastMeasureMs = now;
  }

  uploader::tick(cfg, *buffer);
  delay(10);
}
