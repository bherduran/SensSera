#pragma once

#include <stdint.h>

// Injected by scripts/version.py (e.g. "0.1.0+a1b2c3d").
#ifndef FW_VERSION
#define FW_VERSION "0.0.0-dev"
#endif

// PRD §5.3 pin plan (ESP32-S3 DevKitC N16R8). Soil ADC pins live in core::kChannels.
// Each I2C sensor has its own bus: every sensor lead plugs straight into its pin, and a fault
// on one cable can't take the other sensor down.
constexpr uint8_t kPinLightSda = 8;  // BH1750 (0x23)
constexpr uint8_t kPinLightScl = 9;
constexpr uint8_t kPinAirSda = 15;  // AHT25 (0x38)
constexpr uint8_t kPinAirScl = 16;

constexpr uint32_t kLightI2cHz = 100000;
// The bare AHT25 has no pull-up resistors (the BH1750 module does). With only the ESP32's weak
// internal ones (~45 kΩ) a 40 cm lead takes a few µs to rise: marginal at 100 kHz, fine here.
constexpr uint32_t kAirI2cHz = 20000;

constexpr const char* kDefaultApiUrl = "https://senssera-api-wcav7.azurewebsites.net";

constexpr uint32_t kDefaultIntervalSec = 30;
constexpr uint32_t kMinIntervalSec = 10;
constexpr uint32_t kMaxIntervalSec = 3600;

constexpr uint8_t kSamplesPerReading = 10;  // PRD §6: mean of 10 samples per channel

// Defaults until each probe is calibrated (`cal` command). Typical for a v1.2 capacitive
// probe at 3.3 V; real probes differ by a few hundred mV, so calibrate before trusting the %.
constexpr uint16_t kDefaultSoilDryMv = 2800;
constexpr uint16_t kDefaultSoilWetMv = 1200;

// One pot: soil1, air temperature, air humidity and light on; soil2–4 off until probes are added.
// Readings from a channel the server doesn't know are skipped (and flagged on the board card in the
// web app), so only enable what's registered there.
constexpr uint8_t kDefaultChannelMask = 0b1110001;

constexpr uint32_t kUploadIntervalMs = 60000;
constexpr uint32_t kHttpTimeoutMs = 15000;
// A loop iteration that blocks longer than this resets the board (task watchdog).
constexpr uint32_t kWatchdogTimeoutSec = 60;
