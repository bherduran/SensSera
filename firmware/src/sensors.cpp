#include "sensors.h"

#include <Wire.h>

#include "config.h"

namespace sensors {

namespace {
constexpr uint8_t kAhtAddress = 0x38;
constexpr uint8_t kBh1750Address = 0x23;
// air_temp and air_humidity are read back to back; one measurement serves both.
constexpr uint32_t kAirReuseMs = 2000;

TwoWire& lightBus = Wire;
TwoWire& airBus = Wire1;

bool airReady = false;
uint32_t airAtMs = 0;
float airTempC = 0;
float airHumidity = 0;

bool i2cWrite(TwoWire& bus, uint8_t address, const uint8_t* bytes, size_t n) {
  bus.beginTransmission(address);
  bus.write(bytes, n);
  return bus.endTransmission() == 0;
}

bool i2cRead(TwoWire& bus, uint8_t address, uint8_t* buf, size_t n) {
  if (bus.requestFrom(address, static_cast<uint8_t>(n)) != n) return false;
  for (size_t i = 0; i < n; i++) buf[i] = bus.read();
  return true;
}

// Own driver instead of Adafruit AHTX0: that one polls the busy bit with no time limit, so a
// sensor that hangs or a loose cable would block until the watchdog resets the board, and
// since begin() runs at boot, the board would reset in a loop and never upload.
bool beginAht() {
  uint8_t status;
  if (!i2cRead(airBus, kAhtAddress, &status, 1)) return false;
  if (!(status & core::kAhtStatusCalibrated)) {
    const uint8_t init[] = {0xBE, 0x08, 0x00};
    if (!i2cWrite(airBus, kAhtAddress, init, sizeof init)) return false;
    delay(10);
  }
  return true;
}

bool measureAir() {
  if (airAtMs != 0 && millis() - airAtMs < kAirReuseMs) return true;
  const uint8_t trigger[] = {0xAC, 0x33, 0x00};
  if (!i2cWrite(airBus, kAhtAddress, trigger, sizeof trigger)) return false;
  delay(80);  // conversion time per the datasheet
  uint8_t frame[core::kAhtFrameLen];
  for (int attempt = 0; attempt < 5; attempt++) {
    if (i2cRead(airBus, kAhtAddress, frame, sizeof frame) &&
        core::decodeAht(frame, &airTempC, &airHumidity)) {
      airAtMs = millis();
      return true;
    }
    delay(10);
  }
  return false;
}

// One-time mode on every read, not continuous mode set once at boot (what the BH1750 library
// did): a sensor that loses power for a moment comes back powered down and keeps answering 0,
// which would read as darkness until the board restarted. Re-sending the mode each time also
// picks up a sensor that was plugged in late, and a missing one simply fails this read.
bool measureLight(float* lux) {
  const uint8_t mode = core::kBh1750OneTimeHighRes;
  if (!i2cWrite(lightBus, kBh1750Address, &mode, 1)) return false;
  delay(180);  // maximum high-resolution conversion time per the datasheet
  uint8_t frame[2];
  if (!i2cRead(lightBus, kBh1750Address, frame, sizeof frame)) return false;
  *lux = core::bh1750Lux(frame);
  return true;
}

}  // namespace

void begin() {
  analogReadResolution(12);
  for (const auto& ch : core::kChannels) {
    // 11 dB attenuation covers ~0–3.1 V, the probe's full output at 3.3 V supply.
    if (ch.source == core::Source::Soil) analogSetPinAttenuation(ch.pin, ADC_11db);
  }
  lightBus.begin(kPinLightSda, kPinLightScl, kLightI2cHz);
  airBus.begin(kPinAirSda, kPinAirScl, kAirI2cHz);
  airReady = beginAht();
  if (!airReady) log_w("AHT25 not found on GPIO%u/%u", kPinAirSda, kPinAirScl);
  const uint8_t powerOn = 0x01;
  if (!i2cWrite(lightBus, kBh1750Address, &powerOn, 1))
    log_w("BH1750 not found on GPIO%u/%u", kPinLightSda, kPinLightScl);
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
      // Re-initialised after any failure, so a sensor that was unplugged or plugged in after
      // boot starts reporting again without a reset.
      if (!airReady) airReady = beginAht();
      if (!airReady || !measureAir()) {
        airReady = false;
        return false;
      }
      *value = ch.source == core::Source::AirTemp ? airTempC : airHumidity;
      return true;
    }

    case core::Source::Light:
      return measureLight(value);
  }
  return false;
}

}  // namespace sensors
