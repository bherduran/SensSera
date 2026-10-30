#include <string.h>
#include <unity.h>

#include "core.h"

using namespace core;

void setUp() {}
void tearDown() {}

static void ringBuffer_WhenFull_DropsOldest() {
  int storage[3];
  core::RingBuffer<int> rb(storage, 3);
  TEST_ASSERT_FALSE(rb.push(1));
  TEST_ASSERT_FALSE(rb.push(2));
  TEST_ASSERT_FALSE(rb.push(3));
  TEST_ASSERT_TRUE(rb.push(4));

  TEST_ASSERT_EQUAL(3, rb.size());
  TEST_ASSERT_EQUAL(1, rb.dropped());
  TEST_ASSERT_EQUAL(2, rb.at(0));
  TEST_ASSERT_EQUAL(4, rb.at(2));
}

static void ringBuffer_PopFront_WrapsAndClampsToSize() {
  int storage[3];
  core::RingBuffer<int> rb(storage, 3);
  for (int i = 1; i <= 5; i++) rb.push(i);  // holds 3,4,5 with head wrapped

  rb.popFront(2);
  TEST_ASSERT_EQUAL(1, rb.size());
  TEST_ASSERT_EQUAL(5, rb.at(0));

  rb.popFront(10);
  TEST_ASSERT_TRUE(rb.empty());
  rb.push(6);
  TEST_ASSERT_EQUAL(6, rb.at(0));
}

static void average_OfSamples_IsMean() {
  uint32_t s[] = {1000, 1002, 1004, 1006};
  TEST_ASSERT_EQUAL_FLOAT(1003.0f, average(s, 4));
  TEST_ASSERT_EQUAL_FLOAT(0.0f, average(s, 0));
}

static void soilPercent_MapsBetweenCalibrationPoints() {
  SoilCalibration cal{2800, 1200};
  float pct;
  TEST_ASSERT_TRUE(soilPercent(2000, cal, &pct));
  TEST_ASSERT_EQUAL_FLOAT(50.0f, pct);

  TEST_ASSERT_TRUE(soilPercent(3100, cal, &pct));  // drier than "in air"
  TEST_ASSERT_EQUAL_FLOAT(0.0f, pct);
  TEST_ASSERT_TRUE(soilPercent(900, cal, &pct));  // wetter than "in water"
  TEST_ASSERT_EQUAL_FLOAT(100.0f, pct);
}

static void soilPercent_WithInvertedCalibration_Fails() {
  float pct;
  TEST_ASSERT_FALSE(soilPercent(2000, SoilCalibration{1200, 2800}, &pct));
  TEST_ASSERT_FALSE(soilPercent(2000, SoilCalibration{1500, 1500}, &pct));
}

static void formatIso8601Utc_FormatsAsUtc() {
  char buf[24];
  TEST_ASSERT_TRUE(formatIso8601Utc(1793359500, buf, sizeof buf));
  TEST_ASSERT_EQUAL_STRING("2026-10-30T11:25:00Z", buf);
  TEST_ASSERT_FALSE(formatIso8601Utc(1793359500, buf, 10));
}

static void buildBatchJson_MatchesIngestContract() {
  Reading storage[4];
  RingBuffer<Reading> rb(storage, 4);
  rb.push({1793359500, 41.5f, 0});                      // soil1
  rb.push({1793359500, 22.25f, static_cast<uint8_t>(channelIndex("air_temp"))});
  rb.push({1793359530, 40.0f, 0});

  char buf[512];
  size_t n = buildBatchJson(rb, 2, buf, sizeof buf);

  TEST_ASSERT_EQUAL_STRING(
      "{\"readings\":["
      "{\"metric\":\"SoilMoisture\",\"value\":41.50,\"recordedAt\":\"2026-10-30T11:25:00Z\",\"channel\":\"soil1\"},"
      "{\"metric\":\"Temperature\",\"value\":22.25,\"recordedAt\":\"2026-10-30T11:25:00Z\",\"channel\":\"air_temp\"}"
      "]}",
      buf);
  TEST_ASSERT_EQUAL(strlen(buf), n);
}

static void buildBatchJson_WhenBufferTooSmall_ReturnsZero() {
  Reading storage[2];
  RingBuffer<Reading> rb(storage, 2);
  rb.push({1793359500, 41.5f, 0});
  char buf[40];
  TEST_ASSERT_EQUAL(0, buildBatchJson(rb, 1, buf, sizeof buf));
}

static void buildBatchJson_FullBatchFitsUploadBuffer() {
  static Reading storage[kMaxBatch];
  RingBuffer<Reading> rb(storage, kMaxBatch);
  for (size_t i = 0; i < kMaxBatch; i++) {
    rb.push({1793359500, 99999.99f, static_cast<uint8_t>(channelIndex("air_humidity"))});
  }
  static char buf[12 * 1024];  // same size as the uploader's buffer
  TEST_ASSERT_GREATER_THAN(0, buildBatchJson(rb, kMaxBatch, buf, sizeof buf));
}

static void classifyUpload_SeparatesPoisonFromTransientFailures() {
  TEST_ASSERT_EQUAL(UploadOutcome::Sent, classifyUpload(204));
  TEST_ASSERT_EQUAL(UploadOutcome::DropBatch, classifyUpload(400));
  TEST_ASSERT_EQUAL(UploadOutcome::Retry, classifyUpload(-1));
  TEST_ASSERT_EQUAL(UploadOutcome::Retry, classifyUpload(401));
  TEST_ASSERT_EQUAL(UploadOutcome::Retry, classifyUpload(404));
  TEST_ASSERT_EQUAL(UploadOutcome::Retry, classifyUpload(429));
  TEST_ASSERT_EQUAL(UploadOutcome::Retry, classifyUpload(503));
}

static void nextBackoffMs_DoublesUpToCap() {
  TEST_ASSERT_EQUAL(kMinBackoffMs, nextBackoffMs(0, 503));
  TEST_ASSERT_EQUAL(2 * kMinBackoffMs, nextBackoffMs(kMinBackoffMs, 503));
  TEST_ASSERT_EQUAL(kMaxBackoffMs, nextBackoffMs(200000, -1));
  TEST_ASSERT_EQUAL(kMaxBackoffMs, nextBackoffMs(0, 401));
}

static void splitArgs_KeepsQuotedSpaces() {
  char line[] = "wifi \"Ev Agi 2.4\"  s3cret";
  char* argv[4];
  size_t argc = splitArgs(line, argv, 4);
  TEST_ASSERT_EQUAL(3, argc);
  TEST_ASSERT_EQUAL_STRING("wifi", argv[0]);
  TEST_ASSERT_EQUAL_STRING("Ev Agi 2.4", argv[1]);
  TEST_ASSERT_EQUAL_STRING("s3cret", argv[2]);
}

static void channelIndex_KnownAndUnknown() {
  TEST_ASSERT_EQUAL(0, channelIndex("soil1"));
  TEST_ASSERT_EQUAL(6, channelIndex("light"));
  TEST_ASSERT_EQUAL(-1, channelIndex("pump1"));
}

int main() {
  UNITY_BEGIN();
  RUN_TEST(ringBuffer_WhenFull_DropsOldest);
  RUN_TEST(ringBuffer_PopFront_WrapsAndClampsToSize);
  RUN_TEST(average_OfSamples_IsMean);
  RUN_TEST(soilPercent_MapsBetweenCalibrationPoints);
  RUN_TEST(soilPercent_WithInvertedCalibration_Fails);
  RUN_TEST(formatIso8601Utc_FormatsAsUtc);
  RUN_TEST(buildBatchJson_MatchesIngestContract);
  RUN_TEST(buildBatchJson_WhenBufferTooSmall_ReturnsZero);
  RUN_TEST(buildBatchJson_FullBatchFitsUploadBuffer);
  RUN_TEST(classifyUpload_SeparatesPoisonFromTransientFailures);
  RUN_TEST(nextBackoffMs_DoublesUpToCap);
  RUN_TEST(splitArgs_KeepsQuotedSpaces);
  RUN_TEST(channelIndex_KnownAndUnknown);
  return UNITY_END();
}
