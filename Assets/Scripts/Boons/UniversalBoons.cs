using UnityEngine;

/// <summary>
/// Boons every language can be offered:
///   vertical   - vertical scaling: a bigger machine, so +1 max heart per level.
///   horizontal - horizontal scaling: more instances, so one shadow clone per level.
/// </summary>
public class UniversalBoons : MonoBehaviour, IBoonReceiver
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private CloneManager clones;

    public void ApplyBoon(string id, int level)
    {
        switch (id)
        {
            case "vertical":
                health.AddMaxHeart();
                break;
            case "horizontal":
                clones.SetCount(level);
                break;
            default:
                Debug.LogWarning($"UniversalBoons: unknown boon '{id}'");
                break;
        }
    }
}
