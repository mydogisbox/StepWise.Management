using Walkthrough.Core;
using static Walkthrough.Core.FieldValues;

namespace StepWise.Management.UI.Tests.Api;

public static class Generators
{
    private static readonly string[] _adjectives =
    [
        "amber", "bold", "calm", "dark", "eager", "fair", "grand", "happy",
        "idle", "jolly", "keen", "lazy", "mild", "neat", "open", "proud",
        "quick", "rapid", "safe", "tall", "urban", "vivid", "warm", "young"
    ];

    private static readonly string[] _nouns =
    [
        "anchor", "bridge", "cloud", "delta", "ember", "forge", "grove", "haven",
        "inlet", "jetty", "knoll", "ledge", "mound", "nexus", "orbit", "prism",
        "quay", "ridge", "shore", "tower", "vale", "wharf", "yield", "zenith"
    ];

    public static IFieldValue<string> RandomName() =>
        Generated(() =>
        {
            var adj  = _adjectives[Random.Shared.Next(_adjectives.Length)];
            var noun = _nouns[Random.Shared.Next(_nouns.Length)];
            var num  = Random.Shared.Next(100, 1000);
            return $"{adj}-{noun}-{num}";
        });
}
