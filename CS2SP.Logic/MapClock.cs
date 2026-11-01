namespace CS2SP.Logic;

/// <summary>
/// Map timer from <c>mp_timelimit</c> (minutes) and the engine match clock.
/// Warmup (current time still before game start) has no countdown yet.
/// </summary>
public static class MapClock
{
    public static int? FullMapSeconds(float timelimitMinutes)
    {
        if (timelimitMinutes <= 0)
            return null;
        var seconds = (int)Math.Floor(timelimitMinutes * 60.0);
        return seconds > 0 ? seconds : null;
    }

    public static int? TimeleftSeconds(float timelimitMinutes, float gameStartTime, float currentTime)
    {
        var full = FullMapSeconds(timelimitMinutes);
        if (full is null || gameStartTime <= 0 || currentTime <= 0)
            return null;
        if (currentTime < gameStartTime)
            return null;
        var left = (int)Math.Floor(full.Value - (currentTime - gameStartTime));
        return left < 0 ? 0 : left;
    }
}
