#pragma once

#include <stdint.h>

// Injected by scripts/version.py (e.g. "0.1.0+a1b2c3d").
#ifndef FW_VERSION
#define FW_VERSION "0.0.0-dev"
#endif

// PRD §5.3 pin plan (ESP32-S3 DevKitC N16R8). Soil ADC pins live in core::kChannels.
constexpr uint8_t kPinI2cSda = 8;
constexpr uint8_t kPinI2cScl = 9;  // shared by the AHT25 (0x38) and BH1750 (0x23)

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
