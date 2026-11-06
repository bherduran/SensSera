namespace SensSera.Application.DTOs;

/// <param name="Channel">Required when the token belongs to a node (e.g. "soil1"); must be absent for a standalone device.</param>
public record IngestRequest(string Metric, double Value, DateTime RecordedAt, string? Channel = null);

public record IngestBatchRequest(List<IngestRequest> Readings);

/// <summary>
/// Who sent the readings, taken from the verified token: a standalone device or a node (board)
/// whose readings name a channel. Exactly one of <paramref name="DeviceId"/> and <paramref name="NodeId"/> is set.
/// <paramref name="FirmwareVersion"/> is what a node reported in <c>X-Firmware-Version</c>, already validated.
/// </summary>
public sealed record IngestSource(Guid OrganizationId, Guid? DeviceId, Guid? NodeId, string? FirmwareVersion = null);

/// <param name="Index">Position of the reading in the request's <c>readings</c> array.</param>
public record RejectedReading(int Index, string Reason);

/// <summary>
/// A batch stores every usable reading and lists the rest: a board that kept retrying a batch with
/// one bad reading would never drain its buffer, and one that dropped it would lose the good ones.
/// </summary>
public record IngestBatchResponse(int Accepted, IReadOnlyList<RejectedReading> Rejected);
