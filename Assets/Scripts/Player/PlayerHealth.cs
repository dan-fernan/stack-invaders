using System;
using UnityEngine;

/// <summary>
/// The player's hearts. Enemy projectiles / bosses call TakeHit(); the HUD listens
/// to Changed and the GameManager listens to Died.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHearts = 3;

    public int MaxHearts => maxHearts;
    public int Hearts { get; private set; }
    public bool IsDead => Hearts <= 0;

    /// <summary>(current, max)</summary>
    public event Action<int, int> Changed;
    public event Action Died;

    private void Awake()
    {
        Hearts = maxHearts;
    }

    private void Start()
    {
        Changed?.Invoke(Hearts, maxHearts);
    }

    public void TakeHit()
    {
        if (IsDead) return;

        Hearts--;
        Changed?.Invoke(Hearts, maxHearts);
        if (IsDead) Died?.Invoke();
    }
}
