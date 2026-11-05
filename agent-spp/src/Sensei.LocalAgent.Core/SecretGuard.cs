using System.Text.RegularExpressions;

namespace Sensei.LocalAgent.Core;

public static partial class SecretGuard
{
    // A conservative guard, never a guarantee that arbitrary prose is sanitized.
    [GeneratedRegex(@"(?:sk-or-v1-[a-zA-Z0-9_-]{8,}|gh[pousr]_[a-zA-Z0-9]{16,}|github_pat_[a-zA-Z0-9_]{16,}|AKIA[A-Z0-9]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|(?i:password|api[_-]?key|secret|token)\s*[:=]\s*[^\s]{8,})", RegexOptions.CultureInvariant)]
    private static partial Regex Credentials();
    public static bool Contains(string text) => Credentials().IsMatch(text);
}
