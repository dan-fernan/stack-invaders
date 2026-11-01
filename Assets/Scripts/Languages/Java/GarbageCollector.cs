using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Java's garbage collector. Like the real JVM, it runs under memory pressure rather
/// than on a fixed clock: it charges for a while, then waits until there is garbage
/// close enough to matter (a hostile bit near the developer) and collects every
/// hostile bit on screen. Collected bits pop and fly home harmlessly, where their
/// bug rebuilds.
/// </summary>
public class GarbageCollector : MonoBehaviour
{
    [Tooltip("Seconds to charge before the GC is ready.")]
    [SerializeField] private float chargeTime = 10f;
    [Tooltip("When ready, the GC runs as soon as a hostile bit gets this close to the developer.")]
    [SerializeField] private float triggerRadius = 3f;

    /// <summary>Whole seconds of charging left; 0 means ready and waiting for garbage.</summary>
    public event Action<int> SecondsLeftChanged;

    /// <summary>Raised after a collection with the number of bits collected.</summary>
    public event Action<int> Collected;

    public bool IsReady => timeLeft <= 0f;

    private float timeLeft;
    private int shownSeconds = -1;

    // Copy of the active set, since collecting a bit modifies it.
    private readonly List<BitProjectile> toCollect = new List<BitProjectile>();

    // Enabled at the start of each wave: the GC starts charging again.
    private void OnEnable()
    {
        timeLeft = chargeTime;
        shownSeconds = -1;
    }

    private void Update()
    {
        if (IsReady)
        {
            if (GarbageNearby()) Collect();
        }
        else
        {
            timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        }

        int seconds = Mathf.CeilToInt(timeLeft);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            SecondsLeftChanged?.Invoke(seconds);
        }
    }

    private bool GarbageNearby()
    {
        Vector2 center = transform.position;
        foreach (BitProjectile bit in BitProjectile.Active)
        {
            if (bit.IsDangerous && Vector2.Distance(bit.transform.position, center) <= triggerRadius)
                return true;
        }
        return false;
    }

    private void Collect()
    {
        toCollect.Clear();
        toCollect.AddRange(BitProjectile.Active);

        int count = 0;
        foreach (BitProjectile bit in toCollect)
            if (bit.Collect()) count++;

        timeLeft = chargeTime;
        Collected?.Invoke(count);
    }
}
