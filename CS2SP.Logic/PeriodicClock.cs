namespace CS2SP.Logic;

/// <summary>
/// Deathmatch-style uploader. Arms on the first tick / after <c>curtime</c>
/// resets on map change; does not fire that tick.
/// </summary>
public sealed class PeriodicClock
{
    public float LastFire { get; private set; } = -1f;

    public void Reset() => LastFire = -1f;

    public bool TryFire(float now, float interval)
    {
        if (interval < 1f)
            interval = 1f;

        if (LastFire < 0f || now < LastFire)
        {
            LastFire = now;
            return false;
        }

        if (now - LastFire < interval)
            return false;

        LastFire = now;
        return true;
    }
}
