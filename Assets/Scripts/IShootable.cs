/// <summary>
/// Anything the player's bullets can destroy (aliens, thrown bits, later bosses/shields).
/// Bullets only need to know this interface, not every concrete enemy type.
/// </summary>
public interface IShootable
{
    void OnShot();

    /// <summary>
    /// False for things player shots pass straight through (e.g. the recursion boss's
    /// memoized "cache hit" shots, which can only be dodged or parried).
    /// </summary>
    bool CanBeShot => true;
}
