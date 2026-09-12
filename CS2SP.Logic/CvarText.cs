using System.Text;

namespace CS2SP.Logic;

/// <summary>
/// CSS <c>FakeConVar&lt;string&gt;</c> only strips wrapping quotes when the
/// remainder of the command both starts and ends with <c>"</c>. A typical
/// cfg line <c>sp_mod "aim"    // comment</c> therefore stores
/// <c>"aim"    </c> (quotes + padding). Trim and take the quoted token.
/// </summary>
public static class CvarText
{
    public static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var s = value.Trim();
        if (s.Length >= 2 && s[0] == '"')
        {
            var end = s.IndexOf('"', 1);
            if (end > 0)
                s = s[1..end].Trim();
        }

        return s;
    }

    /// <summary>Clean, then drop CR/LF/controls so the value is safe in an HTTP header.</summary>
    public static string CleanHeader(string? value)
    {
        var s = Clean(value);
        if (s.Length == 0)
            return "";

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is >= (char)32 and not (char)127)
                sb.Append(c);
        }

        return sb.ToString().Trim();
    }
}
