//Simulator
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "SIMULATOR_")
    .Build();

var apiBaseUrl = config["ApiBaseUrl"] ?? "http://localhost:5010";
var intervalSeconds = config.GetValue<int>("IntervalSeconds", 5);
var alertMode = args.Contains("--alert-mode");

var devices = config.GetSection("Devices").Get<List<DeviceConfig>>() ?? [];

using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// No tokens configured → provision demo devices through the API (retries while the API boots).
if (devices.Count == 0)
{
    var email = config["Demo:Email"] ?? "admin@demo.com";
    var password = config["Demo:Password"] ?? "Admin1234!";
    for (var attempt = 1; devices.Count == 0 && !cts.Token.IsCancellationRequested; attempt++)
    {
        try
        {
            devices = await DemoBootstrap.RunAsync(http, email, password, cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException && attempt < 30)
        {
            Console.WriteLine($"Demo bootstrap attempt {attempt} failed: {ex.Message} - retrying in 2s");
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token).ContinueWith(_ => { });
        }
    }
}

Console.WriteLine($"Simulator starting - {devices.Count} device(s), interval={intervalSeconds}s, alertMode={alertMode}");

while (!cts.Token.IsCancellationRequested)
{
    var now = DateTime.UtcNow;
    var hour = now.Hour + now.Minute / 60.0;

    foreach (var device in devices)
    {
        var value = GenerateValue(device.Metric, hour, alertMode);

        var payload = new
        {
            metric = device.Metric,
            value,
            RecordedAt = now
        };


        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Device-Token", device.Token);

        try
        {
            var response = await http.SendAsync(request, cts.Token);
            Console.WriteLine($"[{now:HH:mm:ss}] {device.Metric}={value:F2} => {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{now:HH:mm:ss}] Error: {ex.Message}");
        }
    }

    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cts.Token).ContinueWith(_=> {});
}

Console.WriteLine("Simulator stopped");

static double GenerateValue(string metric, double hour, bool alertMode)
{
    var sine = Math.Sin((hour - 6) * Math.PI / 12);
    var noise = Random.Shared.NextDouble() * 2 - 1;

    var value = metric switch
    {
        "Temperature" => 20 + sine * 10 + noise,
        "Humidity" => 60 - sine * 20 + noise,
        "Co2" => 800 + sine * 200 + noise * 10,
        "Light" => Math.Max(0, sine * 50_000 + noise * 1000),
        "SoilMoisture" => 50 + noise * 5,
        "Pressure" => 1013 + noise * 2,
        _          => 0
   };

   if (alertMode && Random.Shared.NextDouble() < 0.1)
        value *= 2;

   return Math.Round(value, 2);     
}

record DeviceConfig(string Token, string Metric);