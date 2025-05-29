// Worker.cs

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HashSet<string> _targetIcaoAddresses;
    private readonly Dictionary<string, (bool IsAirborne, double? Latitude, double? Longitude)> _previousStates = new Dictionary<string, (bool, double?, double?)>();
    private DateTime _lastApiCall = DateTime.MinValue;
    private const int ApiCallIntervalSeconds = 5; // Adjust as needed

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _targetIcaoAddresses = _configuration.GetSection("MonitoredHelicopters").Get<string[]>()?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        _logger.LogInformation($"Monitoring ICAO Addresses: {string.Join(", ", _targetIcaoAddresses)}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastApiCall).TotalSeconds >= ApiCallIntervalSeconds)
            {
                await GetAndProcessOpenSkyData();
                _lastApiCall = now;
            }

            await Task.Delay(1000, stoppingToken); // Check more frequently than API call
        }
    }

    private async Task GetAndProcessOpenSkyData()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var openSkyApiUrl = _configuration["OpenSkyApiUrl"];
        var openSkyApiKey = _configuration.GetSection("OpenSkyNetwork")["ApiKey"];

        if (!string.IsNullOrEmpty(openSkyApiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{openSkyApiKey}:")));
        }

        try
        {
            var response = await httpClient.GetAsync(openSkyApiUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var openSkyResponse = JsonSerializer.Deserialize<OpenSkyResponse>(json);

            if (openSkyResponse?.states != null)
            {
                foreach (var state in openSkyResponse.states)
                {
                    if (state != null && _targetIcaoAddresses.Contains(state[0]?.ToString()))
                    {
                        string icao = state[0].ToString();
                        bool isAirborne = state[8] == null; // on_ground is null if airborne
                        double? latitude = state[6] as double?;
                        double? longitude = state[5] as double?;

                        if (!_previousStates.ContainsKey(icao))
                        {
                            _previousStates[icao] = (isAirborne, latitude, longitude);
                            if (isAirborne)
                            {
                                await PostToSocialMedia($"Police helicopter ({icao}) spotted in the air at Lat: {latitude:F6}, Lon: {longitude:F6}");
                            }
                        }
                        else
                        {
                            var previousState = _previousStates[icao];
                            if (!previousState.IsAirborne && isAirborne)
                            {
                                await PostToSocialMedia($"Police helicopter ({icao}) has taken off at Lat: {latitude:F6}, Lon: {longitude:F6}");
                            }
                            else if (previousState.IsAirborne && isAirborne && (Math.Abs(previousState.Latitude.GetValueOrDefault() - latitude.GetValueOrDefault()) > 0.0001 || Math.Abs(previousState.Longitude.GetValueOrDefault() - longitude.GetValueOrDefault()) > 0.0001)) // Check for significant movement
                            {
                                await PostToSocialMedia($"Police helicopter ({icao}) location updated to Lat: {latitude:F6}, Lon: {longitude:F6}");
                            }
                            _previousStates[icao] = (isAirborne, latitude, longitude);
                        }
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Error calling OpenSky API: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Error parsing OpenSky API response: {ex.Message}");
        }
    }

    private async Task PostToSocialMedia(string message)
    {
        _logger.LogInformation($"Posting to social media: {message}");

        // *** IMPLEMENT YOUR SOCIAL MEDIA POSTING LOGIC HERE ***
        // You will need to use the API client libraries for Twitter, Facebook, etc.
        // and authenticate using the API keys configured in appsettings.json.

        // Example for Twitter (using a hypothetical TwitterService):
        // var twitterConfig = _configuration.GetSection("Twitter").Get<TwitterConfig>();
        // if (twitterConfig != null)
        // {
        //     try
        //     {
        //         await TwitterService.Tweet(twitterConfig, message);
        //         _logger.LogInformation("Successfully posted to Twitter.");
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError($"Error posting to Twitter: {ex.Message}");
        //     }
        // }

        // Example for Facebook (using a hypothetical FacebookService):
        // var facebookConfig = _configuration.GetSection("Facebook").Get<FacebookConfig>();
        // if (facebookConfig != null)
        // {
        //     try
        //     {
        //         await FacebookService.Post(facebookConfig, message);
        //         _logger.LogInformation("Successfully posted to Facebook.");
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError($"Error posting to Facebook: {ex.Message}");
        //     }
        // }

        await Task.Delay(1000); // Placeholder to prevent rapid retries on social media errors
    }
}

public class OpenSkyResponse
{
    public int time { get; set; }
    public List<object[]> states { get; set; }
}

// Example configuration classes (create these if you use specific social media libraries)
public class TwitterConfig
{
    public string ConsumerKey { get; set; }
    public string ConsumerSecret { get; set; }
    public string AccessToken { get; set; }
    public string AccessTokenSecret { get; set; }
}

public class FacebookConfig
{
    public string AppId { get; set; }
    public string AppSecret { get; set; }
    public string AccessToken { get; set; }
}
