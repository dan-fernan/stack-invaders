using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Java's garbage collector: runs automatically on a timer, like the JVM's GC,
/// and collects every hostile bit on screen. Collected bits pop and fly home
/// harmlessly, where their bug rebuilds.
/// </summary>
public class GarbageCollector : MonoBehaviour
{
    [SerializeField] private float interval = 10f;

    /// <summary>Raised when the whole-second countdown changes.</summary>
    public event Action<int> SecondsLeftChanged;

    /// <summary>Raised after a collection with the number of bits collected.</summary>
    public event Action<int> Collected;

    private float timeLeft;
    private int shownSeconds = -1;

    // Copy of the active set, since collecting a bit modifies it.
    private readonly List<BitProjectile> toCollect = new List<BitProjectile>();

    private void OnEnable()
    {
        timeLeft = interval;
        shownSeconds = -1;
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            Collect();
            timeLeft += interval;
        }

        int seconds = Mathf.CeilToInt(timeLeft);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            SecondsLeftChanged?.Invoke(seconds);
        }
    }

    private void Collect()
    {
        toCollect.Clear();
        toCollect.AddRange(BitProjectile.Active);

        int count = 0;
        foreach (BitProjectile bit in toCollect)
            if (bit.Collect()) count++;

        Collected?.Invoke(count);
    }
}
