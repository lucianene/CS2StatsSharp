namespace CS2SP.Logic;

/// <summary>
/// Coalesced presence POST after joins / warmup. Matches C++
/// <c>SP_RequestPresenceUpload</c>: settle 1s so the controller is in the
/// payload, and share one POST across a join burst (5s since last send).
/// </summary>
public sealed class PresenceClock
{
    public const float Delay = 1f;
    public const float MinInterval = 5f;

    public float LastUpload { get; private set; } = -1f;
    public float DueAt { get; private set; } = -1f;
    public bool Pending { get; private set; }

    public void Reset()
    {
        LastUpload = -1f;
        DueAt = -1f;
        Pending = false;
    }

    public void Request(float now)
    {
        var at = now + Delay;
        if (LastUpload >= 0f)
            at = Math.Max(at, LastUpload + MinInterval);

        Pending = true;
        DueAt = at;
    }

    public bool TryFire(float now)
    {
        if (!Pending || now < DueAt)
            return false;

        Pending = false;
        return true;
    }

    public void NoteUpload(float now) => LastUpload = now;
}
