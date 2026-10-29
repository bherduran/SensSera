namespace SensSera.Application;

public static class Heartbeat
{
    /// <summary>A device or node silent for longer than this counts as offline.</summary>
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(5);
}
