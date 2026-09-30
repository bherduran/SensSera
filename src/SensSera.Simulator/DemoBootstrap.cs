using System.Net.Http.Headers;
using System.Net.Http.Json;

/// <summary>
/// Zero-touch demo setup: when no device tokens are configured, log in as the seeded demo admin,
/// make sure a demo greenhouse with one device + threshold per metric exists, and rotate each
/// device's token to obtain a usable one (raw tokens are only ever returned once by the API).
/// Goes through the public REST API only, so it exercises the same auth and validation as a user.
/// </summary>
static class DemoBootstrap
{
    const string GreenhouseName = "Demo Greenhouse";

    // Sensible greenhouse bands; the simulator's normal curve stays inside them, --alert-mode breaks them.
    static readonly (string Metric, double Min, double Max)[] Metrics =
    [
        ("Temperature", 12, 32),
        ("Humidity", 35, 85),
        ("Co2", 400, 1200),
        ("SoilMoisture", 30, 70),
        ("Light", 0, 60_000),
        ("Pressure", 990, 1035),
    ];

    public static async Task<List<DeviceConfig>> RunAsync(
        HttpClient http, string email, string password, CancellationToken ct)
    {
        var login = await PostAsync<LoginResult>(http, "/api/auth/login", new { email, password }, ct);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        try
        {
            var greenhouses = await GetAsync<List<IdName>>(http, "/api/greenhouses", ct);
            var greenhouseId = greenhouses.FirstOrDefault(g => g.Name == GreenhouseName)?.Id
                ?? (await PostAsync<IdName>(http, "/api/greenhouses", new { name = GreenhouseName, location = "Antalya" }, ct)).Id;

            var devices = await GetAsync<List<DeviceResult>>(http, $"/api/devices/greenhouse/{greenhouseId}", ct);
            var thresholds = await GetAsync<List<ThresholdResult>>(http, $"/api/thresholds/greenhouse/{greenhouseId}", ct);

            var configs = new List<DeviceConfig>();
            foreach (var (metric, min, max) in Metrics)
            {
                var existing = devices.FirstOrDefault(d => Same(d.Metric, metric));
                var token = existing is null
                    ? (await PostAsync<DeviceResult>(http, "/api/devices",
                        new { name = $"{metric} sensor", greenhouseId, metric }, ct)).Token
                    : (await PostAsync<DeviceResult>(http, $"/api/devices/{existing.Id}/rotate-token", null, ct)).Token;

                if (!thresholds.Any(t => Same(t.Metric, metric)))
                    await PostAsync<ThresholdResult>(http, "/api/thresholds",
                        new { greenhouseId, metric, minValue = min, maxValue = max, isEnabled = true }, ct);

                configs.Add(new DeviceConfig(token!, metric));
            }

            Console.WriteLine($"Demo bootstrap ready - greenhouse '{GreenhouseName}', {configs.Count} device(s)");
            return configs;
        }
        finally
        {
            // The ingest loop authenticates per request with X-Device-Token, never the admin JWT.
            http.DefaultRequestHeaders.Authorization = null;
        }
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static async Task<T> GetAsync<T>(HttpClient http, string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await ReadAsync<T>(response, path, ct);
    }

    static async Task<T> PostAsync<T>(HttpClient http, string path, object? body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, body, ct);
        return await ReadAsync<T>(response, path, ct);
    }

    static async Task<T> ReadAsync<T>(HttpResponseMessage response, string path, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"{path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }

    record LoginResult(string AccessToken);
    record IdName(Guid Id, string Name);
    record DeviceResult(Guid Id, string Metric, string? Token);
    record ThresholdResult(Guid Id, string Metric);
}
