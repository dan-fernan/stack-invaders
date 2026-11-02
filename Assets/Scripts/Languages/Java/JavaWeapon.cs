using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Java: throws coffee cups. JIT warmup: the first throws are slow, and every
/// consecutive throw raises "heat" so the next one comes sooner, like the JVM
/// optimizing hot code. Stop throwing and the JIT cools back down.
/// </summary>
public class JavaWeapon : MonoBehaviour, IStageReset
{
    [SerializeField] private CoffeeCup cupPrefab;
    [Tooltip("Where cups are thrown from (the developer's throwing hand).")]
    [SerializeField] private Transform throwPoint;
    [Tooltip("Plays the developer's throw animation on each throw.")]
    [SerializeField] private PlayerAnimator animator;

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

    /// <summary>Spring Boot boon: multiplies the heat each throw adds.</summary>
    public float HeatGainMultiplier { get; set; } = 1f;

    /// <summary>Maven boon: chained splashes per cup (0 = none) and their radius.</summary>
    public int ChainCount { get; set; }
    public float ChainRadius { get; set; } = 0.35f;

    /// <summary>Raised only when the visible heat bar changes (filled segments, total).</summary>
    public event Action<int, int> HeatSegmentsChanged;

    private float nextThrowTime;
    private float lastThrowTime = float.NegativeInfinity;
    private int shownSegments = -1;

    private void OnEnable() => PublishHeat(force: true);

    /// <summary>Each stage starts with a cold JIT (pausing and resuming doesn't reset it).</summary>
    public void ResetForStage()
    {
        Heat = 0f;
        nextThrowTime = 0f;
        lastThrowTime = float.NegativeInfinity;
        PublishHeat(force: true);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        bool firing = kb != null && kb.spaceKey.isPressed;

        if (firing)
        {
            // Holding fire keeps the JIT warm, even while waiting out a slow cold throw.
            if (Time.time >= nextThrowTime) Throw();
        }
        else if (Time.time - lastThrowTime > coolDelay)
        {
            Heat = Mathf.Max(0f, Heat - coolRate * Time.deltaTime);
        }

        PublishHeat(force: false);
    }

    private void Throw()
    {
        CoffeeCup cup = Instantiate(cupPrefab, throwPoint.position, Quaternion.identity);
        cup.SetChain(ChainCount, ChainRadius);
        if (animator != null) animator.PlayThrow();
        nextThrowTime = Time.time + Mathf.Lerp(coldCooldown, hotCooldown, Heat);
        lastThrowTime = Time.time;
        Heat = Mathf.Min(1f, Heat + HeatGainMultiplier / throwsToWarm);
    }

    private void PublishHeat(bool force)
    {
        int segments = Mathf.RoundToInt(Heat * heatSegments);
        if (!force && segments == shownSegments) return;
        shownSegments = segments;
        HeatSegmentsChanged?.Invoke(segments, heatSegments);
    }
}
