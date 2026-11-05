using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sensei.BuildingBlocks.Ai;

/// <summary>Recognized credential formats only; this is a guard, not proof that text is anonymized.</summary>
public static partial class SecretBoundary
{
    [GeneratedRegex(@"(?:sk-or-v1-[a-zA-Z0-9_-]{8,}|gh[pousr]_[a-zA-Z0-9]{16,}|github_pat_[a-zA-Z0-9_]{16,}|AKIA[A-Z0-9]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialPattern();

    public static bool ContainsRecognizedSecret(string text) => CredentialPattern().IsMatch(text);

    public static bool ContainsSecret(JsonElement value, string? configuredKey = null) => value.ValueKind switch
    {
        JsonValueKind.String => Contains(value.GetString()!, configuredKey),
        JsonValueKind.Object => value.EnumerateObject().Any(p => Contains(p.Name, configuredKey) || ContainsSecret(p.Value, configuredKey)),
        JsonValueKind.Array => value.EnumerateArray().Any(item => ContainsSecret(item, configuredKey)),
        _ => false
    };

    internal static bool Contains(string text, string? configuredKey) => ContainsRecognizedSecret(text) ||
        !string.IsNullOrEmpty(configuredKey) && text.Contains(configuredKey, StringComparison.Ordinal);
}
