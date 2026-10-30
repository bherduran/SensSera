#pragma once

#include "core.h"
#include "settings.h"

namespace uploader {

struct Status {
  int lastHttpCode = 0;  // 0 = nothing sent yet
  uint32_t lastSuccessMs = 0;
  uint32_t sentReadings = 0;
  uint32_t droppedBatches = 0;
  uint32_t backoffMs = 0;
};

// Sends up to core::kMaxBatch of the oldest buffered readings when one is due.
// Call from loop(); returns quickly when nothing is due.
void tick(const Settings& s, core::RingBuffer<core::Reading>& buffer);

const Status& status();

}  // namespace uploader
