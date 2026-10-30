#pragma once

#include "core.h"
#include "settings.h"

// Line-based setup console on the USB serial port (115200 baud). The browser install
// page will send the same commands over Web Serial, so there is one provisioning path.
namespace console {

// Reads pending serial input and runs complete lines. Changes are saved to NVS;
// returns true when Wi-Fi settings changed and the connection must restart.
bool poll(Settings& s, const core::RingBuffer<core::Reading>& buffer);

}  // namespace console
