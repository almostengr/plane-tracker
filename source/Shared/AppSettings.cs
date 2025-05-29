namespace Almostengr.PlaneTracker.Features.Common.Shared;

public sealed class AppSettings
{
    public List<Aircraft> Aircrafts { get; init; } = new();
    public BlueSkyConfig BlueSky { get; init; } = new();
    public int InAirInterval { get; init; } = 5;
    public int OnGroundInterval { get; init; } = 15;
    public TwitterConfig Twitter { get; init; } = new();

    public class Aircraft
    {
        public string? TailNumber { get; init; } = null;
    }

    public class TwitterConfig
    {
        public string? ConsumerKey { get; set; } = null;
        public string? ConsumerSecret { get; set; } = null;
        public string? AccessToken { get; set; } = null;
        public string? AccessTokenSecret { get; set; } = null;
    }

    public class BlueSkyConfig
    {
        public string? AccessToken { get; init; } = null;
        public string? AccessTokenSecret { get; init; } = null;
    }
}
