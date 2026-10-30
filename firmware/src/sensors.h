#pragma once

#include "settings.h"

namespace sensors {

void begin();

// Reads one channel into *value (already converted to the metric's unit).
// Returns false when the sensor didn't answer, so nothing is buffered for it.
bool read(size_t channel, const Settings& s, float* value);

// Averaged soil probe voltage, for calibration (`raw` command).
float soilMillivolts(int pin);

}  // namespace sensors
