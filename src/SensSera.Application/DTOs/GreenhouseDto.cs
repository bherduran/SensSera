namespace SensSera.Application.DTOs;

public record GreenhouseRequest(string Name, string Location);

public record GreenhouseResponse(Guid Id, string Name, string? Location, DateTime CreatedAt);