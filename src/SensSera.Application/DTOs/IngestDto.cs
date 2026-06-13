namespace SensSera.Application.DTOs;

public record IngestRequest(string Metric, double Value, DateTime RecordedAt);

public record IngestBatchRequest(List<IngestRequest> Readings);
