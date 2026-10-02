namespace SensSera.Application.Exceptions;

/// <summary>The request clashes with existing state (e.g. a duplicate email); mapped to 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
