#pragma once

// Board-independent logic. Nothing here includes Arduino headers, so it builds and
// runs in the native test environment (`pio test -e native`).

#include <stddef.h>
#include <stdint.h>

#include "ring_buffer.h"

namespace core {

// --- Channels -------------------------------------------------------------------

// Wired sensors, in the order of the PRD pin plan (§5.3). The code is what the board
// sends as "channel"; it must match a channel registered for the board in the web app.
enum class Source : uint8_t { Soil, AirTemp, AirHumidity, Light };

struct ChannelDef {
  const char* code;
  const char* metric;  // API MetricType name
  Source source;
  int8_t pin;  // ADC pin for soil channels, -1 otherwise
};

constexpr size_t kChannelCount = 7;
extern const ChannelDef kChannels[kChannelCount];

// Index into kChannels, or -1 if the code is unknown.
int channelIndex(const char* code);

// --- Readings -------------------------------------------------------------------

struct Reading {
  uint32_t epoch;  // seconds since 1970, UTC
  float value;
  uint8_t channel;  // index into kChannels
};

// Mean of n samples; 0 when n == 0.
float average(const uint32_t* samples, size_t n);

// Capacitive probes read lower when wet. Two calibration points (in air, in water)
// map the probe linearly to 0..100 %, clamped. Returns false for an unusable
// calibration (wet must be below dry).
struct SoilCalibration {
  uint16_t dryMv;
  uint16_t wetMv;
};
bool soilPercent(float mv, SoilCalibration cal, float* out);

// "2026-10-30T11:05:00Z" into buf (needs at least 21 bytes). Returns false if it doesn't fit.
bool formatIso8601Utc(uint32_t epoch, char* buf, size_t len);

// Any time before this means NTP hasn't synced yet (the RTC starts at 1970).
constexpr uint32_t kMinValidEpoch = 1767225600;  // 2026-01-01T00:00:00Z

// --- Upload ---------------------------------------------------------------------

constexpr size_t kMaxBatch = 100;  // IngestBatchRequestValidator.MaxReadings

// JSON body for POST /api/ingest/batch with the first `count` buffered readings.
// Returns the number of bytes written (excluding the terminator), or 0 if buf is too small.
size_t buildBatchJson(const RingBuffer<Reading>& buffer, size_t count, char* buf, size_t len);

enum class UploadOutcome : uint8_t {
  Sent,       // 2xx: drop the readings from the buffer
  DropBatch,  // 400: the server will never accept this batch; drop it so it can't block the queue
  Retry,      // network error, 401/403, 404, 429, 5xx: keep the readings and back off
};

// httpCode <= 0 is a transport error (DNS, TLS, timeout).
UploadOutcome classifyUpload(int httpCode);

constexpr uint32_t kMinBackoffMs = 10000;
constexpr uint32_t kMaxBackoffMs = 300000;

// Exponential backoff: 10 s, 20 s, 40 s ... capped at 5 min. Auth failures go straight
// to the cap since only a person fixing the token can make them succeed.
uint32_t nextBackoffMs(uint32_t currentMs, int httpCode);

// --- Console --------------------------------------------------------------------

// Splits a console line on spaces; "double quotes" keep spaces (Wi-Fi names often
// contain them). Modifies line in place. Returns the number of arguments.
size_t splitArgs(char* line, char** argv, size_t maxArgs);

}  // namespace core
