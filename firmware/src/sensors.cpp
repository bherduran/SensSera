#include "sensors.h"

#include <BH1750.h>
#include <Wire.h>

#include "config.h"

namespace sensors {

namespace {
constexpr uint8_t kAhtAddress = 0x38;
// air_temp and air_humidity are read back to back; one measurement serves both.
constexpr uint32_t kAirReuseMs = 2000;

bool airReady = false;
uint32_t airAtMs = 0;
float airTempC = 0;
float airHumidity = 0;
BH1750 lightMeter;
bool lightReady = false;

bool ahtWrite(const uint8_t* bytes, size_t n) {
  Wire.beginTransmission(kAhtAddress);
  Wire.write(bytes, n);
  return Wire.endTransmission() == 0;
}

bool ahtRead(uint8_t* buf, size_t n) {
  if (Wire.requestFrom(kAhtAddress, static_cast<uint8_t>(n)) != n) return false;
  for (size_t i = 0; i < n; i++) buf[i] = Wire.read();
  return true;
}

// Own driver instead of Adafruit AHTX0: that one polls the busy bit with no time limit, so a
// sensor that hangs or a loose cable would block until the watchdog resets the board, and
// since begin() runs at boot, the board would reset in a loop and never upload.
bool beginAht() {
  uint8_t status;
  if (!ahtRead(&status, 1)) return false;
  if (!(status & core::kAhtStatusCalibrated)) {
    const uint8_t init[] = {0xBE, 0x08, 0x00};
    if (!ahtWrite(init, sizeof init)) return false;
    delay(10);
  }
  return true;
}

bool measureAir() {
  if (airAtMs != 0 && millis() - airAtMs < kAirReuseMs) return true;
  const uint8_t trigger[] = {0xAC, 0x33, 0x00};
  if (!ahtWrite(trigger, sizeof trigger)) return false;
  delay(80);  // conversion time per the datasheet
  uint8_t frame[core::kAhtFrameLen];
  for (int attempt = 0; attempt < 5; attempt++) {
    if (ahtRead(frame, sizeof frame) && core::decodeAht(frame, &airTempC, &airHumidity)) {
      airAtMs = millis();
      return true;
    }
    delay(10);
  }
  return false;
}

}  // namespace

void begin() {
  analogReadResolution(12);
  for (const auto& ch : core::kChannels) {
    // 11 dB attenuation covers ~0–3.1 V, the probe's full output at 3.3 V supply.
    if (ch.source == core::Source::Soil) analogSetPinAttenuation(ch.pin, ADC_11db);
  }
  Wire.begin(kPinI2cSda, kPinI2cScl);
  airReady = beginAht();
  if (!airReady) log_w("AHT25 not found on I2C");
  lightReady = lightMeter.begin(BH1750::CONTINUOUS_HIGH_RES_MODE);
  if (!lightReady) log_w("BH1750 not found on I2C");
}

float soilMillivolts(int pin) {
  uint32_t samples[kSamplesPerReading];
  for (auto& s : samples) {
    s = analogReadMilliVolts(pin);  // eFuse-calibrated, so values transfer between boards
    delay(5);
  }
  return core::average(samples, kSamplesPerReading);
}

bool read(size_t channel, const Settings& s, float* value) {
  const core::ChannelDef& ch = core::kChannels[channel];
  switch (ch.source) {
    case core::Source::Soil:
      return core::soilPercent(soilMillivolts(ch.pin), s.soilCal[channel], value);

    case core::Source::AirTemp:
    case core::Source::AirHumidity: {
      // Retried here so a sensor plugged in after boot starts reporting without a reset.
      if (!airReady) airReady = beginAht();
      if (!airReady || !measureAir()) return false;
      *value = ch.source == core::Source::AirTemp ? airTempC : airHumidity;
      return true;
    }

    case core::Source::Light: {
      if (!lightReady) return false;
      float lux = lightMeter.readLightLevel();
      if (lux < 0) return false;
      *value = lux;
      return true;
    }
  }
  return false;
}

}  // namespace sensors
