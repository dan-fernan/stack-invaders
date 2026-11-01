using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The player's hearts. Enemy attacks call TakeHit(); the HUD listens to Changed and
/// the GameManager listens to Died. After a hit the ship blinks and is briefly
/// invulnerable, so two bits landing together cost one heart, not two.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHearts = 3;
    [Tooltip("Seconds of invulnerability after taking a hit.")]
    [SerializeField] private float invulnerableTime = 1f;
    [SerializeField] private float blinkInterval = 0.08f;

    public int MaxHearts => maxHearts;
    public int Hearts { get; private set; }
    public bool IsDead => Hearts <= 0;
    public bool IsInvulnerable { get; private set; }

    /// <summary>(current, max)</summary>
    public event Action<int, int> Changed;
    public event Action Died;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        Hearts = maxHearts;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        Changed?.Invoke(Hearts, maxHearts);
    }

    /// <summary>Returns true if the hit landed (not dead, not invulnerable).</summary>
    public bool TakeHit()
    {
        if (IsDead || IsInvulnerable) return false;

        Hearts--;
        Changed?.Invoke(Hearts, maxHearts);

        if (IsDead)
            Died?.Invoke();
        else
            StartCoroutine(InvulnerabilityRoutine());
        return true;
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        IsInvulnerable = true;
        var wait = new WaitForSeconds(blinkInterval);
        for (float t = 0f; t < invulnerableTime; t += blinkInterval)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return wait;
        }
        spriteRenderer.enabled = true;
        IsInvulnerable = false;
    }
}
