using System.Text.RegularExpressions;

namespace Desertod.Domain.Chat;

/// <summary>Check untrusted model output before truncation or publication.</summary>
public static partial class ChaplainSpeech
{
    [GeneratedRegex("(?:^|[\\s,{])['\"]?(?:option|reason|intent|errorCode)['\"]?\\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex ServiceField();

    public static bool IsPlainText([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.TrimStart();
        return !text.StartsWith("```") && text[0] is not ('{' or '[') && !ServiceField().IsMatch(text);
    }
}
