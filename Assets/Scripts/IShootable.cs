/// <summary>
/// Anything the player's bullets can destroy (aliens, thrown bits, later bosses/shields).
/// Bullets only need to know this interface, not every concrete enemy type.
/// </summary>
public interface IShootable
{
    void OnShot();
}
