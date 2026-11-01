using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Java: throws coffee cups. JIT warmup: the first throws are slow, and every
/// consecutive throw raises "heat" so the next one comes sooner, like the JVM
/// optimizing hot code. Stop throwing and the JIT cools back down.
/// </summary>
public class JavaWeapon : MonoBehaviour
{
    [SerializeField] private CoffeeCup cupPrefab;
    [Tooltip("Where cups are thrown from (the held mug).")]
    [SerializeField] private Transform throwPoint;

    [Header("JIT warmup")]
    [Tooltip("Seconds between throws when cold.")]
    [SerializeField] private float coldCooldown = 0.8f;
    [Tooltip("Seconds between throws when fully warmed up.")]
    [SerializeField] private float hotCooldown = 0.25f;
    [Tooltip("Consecutive throws needed to go from cold to fully hot.")]
    [SerializeField] private int throwsToWarm = 6;
    [Tooltip("Idle seconds after the last throw before the JIT starts cooling.")]
    [SerializeField] private float coolDelay = 0.6f;
    [Tooltip("Heat lost per second while cooling (heat ranges from 0 to 1).")]
    [SerializeField] private float coolRate = 0.7f;
    [Tooltip("Number of segments in the HUD heat bar.")]
    [SerializeField] private int heatSegments = 10;

    /// <summary>0 = cold, 1 = fully optimized.</summary>
    public float Heat { get; private set; }

    /// <summary>Raised only when the visible heat bar changes (filled segments, total).</summary>
    public event Action<int, int> HeatSegmentsChanged;

    private float nextThrowTime;
    private float lastThrowTime = float.NegativeInfinity;
    private int shownSegments = -1;

    private void OnEnable() => PublishHeat(force: true);

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        bool firing = kb != null && kb.spaceKey.isPressed;

        if (firing && Time.time >= nextThrowTime)
            Throw();
        else if (Time.time - lastThrowTime > coolDelay)
            Heat = Mathf.Max(0f, Heat - coolRate * Time.deltaTime);

        PublishHeat(force: false);
    }

    private void Throw()
    {
        Instantiate(cupPrefab, throwPoint.position, Quaternion.identity);
        nextThrowTime = Time.time + Mathf.Lerp(coldCooldown, hotCooldown, Heat);
        lastThrowTime = Time.time;
        Heat = Mathf.Min(1f, Heat + 1f / throwsToWarm);
    }

    private void PublishHeat(bool force)
    {
        int segments = Mathf.RoundToInt(Heat * heatSegments);
        if (!force && segments == shownSegments) return;
        shownSegments = segments;
        HeatSegmentsChanged?.Invoke(segments, heatSegments);
    }
}
