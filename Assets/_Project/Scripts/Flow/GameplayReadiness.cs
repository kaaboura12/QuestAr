/// <summary>
/// The gameplay scene tells the loader when the player can actually see a match.
/// </summary>
public static class GameplayReadiness
{
    public static bool IsReady { get; private set; }

    public static void Reset()
    {
        IsReady = false;
    }

    public static void MarkReady()
    {
        IsReady = true;
    }
}
