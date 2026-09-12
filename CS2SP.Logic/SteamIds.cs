namespace CS2SP.Logic;

/// <summary>
/// Steam64 individual accounts (universe 1, type 1, instance 1).
/// CS2 bots often have a non-zero XUID that is not a real player.
/// </summary>
public static class SteamIds
{
    public const ulong MinIndividual = 76561197960265728UL;

    public static bool IsIndividual(ulong id) =>
        id >= MinIndividual && id < MinIndividual + uint.MaxValue;
}
