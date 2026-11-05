#include "sensors.h"

#include <Adafruit_AHTX0.h>
#include <BH1750.h>
#include <Wire.h>

#include "config.h"

namespace sensors {

namespace {
Adafruit_AHTX0 aht;
bool airReady = false;
BH1750 lightMeter;
bool lightReady = false;
}  // namespace

void begin() {
  analogReadResolution(12);
  for (const auto& ch : core::kChannels) {
    // 11 dB attenuation covers ~0–3.1 V, the probe's full output at 3.3 V supply.
    if (ch.source == core::Source::Soil) analogSetPinAttenuation(ch.pin, ADC_11db);
  }
  Wire.begin(kPinI2cSda, kPinI2cScl);
  airReady = aht.begin(&Wire);
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
      if (!airReady) return false;
      // Each call is one ~80 ms measurement that returns both values; measuring twice
      // per cycle is simpler than caching and far below the sensor's self-heating limit.
      sensors_event_t humidity, temp;
      if (!aht.getEvent(&humidity, &temp)) return false;
      *value = ch.source == core::Source::AirTemp ? temp.temperature : humidity.relative_humidity;
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
