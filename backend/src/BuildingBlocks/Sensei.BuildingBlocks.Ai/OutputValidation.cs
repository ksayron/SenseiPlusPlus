using System.Text.Json;
using Sensei.BuildingBlocks.Application.Models;

namespace Sensei.BuildingBlocks.Ai;

internal static class OutputValidation
{
    public static bool Accepts(IModelOutputContract contract, JsonElement output)
    {
        try { return output.ValueKind == JsonValueKind.Object && UniqueNames(output) && contract.Accepts(output); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        { return false; }
    }
    private static bool UniqueNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() ==
            element.EnumerateObject().Count() && element.EnumerateObject().All(p => UniqueNames(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().All(UniqueNames),
        _ => true
    };
}
