using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Miniboss: recurse(3). The root keeps calling itself, and so do its clones, growing a
/// call tree down toward the player:
///   - Each frame's first call lands directly below it, so a column of clones shields
///     the root; side calls branch outward.
///   - Destroying a frame unwinds its whole subtree, one depth level at a time.
///     Destroying the root (12 HP) unwinds everything and wins the fight.
///   - Growing past the stack limit throws StackOverflowError: the boss freezes for a
///     few seconds, then its deep frames unwind.
///   - Frames fire SplitShots whose split count equals their N (the recursion depth left).
///   - The root fires a telegraphed stack-trace beam, more often as it loses HP.
///   - Unwound frames "return" their N: the digit falls as a projectile (return-value rain).
///     Frames lost to a stack overflow crashed, so they return nothing.
///   - Below half HP, clones make telegraphed tail-call dives at the developer.
/// </summary>
public class RecursionBoss : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private BossFrame framePrefab;
    [SerializeField] private Sprite rootSprite;
    [SerializeField] private Sprite cloneSprite;
    [Tooltip("Digit sprites 0-9 for the N label.")]
    [SerializeField] private Sprite[] digits;
    [SerializeField] private Color rootColor = new Color32(0x58, 0xa6, 0xff, 0xff);
    [SerializeField] private Color cloneColor = new Color32(0x38, 0x8b, 0xfd, 0xff);
    [Tooltip("The base case, recurse(0), looks slightly different.")]
    [SerializeField] private Color baseCaseColor = new Color32(0xe3, 0xb3, 0x41, 0xff);
    [SerializeField] private Color digitColor = new Color32(0x0d, 0x11, 0x17, 0xff);
    [SerializeField] private OneShotAnimation unwindEffect;

    [Header("Recursion")]
    [Tooltip("The root is recurse(maxDepth); recurse(0) frames can't call further.")]
    [SerializeField] private int maxDepth = 3;
    [SerializeField] private int rootHp = 40;
    [SerializeField] private int cloneHp = 1;
    [Tooltip("Seconds between recursive calls (new clones).")]
    [SerializeField] private float callInterval = 1.6f;
    [Tooltip("More frames than this throws StackOverflowError.")]
    [SerializeField] private int stackLimit = 12;
    [SerializeField] private float overflowStunTime = 4f;
    [Tooltip("After an overflow, frames at this depth or deeper unwind.")]
    [SerializeField] private int overflowUnwindDepth = 2;
    [Tooltip("Vertical distance between depth levels (0.875 = 14 pixels, keeps frames on the pixel grid).")]
    [SerializeField] private float rowStep = 0.875f;
    [Tooltip("Sideways distance of a side call at each depth (index = child depth).")]
    [SerializeField] private float[] sideStep = { 0f, 3f, 1.5f, 0.75f };
    [Tooltip("Delay between unwinding one depth level and the next.")]
    [SerializeField] private float unwindStepTime = 0.08f;

    [Header("Attack")]
    [SerializeField] private SplitShot shotPrefab;
    [Tooltip("Seconds between shots at full root HP / near death (hitting it makes it angrier).")]
    [SerializeField] private Vector2 fireIntervalRange = new Vector2(1.5f, 0.7f);
    [Tooltip("Chance the root fires a given shot itself (its shots split the most).")]
    [Range(0f, 1f)] [SerializeField] private float rootFireChance = 0.35f;

    [Header("Stack-trace beam")]
    [SerializeField] private StackTraceBeam beamPrefab;
    [Tooltip("Seconds between beams at full root HP / near death.")]
    [SerializeField] private Vector2 beamIntervalRange = new Vector2(9f, 4f);

    [Header("Return-value rain")]
    [Tooltip("Return values fall at this fraction of a split shot's speed.")]
    [SerializeField] private float returnValueSpeedScale = 0.75f;

    [Header("Tail-call dive")]
    [Tooltip("Dives start once the root's HP fraction drops to this.")]
    [Range(0f, 1f)] [SerializeField] private float diveHpThreshold = 0.5f;
    [Tooltip("Seconds between dives just below the threshold / near death.")]
    [SerializeField] private Vector2 diveIntervalRange = new Vector2(6f, 3f);
    [SerializeField] private float diveTelegraphTime = 0.6f;
    [SerializeField] private float diveSpeed = 9f;
    [SerializeField] private float diveReturnSpeed = 8f;
    [SerializeField] private Sprite telegraphDot;
    [SerializeField] private int telegraphDots = 8;

    [Header("Movement")]
    [SerializeField] private float driftAmplitude = 1.5f;
    [SerializeField] private float driftPeriod = 8f;
    [SerializeField] private int pixelsPerUnit = 16;

    /// <summary>(current, max) root HP.</summary>
    public event Action<int, int> RootHpChanged;
    /// <summary>Raised when StackOverflowError starts (true) and ends (false).</summary>
    public event Action<bool> OverflowChanged;
    public event Action Defeated;

    public BossFrame Root { get; private set; }
    public int FrameCount => frames.Count;
    public bool IsStunned { get; private set; }

    private const int SlotBelow = 0, SlotSide = 1, SlotLeft = 2, SlotRight = 3;

    private readonly List<BossFrame> frames = new List<BossFrame>();
    private readonly List<(BossFrame parent, int slot)> openSlots = new List<(BossFrame, int)>();
    private float callTimer;
    private float fireTimer;
    private float beamTimer;
    private float diveTimer;
    private Transform target;
    private Camera cam;
    private float driftTime;
    private float baseX;
    private bool defeated;

    private void Awake()
    {
        cam = Camera.main;
        baseX = transform.position.x;
        Root = SpawnFrame(null, 0, Vector3.zero);
        fireTimer = fireIntervalRange.x;
        beamTimer = beamIntervalRange.x;
        diveTimer = diveIntervalRange.x;
    }

    /// <summary>The developer, for aiming dives.</summary>
    public void SetTarget(Transform player) => target = player;

    /// <summary>0 at full root HP, 1 when nearly dead. Attacks speed up with it.</summary>
    private float Anger => 1f - (float)Root.Hp / Root.MaxHp;

    private void Start() => RootHpChanged?.Invoke(Root.Hp, Root.MaxHp);

    private void Update()
    {
        if (defeated || IsStunned) return;

        Drift();

        callTimer += Time.deltaTime;
        if (callTimer >= callInterval)
        {
            callTimer -= callInterval;
            Call();
        }

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            Fire();
            fireTimer = Mathf.Lerp(fireIntervalRange.x, fireIntervalRange.y, Anger);
        }

        beamTimer -= Time.deltaTime;
        if (beamTimer <= 0f)
        {
            FireBeam();
            beamTimer = Mathf.Lerp(beamIntervalRange.x, beamIntervalRange.y, Anger);
        }

        if (Anger >= 1f - diveHpThreshold && target != null)
        {
            diveTimer -= Time.deltaTime;
            if (diveTimer <= 0f)
            {
                StartCoroutine(TailCallDive());
                float t = Mathf.InverseLerp(1f - diveHpThreshold, 1f, Anger);
                diveTimer = Mathf.Lerp(diveIntervalRange.x, diveIntervalRange.y, t);
            }
        }
    }

    private void Drift()
    {
        driftTime += Time.deltaTime;
        float x = driftAmplitude * Mathf.Sin(driftTime * 2f * Mathf.PI / driftPeriod);
        x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit; // stay on the pixel grid
        Vector3 p = transform.position;
        transform.position = new Vector3(baseX + x, p.y, p.z);
    }

    // ---------- Recursion ----------

    /// <summary>A random frame makes a recursive call into one of its free slots.</summary>
    private void Call()
    {
        openSlots.Clear();
        foreach (BossFrame frame in frames)
        {
            if (frame.IsDead || frame.N == 0) continue; // base case: no more calls
            foreach (int slot in SlotsOf(frame))
                if (!frame.UsedSlots.Contains(slot))
                    openSlots.Add((frame, slot));
        }
        if (openSlots.Count == 0) return;

        if (frames.Count + 1 > stackLimit)
        {
            StartCoroutine(StackOverflow());
            return;
        }

        (BossFrame parent, int chosen) = openSlots[UnityEngine.Random.Range(0, openSlots.Count)];
        parent.UsedSlots.Add(chosen);
        SpawnFrame(parent, chosen, SlotPosition(parent, chosen));
    }

    private static IEnumerable<int> SlotsOf(BossFrame frame)
    {
        yield return SlotBelow;
        if (frame.IsRoot)
        {
            yield return SlotLeft;
            yield return SlotRight;
        }
        else if (frame.Direction != 0)
        {
            yield return SlotSide;
        }
    }

    private Vector3 SlotPosition(BossFrame parent, int slot)
    {
        int depth = parent.Depth + 1;
        float x = parent.transform.localPosition.x;
        switch (slot)
        {
            case SlotLeft: x -= sideStep[depth]; break;
            case SlotRight: x += sideStep[depth]; break;
            case SlotSide: x += parent.Direction * sideStep[depth]; break;
        }
        return new Vector3(x, -depth * rowStep, 0f);
    }

    private static int DirectionFor(BossFrame parent, int slot)
    {
        if (slot == SlotLeft) return -1;
        if (slot == SlotRight) return 1;
        return parent.Direction;
    }

    private BossFrame SpawnFrame(BossFrame parent, int slot, Vector3 localPosition)
    {
        BossFrame frame = Instantiate(framePrefab, transform);
        frame.transform.localPosition = localPosition;
        frame.SlotLocalPosition = localPosition;

        int depth = parent == null ? 0 : parent.Depth + 1;
        int n = maxDepth - depth;
        bool root = parent == null;
        Color color = root ? rootColor : n == 0 ? baseCaseColor : cloneColor;
        frame.Init(this, parent, depth, n, root ? 0 : DirectionFor(parent, slot), root ? rootHp : cloneHp,
                   root ? rootSprite : cloneSprite, digits[n], color, digitColor);
        frame.SlotInParent = slot;
        frames.Add(frame);
        return frame;
    }

    // ---------- Damage & unwinding ----------

    public void OnFrameDamaged(BossFrame frame)
    {
        if (frame.IsRoot) RootHpChanged?.Invoke(Mathf.Max(0, frame.Hp), frame.MaxHp);
    }

    /// <summary>Destroys a frame and every call it made, deepest levels last.</summary>
    /// <param name="returnValues">Each unwound frame drops its N (false for crashed frames).</param>
    public void Unwind(BossFrame frame, bool returnValues = true)
    {
        // Gather the subtree by depth level before anything is removed.
        var levels = new List<List<BossFrame>>();
        var current = new List<BossFrame> { frame };
        while (current.Count > 0)
        {
            levels.Add(current);
            var next = new List<BossFrame>();
            foreach (BossFrame f in current) next.AddRange(f.Children);
            current = next;
        }

        // Free the slot this frame occupied so its parent can call again later.
        if (frame.Parent != null) frame.Parent.UsedSlots.Remove(frame.SlotInParent);

        foreach (List<BossFrame> level in levels)
            foreach (BossFrame f in level)
            {
                f.MarkDead(); // stops blocking shots immediately
                frames.Remove(f);
            }

        bool rootDown = frame.IsRoot;
        if (rootDown) defeated = true;
        StartCoroutine(PopLevels(levels, rootDown, returnValues && !rootDown));
    }

    private IEnumerator PopLevels(List<List<BossFrame>> levels, bool rootDown, bool returnValues)
    {
        var wait = new WaitForSeconds(unwindStepTime);
        foreach (List<BossFrame> level in levels)
        {
            foreach (BossFrame f in level)
            {
                if (unwindEffect != null) unwindEffect.Spawn(f.transform.position, f.Color);
                if (returnValues) ReturnValue(f);
                Destroy(f.gameObject);
            }
            yield return wait;
        }
        if (rootDown) Defeated?.Invoke();
    }

    private IEnumerator StackOverflow()
    {
        IsStunned = true;
        OverflowChanged?.Invoke(true);
        foreach (BossFrame f in frames) f.Flash(overflowStunTime);

        yield return new WaitForSeconds(overflowStunTime);

        // The crash clears the deep part of the stack.
        var deep = new List<BossFrame>();
        foreach (BossFrame f in frames)
            if (f.Depth == overflowUnwindDepth) deep.Add(f);
        foreach (BossFrame f in deep)
            if (!f.IsDead) Unwind(f, returnValues: false); // crashed calls never return

        IsStunned = false;
        callTimer = 0f;
        OverflowChanged?.Invoke(false);
    }

    // ---------- Attacks ----------

    /// <summary>An unwound frame "returns" its N: the digit falls straight down.</summary>
    private void ReturnValue(BossFrame frame)
    {
        SplitShot value = Instantiate(shotPrefab, frame.transform.position, Quaternion.identity);
        value.Launch(frame, 0, Vector2.down, frame.Color, digits[frame.N], returnValueSpeedScale);
    }

    /// <summary>The root marks its column, then fires a stack-trace beam down it.</summary>
    private void FireBeam()
    {
        if (beamPrefab == null || Root == null || Root.IsDead) return;
        float top = Root.transform.position.y - Root.GetComponent<SpriteRenderer>().bounds.extents.y;
        float bottom = cam.transform.position.y - cam.orthographicSize;
        StackTraceBeam beam = Instantiate(beamPrefab);
        beam.Fire(Root.transform.position.x, top, bottom);
    }

    /// <summary>A clone marks a dotted path to the developer, then dives along it.</summary>
    private IEnumerator TailCallDive()
    {
        var candidates = new List<BossFrame>();
        foreach (BossFrame f in frames)
            if (!f.IsRoot && !f.IsDead && !f.IsDiving) candidates.Add(f);
        if (candidates.Count == 0) yield break;

        BossFrame diver = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        Vector2 aim = target.position;

        // Telegraph: dotted line from the clone to where the developer was.
        var dots = new List<SpriteRenderer>();
        for (int i = 1; i <= telegraphDots; i++)
        {
            var dot = new GameObject("TailCallDot").AddComponent<SpriteRenderer>();
            dot.sprite = telegraphDot;
            dot.color = diver.Color;
            dot.sortingOrder = 3;
            dot.transform.position = Vector2.Lerp(diver.transform.position, aim, i / (float)(telegraphDots + 1));
            dots.Add(dot);
        }
        diver.Flash(diveTelegraphTime);
        for (float t = 0f; t < diveTelegraphTime; t += 0.1f)
        {
            foreach (SpriteRenderer dot in dots) dot.enabled = !dot.enabled;
            yield return new WaitForSeconds(0.1f);
        }
        foreach (SpriteRenderer dot in dots) Destroy(dot.gameObject);

        if (diver != null && !diver.IsDead && !IsStunned && !defeated)
            diver.StartDive(aim, diveSpeed, diveReturnSpeed, cam);
    }

    private void Fire()
    {
        BossFrame shooter = Root;
        if (frames.Count > 1 && UnityEngine.Random.value > rootFireChance)
            shooter = frames[UnityEngine.Random.Range(1, frames.Count)];
        if (shooter == null || shooter.IsDead) return;

        Vector3 muzzle = shooter.transform.position + Vector3.down * 0.4f;
        SplitShot shot = Instantiate(shotPrefab, muzzle, Quaternion.identity);
        shot.Launch(shooter, shooter.N, Vector2.down, shooter.Color);
    }
}
