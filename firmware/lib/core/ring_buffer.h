#pragma once

#include <stddef.h>

// Fixed-capacity FIFO over caller-owned storage, so the storage can live in PSRAM.
// When full, push() overwrites the oldest item: during a long outage the newest
// readings are the ones worth keeping.
namespace core {

template <typename T>
class RingBuffer {
 public:
  RingBuffer(T* storage, size_t capacity) : data_(storage), capacity_(capacity) {}

  // Returns true when the oldest item was dropped to make room.
  bool push(const T& item) {
    if (capacity_ == 0) return true;
    bool dropped = false;
    if (size_ == capacity_) {
      head_ = (head_ + 1) % capacity_;
      size_--;
      dropped_++;
      dropped = true;
    }
    data_[(head_ + size_) % capacity_] = item;
    size_++;
    return dropped;
  }

  // i = 0 is the oldest item. Caller keeps i < size().
  const T& at(size_t i) const { return data_[(head_ + i) % capacity_]; }

  void popFront(size_t n) {
    if (n > size_) n = size_;
    head_ = (head_ + n) % capacity_;
    size_ -= n;
  }

  size_t size() const { return size_; }
  size_t capacity() const { return capacity_; }
  bool empty() const { return size_ == 0; }
  // Items overwritten because the buffer was full, since boot.
  size_t dropped() const { return dropped_; }

 private:
  T* data_;
  size_t capacity_;
  size_t head_ = 0;
  size_t size_ = 0;
  size_t dropped_ = 0;
};

}  // namespace core
