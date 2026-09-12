namespace CS2SP.Logic;

public static class UploadUrl
{
    public static bool IsConfigured(string? apiAddress) =>
        !string.IsNullOrWhiteSpace(apiAddress);

    public static string Build(string apiAddress, string? matchId, string? mod, string? gameMode) =>
        apiAddress
        + "?match_id=" + Uri.EscapeDataString(matchId ?? "")
        + "&mod=" + Uri.EscapeDataString(mod ?? "")
        + "&game_mode=" + Uri.EscapeDataString(gameMode ?? "");
}
