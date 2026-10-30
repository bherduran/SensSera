#include "core.h"

#include <stdio.h>
#include <string.h>
#include <time.h>

namespace core {

const ChannelDef kChannels[kChannelCount] = {
    {"soil1", "SoilMoisture", Source::Soil, 4},
    {"soil2", "SoilMoisture", Source::Soil, 5},
    {"soil3", "SoilMoisture", Source::Soil, 6},
    {"soil4", "SoilMoisture", Source::Soil, 7},  // spare probe
    {"air_temp", "Temperature", Source::AirTemp, -1},
    {"air_humidity", "Humidity", Source::AirHumidity, -1},
    {"light", "Light", Source::Light, -1},
};

int channelIndex(const char* code) {
  for (size_t i = 0; i < kChannelCount; i++) {
    if (strcmp(kChannels[i].code, code) == 0) return static_cast<int>(i);
  }
  return -1;
}

float average(const uint32_t* samples, size_t n) {
  if (n == 0) return 0;
  uint64_t sum = 0;
  for (size_t i = 0; i < n; i++) sum += samples[i];
  return static_cast<float>(sum) / static_cast<float>(n);
}

bool soilPercent(float mv, SoilCalibration cal, float* out) {
  if (cal.wetMv >= cal.dryMv) return false;
  float pct = (cal.dryMv - mv) * 100.0f / (cal.dryMv - cal.wetMv);
  if (pct < 0) pct = 0;
  if (pct > 100) pct = 100;
  *out = pct;
  return true;
}

bool formatIso8601Utc(uint32_t epoch, char* buf, size_t len) {
  time_t t = static_cast<time_t>(epoch);
  struct tm tm;
  if (gmtime_r(&t, &tm) == nullptr) return false;
  return strftime(buf, len, "%Y-%m-%dT%H:%M:%SZ", &tm) != 0;
}

size_t buildBatchJson(const RingBuffer<Reading>& buffer, size_t count, char* buf, size_t len) {
  if (count > buffer.size()) count = buffer.size();
  size_t pos = 0;
  // snprintf returns the length it wanted; anything >= the space left means truncation.
  auto advance = [&](int n) {
    if (n < 0 || static_cast<size_t>(n) >= len - pos) return false;
    pos += static_cast<size_t>(n);
    return true;
  };

  if (len == 0 || !advance(snprintf(buf, len, "{\"readings\":["))) return 0;
  for (size_t i = 0; i < count; i++) {
    const Reading& r = buffer.at(i);
    const ChannelDef& ch = kChannels[r.channel];
    char when[24];
    if (!formatIso8601Utc(r.epoch, when, sizeof when)) return 0;
    int n = snprintf(buf + pos, len - pos,
                     "%s{\"metric\":\"%s\",\"value\":%.2f,\"recordedAt\":\"%s\",\"channel\":\"%s\"}",
                     i == 0 ? "" : ",", ch.metric, static_cast<double>(r.value), when, ch.code);
    if (!advance(n)) return 0;
  }
  if (!advance(snprintf(buf + pos, len - pos, "]}"))) return 0;
  return pos;
}

UploadOutcome classifyUpload(int httpCode) {
  if (httpCode >= 200 && httpCode < 300) return UploadOutcome::Sent;
  if (httpCode == 400) return UploadOutcome::DropBatch;
  return UploadOutcome::Retry;
}

uint32_t nextBackoffMs(uint32_t currentMs, int httpCode) {
  if (httpCode == 401 || httpCode == 403) return kMaxBackoffMs;
  if (currentMs < kMinBackoffMs) return kMinBackoffMs;
  uint32_t next = currentMs * 2;
  return next > kMaxBackoffMs ? kMaxBackoffMs : next;
}

size_t splitArgs(char* line, char** argv, size_t maxArgs) {
  size_t argc = 0;
  char* p = line;
  while (*p && argc < maxArgs) {
    while (*p == ' ') p++;
    if (!*p) break;
    if (*p == '"') {
      argv[argc++] = ++p;
      while (*p && *p != '"') p++;
    } else {
      argv[argc++] = p;
      while (*p && *p != ' ') p++;
    }
    if (*p) *p++ = '\0';
  }
  return argc;
}

}  // namespace core
