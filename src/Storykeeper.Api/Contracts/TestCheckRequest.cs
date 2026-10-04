using System.Text.Json.Serialization;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record TestCheckRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<CheckDifficulty>))]
    CheckDifficulty Difficulty,
    string? Strength,
    bool SpendSparkleToken,
    bool Risky);
