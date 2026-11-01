namespace CS2SP.Logic;

public static class MapNames
{
    /// <summary>
    /// Engine map token. Strips a <c>workshop/&lt;id&gt;/</c> prefix so the
    /// site can match <c>de_dust2.jpg</c>.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var name = raw.Trim().Trim('"');
        const string prefix = "workshop/";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = name[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash >= 0 && slash < rest.Length - 1)
                name = rest[(slash + 1)..];
        }
        return name.Trim();
    }

    /// <summary>
    /// Next map from the <c>nextlevel</c> cvar. Empty, placeholder, and
    /// "same as the map already running" are not a destination.
    /// </summary>
    public static string? NextMap(string? raw, string? current)
    {
        var name = Normalize(raw);
        if (name.Length == 0)
            return null;
        if (name.Equals("0", StringComparison.OrdinalIgnoreCase)
            || name.Equals("none", StringComparison.OrdinalIgnoreCase)
            || name.Equals("undefined", StringComparison.OrdinalIgnoreCase))
            return null;
        var currentName = Normalize(current);
        if (currentName.Length > 0 && name.Equals(currentName, StringComparison.OrdinalIgnoreCase))
            return null;
        return name;
    }
}
