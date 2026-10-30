#include "settings.h"

#include <Preferences.h>

#include "config.h"

namespace settings {

namespace {
constexpr const char* kNamespace = "saksi";

// NVS keys are limited to 15 characters.
String calKey(const char* prefix, size_t i) { return String(prefix) + core::kChannels[i].code; }
}  // namespace

void load(Settings& s) {
  Preferences p;
  p.begin(kNamespace, true);
  s.wifiSsid = p.getString("wifi_ssid", "");
  s.wifiPassword = p.getString("wifi_pass", "");
  s.apiUrl = p.getString("api_url", kDefaultApiUrl);
  s.nodeToken = p.getString("token", "");
  s.intervalSec = p.getUInt("interval", kDefaultIntervalSec);
  s.channelMask = p.getUChar("channels", kDefaultChannelMask);
  s.insecureTls = p.getBool("insecure", false);
  for (size_t i = 0; i < core::kChannelCount; i++) {
    s.soilCal[i].dryMv = p.getUShort(calKey("d_", i).c_str(), kDefaultSoilDryMv);
    s.soilCal[i].wetMv = p.getUShort(calKey("w_", i).c_str(), kDefaultSoilWetMv);
  }
  p.end();

  if (s.intervalSec < kMinIntervalSec || s.intervalSec > kMaxIntervalSec) {
    s.intervalSec = kDefaultIntervalSec;
  }
}

void save(const Settings& s) {
  Preferences p;
  p.begin(kNamespace, false);
  p.putString("wifi_ssid", s.wifiSsid);
  p.putString("wifi_pass", s.wifiPassword);
  p.putString("api_url", s.apiUrl);
  p.putString("token", s.nodeToken);
  p.putUInt("interval", s.intervalSec);
  p.putUChar("channels", s.channelMask);
  p.putBool("insecure", s.insecureTls);
  for (size_t i = 0; i < core::kChannelCount; i++) {
    if (core::kChannels[i].source != core::Source::Soil) continue;
    p.putUShort(calKey("d_", i).c_str(), s.soilCal[i].dryMv);
    p.putUShort(calKey("w_", i).c_str(), s.soilCal[i].wetMv);
  }
  p.end();
}

}  // namespace settings
