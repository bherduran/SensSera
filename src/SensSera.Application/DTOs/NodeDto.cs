using SensSera.Domain.Enums;

namespace SensSera.Application.DTOs;

public record NodeRequest(string Name, Guid GreenhouseId);

public record NodeChannelRequest(string Channel, string Name, MetricType Metric);

public record NodeChannelResponse(
    Guid DeviceId,
    string Channel,
    string Name,
    MetricType Metric,
    DateTime? LastSeenAt);

public record NodeResponse(
    Guid Id,
    string Name,
    Guid GreenhouseId,
    string? FirmwareVersion,
    DateTime? LastSeenAt,
    DateTime CreatedAt,
    List<NodeChannelResponse> Channels);

// Returned only on create and rotate: the raw token is never stored, so this is the one chance to copy it.
public record NodeWithTokenResponse(
    Guid Id,
    string Name,
    Guid GreenhouseId,
    string Token);
