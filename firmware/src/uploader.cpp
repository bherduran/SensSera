#include "uploader.h"

#include <HTTPClient.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>

#include "config.h"
#include "root_ca.h"

namespace uploader {

namespace {
Status st;
uint32_t nextAttemptMs = 0;
char body[12 * 1024];  // fits a full batch (checked by a native test)

int post(const Settings& s, const char* json, size_t len) {
  String url = s.apiUrl + "/api/ingest/batch";
  bool tls = url.startsWith("https://");

  // Plain HTTP is only for the local demo network (PRD §6).
  WiFiClient plain;
  WiFiClientSecure secure;
  if (tls) {
    if (s.insecureTls) {
      secure.setInsecure();
    } else {
      secure.setCACert(kRootCaPem);
    }
  }

  HTTPClient http;
  http.setConnectTimeout(kHttpTimeoutMs);
  http.setTimeout(kHttpTimeoutMs);
  if (!http.begin(tls ? static_cast<WiFiClient&>(secure) : plain, url)) return -1;
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Token", s.nodeToken);
  http.addHeader("X-Firmware-Version", FW_VERSION);
  int code = http.POST(reinterpret_cast<uint8_t*>(const_cast<char*>(json)), len);
  http.end();
  return code;
}
}  // namespace

void tick(const Settings& s, core::RingBuffer<core::Reading>& buffer) {
  uint32_t now = millis();
  if (buffer.empty() || s.nodeToken.isEmpty() || WiFi.status() != WL_CONNECTED) return;
  if (static_cast<int32_t>(now - nextAttemptMs) < 0) return;

  size_t count = buffer.size() < core::kMaxBatch ? buffer.size() : core::kMaxBatch;
  size_t len = core::buildBatchJson(buffer, count, body, sizeof body);
  if (len == 0) {
    log_e("Batch of %u readings did not fit the upload buffer", static_cast<unsigned>(count));
    buffer.popFront(count);
    return;
  }

  int code = post(s, body, len);
  st.lastHttpCode = code;

  switch (core::classifyUpload(code)) {
    case core::UploadOutcome::Sent:
      buffer.popFront(count);
      st.sentReadings += count;
      st.lastSuccessMs = now;
      st.backoffMs = 0;
      // More waiting after a long outage: drain it batch by batch, not once a minute.
      nextAttemptMs = buffer.empty() ? now + kUploadIntervalMs : now;
      log_i("Sent %u readings (%u left)", static_cast<unsigned>(count),
            static_cast<unsigned>(buffer.size()));
      break;

    case core::UploadOutcome::DropBatch:
      // Usually a channel that isn't registered for this board in the web app.
      buffer.popFront(count);
      st.droppedBatches++;
      nextAttemptMs = now + kUploadIntervalMs;
      log_e("Server rejected a batch (400); dropped %u readings. Are all enabled channels "
            "registered for this board?", static_cast<unsigned>(count));
      break;

    case core::UploadOutcome::Retry:
      st.backoffMs = core::nextBackoffMs(st.backoffMs, code);
      nextAttemptMs = now + st.backoffMs;
      log_w("Upload failed (%d); retrying in %u s", code, static_cast<unsigned>(st.backoffMs / 1000));
      break;
  }
}

const Status& status() { return st; }

}  // namespace uploader
