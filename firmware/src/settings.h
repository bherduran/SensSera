#pragma once

#include <Arduino.h>

#include "core.h"

// Everything the board needs that isn't compiled in. Lives in NVS ("Preferences"), which
// a browser re-flash leaves untouched, so Wi-Fi and the token survive firmware updates.
struct Settings {
  String wifiSsid;
  String wifiPassword;
  String apiUrl;
  String nodeToken;  // "<nodeId>.<secret>"; never printed
  uint32_t intervalSec;
  uint8_t channelMask;  // bit i = core::kChannels[i] enabled
  bool insecureTls;     // bench tests only: skip certificate verification
  core::SoilCalibration soilCal[core::kChannelCount];

  bool channelEnabled(size_t i) const { return channelMask & (1u << i); }
};

namespace settings {

void load(Settings& s);
void save(const Settings& s);

}  // namespace settings
