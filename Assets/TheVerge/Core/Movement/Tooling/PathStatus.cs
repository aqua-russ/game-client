namespace TheVerge.Core.Movement.Tooling
{
    /// <summary>
    /// Tooling / utility-only A* search outcome (for bot and automated tests only; player input has no auto-walk,
    /// CONTROLS.md §1).
    /// </summary>
    public enum PathStatus
    {
        Found = 0,
        OutOfBounds = 1,
        Unreachable = 2,
        LimitReached = 3
    }
}
