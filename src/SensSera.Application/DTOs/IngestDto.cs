namespace SensSera.Application.DTOs;

/// <param name="Channel">Required when the token belongs to a node (e.g. "soil1"); must be absent for a standalone device.</param>
public record IngestRequest(string Metric, double Value, DateTime RecordedAt, string? Channel = null);

public record IngestBatchRequest(List<IngestRequest> Readings);

/// <summary>
/// Who sent the readings, taken from the verified token: a standalone device or a node (board)
/// whose readings name a channel. Exactly one of <paramref name="DeviceId"/> and <paramref name="NodeId"/> is set.
/// </summary>
public sealed record IngestSource(Guid OrganizationId, Guid? DeviceId, Guid? NodeId);
