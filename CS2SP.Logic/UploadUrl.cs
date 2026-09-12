namespace CS2SP.Logic;

public static class UploadUrl
{
    public static bool IsConfigured(string? apiAddress) =>
        !string.IsNullOrWhiteSpace(CvarText.Clean(apiAddress));

    public static string Build(string apiAddress, string? matchId, string? mod, string? gameMode)
    {
        var baseUrl = CvarText.Clean(apiAddress);
        var sep = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return baseUrl
            + sep + "match_id=" + Uri.EscapeDataString(CvarText.Clean(matchId))
            + "&mod=" + Uri.EscapeDataString(CvarText.Clean(mod))
            + "&game_mode=" + Uri.EscapeDataString(CvarText.Clean(gameMode));
    }
}
