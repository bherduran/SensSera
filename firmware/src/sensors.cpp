#include "sensors.h"

#include <BH1750.h>
#include <DHT.h>
#include <Wire.h>

#include "config.h"

namespace sensors {

namespace {
DHT dht(kPinDht, DHT22);
BH1750 lightMeter;
bool lightReady = false;
}  // namespace

void begin() {
  analogReadResolution(12);
  for (const auto& ch : core::kChannels) {
    // 11 dB attenuation covers ~0–3.1 V, the probe's full output at 3.3 V supply.
    if (ch.source == core::Source::Soil) analogSetPinAttenuation(ch.pin, ADC_11db);
  }
  dht.begin();
  Wire.begin(kPinI2cSda, kPinI2cScl);
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
      // The DHT22 is slow (max one read per 2 s) and the library caches the last
      // result, so reading both values back to back costs one sensor read.
      float v = ch.source == core::Source::AirTemp ? dht.readTemperature() : dht.readHumidity();
      if (isnan(v)) return false;
      *value = v;
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
