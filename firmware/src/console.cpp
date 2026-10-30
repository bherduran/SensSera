#include "console.h"

#include <WiFi.h>
#include <time.h>

#include "config.h"
#include "sensors.h"
#include "uploader.h"

namespace console {

namespace {
char line[256];
size_t lineLen = 0;

void printHelp() {
  Serial.println(F(
      "Commands:\n"
      "  status                        board, Wi-Fi, clock, buffer and upload state\n"
      "  wifi \"<ssid>\" \"<password>\"    2.4 GHz network; quotes allow spaces\n"
      "  token <nodeId.secret>         board token from the web app (shown once there)\n"
      "  url <https://host>            API base URL\n"
      "  interval <seconds>            measurement interval, 10-3600\n"
      "  channel <code> on|off         e.g. channel soil4 on\n"
      "  cal <soilN> <dry_mV> <wet_mV> soil calibration: probe in air, probe in water\n"
      "  raw                           read every sensor now (for calibration)\n"
      "  insecure on|off               skip TLS certificate check (bench tests only)\n"
      "  reboot"));
}

void printStatus(const Settings& s, const core::RingBuffer<core::Reading>& buffer) {
  const uploader::Status& up = uploader::status();
  Serial.printf("firmware   %s\n", FW_VERSION);
  // Only the node id part: the secret after the dot must not show up in logs or screenshots.
  int dot = s.nodeToken.indexOf('.');
  Serial.printf("token      %s\n", s.nodeToken.isEmpty() ? "(not set)"
                                   : (s.nodeToken.substring(0, dot < 0 ? 8 : dot) + ".***").c_str());
  Serial.printf("api        %s%s\n", s.apiUrl.c_str(), s.insecureTls ? "  (TLS check OFF)" : "");
  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("wifi       %s, %s, %d dBm\n", s.wifiSsid.c_str(), WiFi.localIP().toString().c_str(),
                  WiFi.RSSI());
  } else {
    Serial.printf("wifi       not connected (%s)\n", s.wifiSsid.isEmpty() ? "no network set" : s.wifiSsid.c_str());
  }
  time_t now = time(nullptr);
  char when[24];
  if (now >= core::kMinValidEpoch && core::formatIso8601Utc(now, when, sizeof when)) {
    Serial.printf("clock      %s\n", when);
  } else {
    Serial.println("clock      not synced (no readings until NTP answers)");
  }
  Serial.printf("interval   %u s\n", static_cast<unsigned>(s.intervalSec));
  Serial.print("channels  ");
  for (size_t i = 0; i < core::kChannelCount; i++) {
    if (s.channelEnabled(i)) Serial.printf(" %s", core::kChannels[i].code);
  }
  Serial.println();
  Serial.printf("buffer     %u / %u readings, %u overwritten while full\n",
                static_cast<unsigned>(buffer.size()), static_cast<unsigned>(buffer.capacity()),
                static_cast<unsigned>(buffer.dropped()));
  Serial.printf("upload     last HTTP %d, sent %u, dropped batches %u\n", up.lastHttpCode,
                static_cast<unsigned>(up.sentReadings), static_cast<unsigned>(up.droppedBatches));
}

void printRaw(const Settings& s) {
  for (size_t i = 0; i < core::kChannelCount; i++) {
    const core::ChannelDef& ch = core::kChannels[i];
    if (ch.source == core::Source::Soil) {
      float mv = sensors::soilMillivolts(ch.pin);
      float pct = 0;
      bool ok = core::soilPercent(mv, s.soilCal[i], &pct);
      Serial.printf("%-13s %6.0f mV  -> %s (cal dry %u / wet %u)\n", ch.code, mv,
                    ok ? String(pct, 1).c_str() : "bad calibration", s.soilCal[i].dryMv,
                    s.soilCal[i].wetMv);
    } else {
      float v = 0;
      if (sensors::read(i, s, &v)) {
        Serial.printf("%-13s %.2f\n", ch.code, v);
      } else {
        Serial.printf("%-13s no answer\n", ch.code);
      }
    }
  }
}

bool parseOnOff(const char* arg, bool* out) {
  if (strcmp(arg, "on") == 0) return *out = true, true;
  if (strcmp(arg, "off") == 0) return *out = false, true;
  return false;
}

// Returns true when the Wi-Fi connection must restart.
bool run(char* input, Settings& s, const core::RingBuffer<core::Reading>& buffer) {
  char* argv[4];
  size_t argc = core::splitArgs(input, argv, 4);
  if (argc == 0) return false;
  const char* cmd = argv[0];
  bool on = false;

  if (strcmp(cmd, "help") == 0) {
    printHelp();
  } else if (strcmp(cmd, "status") == 0) {
    printStatus(s, buffer);
  } else if (strcmp(cmd, "raw") == 0) {
    printRaw(s);
  } else if (strcmp(cmd, "wifi") == 0 && (argc == 2 || argc == 3)) {
    s.wifiSsid = argv[1];
    s.wifiPassword = argc == 3 ? argv[2] : "";
    settings::save(s);
    Serial.printf("OK wifi %s\n", s.wifiSsid.c_str());
    return true;
  } else if (strcmp(cmd, "token") == 0 && argc == 2 && strchr(argv[1], '.') != nullptr) {
    s.nodeToken = argv[1];
    settings::save(s);
    Serial.println("OK token");
  } else if (strcmp(cmd, "url") == 0 && argc == 2 &&
             (strncmp(argv[1], "https://", 8) == 0 || strncmp(argv[1], "http://", 7) == 0)) {
    s.apiUrl = argv[1];
    while (s.apiUrl.endsWith("/")) s.apiUrl.remove(s.apiUrl.length() - 1);
    settings::save(s);
    Serial.printf("OK url %s\n", s.apiUrl.c_str());
  } else if (strcmp(cmd, "interval") == 0 && argc == 2) {
    long sec = strtol(argv[1], nullptr, 10);
    if (sec < static_cast<long>(kMinIntervalSec) || sec > static_cast<long>(kMaxIntervalSec)) {
      Serial.println("ERR interval must be 10-3600 seconds");
      return false;
    }
    s.intervalSec = static_cast<uint32_t>(sec);
    settings::save(s);
    Serial.printf("OK interval %ld\n", sec);
  } else if (strcmp(cmd, "channel") == 0 && argc == 3 && parseOnOff(argv[2], &on)) {
    int i = core::channelIndex(argv[1]);
    if (i < 0) {
      Serial.println("ERR unknown channel");
      return false;
    }
    if (on) {
      s.channelMask |= (1u << i);
    } else {
      s.channelMask &= ~(1u << i);
    }
    settings::save(s);
    Serial.printf("OK channel %s %s\n", argv[1], on ? "on" : "off");
  } else if (strcmp(cmd, "cal") == 0 && argc == 4) {
    int i = core::channelIndex(argv[1]);
    long dry = strtol(argv[2], nullptr, 10);
    long wet = strtol(argv[3], nullptr, 10);
    if (i < 0 || core::kChannels[i].source != core::Source::Soil) {
      Serial.println("ERR cal needs a soil channel (soil1-soil4)");
      return false;
    }
    if (wet <= 0 || dry > 3300 || wet >= dry) {
      Serial.println("ERR expected 0 < wet < dry <= 3300 mV");
      return false;
    }
    s.soilCal[i] = {static_cast<uint16_t>(dry), static_cast<uint16_t>(wet)};
    settings::save(s);
    Serial.printf("OK cal %s %ld %ld\n", argv[1], dry, wet);
  } else if (strcmp(cmd, "insecure") == 0 && argc == 2 && parseOnOff(argv[1], &on)) {
    s.insecureTls = on;
    settings::save(s);
    Serial.printf("OK insecure %s\n", on ? "on" : "off");
  } else if (strcmp(cmd, "reboot") == 0) {
    Serial.println("OK reboot");
    Serial.flush();
    ESP.restart();
  } else {
    Serial.println("ERR unknown command or arguments; type help");
  }
  return false;
}
}  // namespace

bool poll(Settings& s, const core::RingBuffer<core::Reading>& buffer) {
  bool wifiChanged = false;
  while (Serial.available() > 0) {
    char c = static_cast<char>(Serial.read());
    if (c == '\r') continue;
    if (c == '\n') {
      line[lineLen] = '\0';
      wifiChanged |= run(line, s, buffer);
      lineLen = 0;
    } else if (lineLen < sizeof line - 1) {
      line[lineLen++] = c;
    }
  }
  return wifiChanged;
}

}  // namespace console
