using UnityEngine;

/// <summary>
/// Boons every language can be offered:
///   compute - Compute Upgrade (vertical scaling: a bigger machine), +1 max heart per level.
/// </summary>
public class UniversalBoons : MonoBehaviour, IBoonReceiver
{
    [SerializeField] private PlayerHealth health;

    public void ApplyBoon(string id, int level)
    {
        switch (id)
        {
            case "compute":
                health.AddMaxHeart();
                break;
            default:
                Debug.LogWarning($"UniversalBoons: unknown boon '{id}'");
                break;
        }
    }
}
