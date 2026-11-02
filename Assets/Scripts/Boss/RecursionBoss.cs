using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Final boss: recurse(4). The root keeps calling itself, and so do its clones, growing a
/// call tree down toward the developer.
///   - Each frame's first call lands directly below it, so a column of clones (2 HP each)
///     shields the root; side calls branch outward.
///   - Destroying a frame unwinds its whole subtree, one depth level at a time.
///   - Growing past the stack limit throws StackOverflowError: the boss freezes, then its
///     deep frames unwind (and any stack growth resets).
///   - Frames fire SplitShots that split min(N, 3) times; unwound frames "return" their N
///     as falling digits (crashed frames return nothing).
/// The fight escalates in three phases by remaining HP:
///   1. (100-66%) split shots, return-value rain, stack-trace beam, Fibonacci volleys.
///   2. (66-33%)  + memoization (fires at your cached positions), stack growth (the tree
///                pushes down a row), tail-call dives, and the beam fires in 3 columns.
///   3. (< 33%)   mutual recursion: the tree collapses and the root splits into ping and
///                pong, sharing the remaining HP. Kill one and the other calls twice as
///                fast; kill both to win. The beam fires in 4 columns.
/// Parry: only the purple Fibonacci volleys and the gold memoized cache hits can be
/// parried; split shots, return values and dives must be dodged or shot.
/// Anti-spam: the smaller the tree, the faster it calls (it regrows when pruned), and a
/// root with no calls to wait on is idle (dimmed) and can't be damaged.
/// </summary>
public class RecursionBoss : MonoBehaviour, IMultiBarBoss
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
    [SerializeField] private Color mutualColor = new Color32(0xbc, 0x8c, 0xff, 0xff);
    [SerializeField] private Color digitColor = new Color32(0x0d, 0x11, 0x17, 0xff);
    [SerializeField] private OneShotAnimation unwindEffect;

    [Header("Recursion")]
    [Tooltip("The root is recurse(maxDepth); recurse(0) frames can't call further.")]
    [SerializeField] private int maxDepth = 4;
    [SerializeField] private int rootHp = 70;
    [SerializeField] private int cloneHp = 1;
    [Tooltip("HP of clones in the center column that shields the root.")]
    [SerializeField] private int shieldCloneHp = 2;
    [Tooltip("Seconds between recursive calls when the tree is full (stack limit).")]
    [SerializeField] private float callInterval = 1.6f;
    [Tooltip("Seconds between recursive calls when the tree is empty: pruning makes it regrow faster.")]
    [SerializeField] private float minCallInterval = 0.3f;
    [Tooltip("More frames than this throws StackOverflowError.")]
    [SerializeField] private int stackLimit = 18;
    [SerializeField] private float overflowStunTime = 4f;
    [Tooltip("After an overflow, frames at this depth or deeper unwind.")]
    [SerializeField] private int overflowUnwindDepth = 2;
    [Tooltip("Vertical distance between depth levels (0.875 = 14 pixels, keeps frames on the pixel grid).")]
    [SerializeField] private float rowStep = 0.875f;
    [Tooltip("Sideways distance of a side call at each depth (index = child depth). Checked so no two frames overlap.")]
    [SerializeField] private float[] sideStep = { 0f, 1.75f, 3.5f, 1.75f, 0.875f };
    [Tooltip("Delay between unwinding one depth level and the next.")]
    [SerializeField] private float unwindStepTime = 0.08f;

    [Header("Phases (fraction of total HP)")]
    [Range(0f, 1f)] [SerializeField] private float phase2At = 0.66f;
    [Range(0f, 1f)] [SerializeField] private float phase3At = 0.33f;

    [Header("Split shot")]
    [SerializeField] private SplitShot shotPrefab;
    [Tooltip("Seconds between shots at full HP / near death (hitting it makes it angrier).")]
    [SerializeField] private Vector2 fireIntervalRange = new Vector2(1.5f, 0.7f);
    [Tooltip("Chance a root fires a given shot itself (its shots split the most).")]
    [Range(0f, 1f)] [SerializeField] private float rootFireChance = 0.35f;
    [Tooltip("Shots never split more than this many times (recurse(4) would be 16 bullets).")]
    [SerializeField] private int maxShotSplits = 3;

    [Header("Return-value rain")]
    [Tooltip("Return values fall at this fraction of a split shot's speed.")]
    [SerializeField] private float returnValueSpeedScale = 0.75f;

    [Header("Fibonacci volley (all phases)")]
    [SerializeField] private float fibInterval = 4f;
    [Tooltip("Total fan angle of a volley, in degrees.")]
    [SerializeField] private float fibSpread = 70f;
    [SerializeField] private float fibSpeedScale = 0.9f;

    [Header("Stack-trace beam")]
    [SerializeField] private StackTraceBeam beamPrefab;
    [Tooltip("Seconds between beams at full HP / near death.")]
    [SerializeField] private Vector2 beamIntervalRange = new Vector2(9f, 4f);
    [Tooltip("Beam columns fired at once in phases 1, 2 and 3.")]
    [SerializeField] private int[] beamColumns = { 1, 3, 4 };
    [Tooltip("Distance between beam columns (beams are 1 unit wide, so 4 leaves 3-unit safe lanes).")]
    [SerializeField] private float beamColumnSpacing = 4f;

    [Header("Memoization (phase 2+)")]
    [Tooltip("Seconds between caching the developer's position.")]
    [SerializeField] private float memoSampleInterval = 1.2f;
    [SerializeField] private int memoCacheSize = 3;
    [SerializeField] private float memoInterval = 6f;
    [SerializeField] private float memoTelegraphTime = 0.7f;
    [SerializeField] private float memoSpeedScale = 1.6f;

    [Header("Stack growth (phase 2+)")]
    [SerializeField] private float growInterval = 9f;
    [Tooltip("Max rows the tree pushes down before an overflow resets it.")]
    [SerializeField] private int maxGrowRows = 3;

    [Header("Tail-call dive (phase 2+)")]
    [Tooltip("Seconds between dives in phase 2 / near death.")]
    [SerializeField] private Vector2 diveIntervalRange = new Vector2(6f, 3f);
    [SerializeField] private float diveTelegraphTime = 0.6f;
    [SerializeField] private float diveSpeed = 9f;
    [SerializeField] private float diveReturnSpeed = 8f;
    [SerializeField] private Sprite telegraphDot;
    [SerializeField] private int telegraphDots = 8;

    [Header("Mutual recursion (phase 3)")]
    [SerializeField] private int mutualDepth = 3;
    [Tooltip("Sideways steps for the two half-size trees (index = child depth).")]
    [SerializeField] private float[] mutualSideStep = { 0f, 1f, 1.75f, 0.875f };
    [SerializeField] private float mutualRootX = 4.75f;
    [SerializeField] private float splitTelegraphTime = 1f;
    [Tooltip("Call-interval multiplier for the survivor once one of ping/pong dies.")]
    [SerializeField] private float survivorCallMultiplier = 0.5f;

    [Header("Movement")]
    [SerializeField] private float driftAmplitude = 1f;
    [SerializeField] private float driftPeriod = 8f;
    [SerializeField] private int pixelsPerUnit = 16;

    public event Action<int, int> HpChanged;
    public event Action<string> LabelChanged;
    /// <summary>Raised when StackOverflowError starts (true) and ends (false).</summary>
    public event Action<bool> StunnedChanged;
    public event Action Defeated;
    /// <summary>Phase 3: each of ping and pong gets its own health bar.</summary>
    public event Action BarsChanged;

    public int BarCount => mutualRoots.Count > 0 ? mutualRoots.Count : 1;

    public void GetBar(int index, out int hp, out int max)
    {
        if (mutualRoots.Count == 0) { hp = Hp; max = MaxHp; return; }
        BossFrame r = mutualRoots[index];
        hp = r.IsDead ? 0 : Mathf.Max(0, r.Hp);
        max = r.MaxHp;
    }

    public string GetBarLabel(int index)
    {
        if (mutualRoots.Count == 0) return "";
        BossFrame r = mutualRoots[index];
        string name = MutualNames[index];
        return r.IsDead ? $"{name} returned" : $"{name} {Mathf.Max(0, r.Hp)}/{r.MaxHp} HP";
    }

    /// <summary>Total HP of the living root(s).</summary>
    public int Hp
    {
        get
        {
            int hp = 0;
            foreach (BossFrame r in roots) hp += Mathf.Max(0, r.Hp);
            return hp;
        }
    }
    public int MaxHp => rootHp;
    public int Phase { get; private set; } = 1;

    public string Label
    {
        get
        {
            if (IsStunned) return "StackOverflowError!";
            if (Phase < 3) return $"recurse({maxDepth})";
            if (splitting) return "mutual recursion...";
            if (roots.Count == 2) return "ping <-> pong";
            return roots.Count == 1 ? $"{RootName(roots[0]).Split('(')[0]}() alone: calls x2" : "";
        }
    }

    /// <summary>The first living root (phase 1-2: the only one).</summary>
    public BossFrame Root => roots.Count > 0 ? roots[0] : null;
    public int FrameCount => frames.Count;
    public bool IsStunned { get; private set; }

    private const int SlotBelow = 0, SlotSide = 1, SlotLeft = 2, SlotRight = 3;
    private static readonly string[] MutualNames = { "ping", "pong" };

    private static string RootName(BossFrame root) => root.name.Split('(')[0];
    private static readonly int[] Fibonacci = { 1, 1, 2, 3, 5, 8 };

    private readonly List<BossFrame> frames = new List<BossFrame>();
    private readonly List<BossFrame> roots = new List<BossFrame>();
    // ping and pong in bar order (kept after death so the dead one's bar shows empty).
    private readonly List<BossFrame> mutualRoots = new List<BossFrame>();
    private readonly List<(BossFrame parent, int slot)> openSlots = new List<(BossFrame, int)>();
    private readonly Queue<float> memoCache = new Queue<float>();
    private float[] activeSideStep;
    private float callTimer, fireTimer, beamTimer, diveTimer, fibTimer, memoTimer, memoSampleTimer, growTimer;
    private float callMultiplier = 1f;
    private int fibIndex;
    private int growRows;
    private bool splitting;
    private Transform target;
    private Camera cam;
    private float driftTime;
    private float baseX, baseY;
    private bool defeated;

    private void Awake()
    {
        cam = Camera.main;
        baseX = transform.position.x;
        baseY = transform.position.y;
        activeSideStep = sideStep;
        BossFrame root = SpawnFrame(null, 0, Vector3.zero, maxDepth, rootHp, rootColor);
        root.name = $"recurse({maxDepth})";
        roots.Add(root);
        fireTimer = fireIntervalRange.x;
        beamTimer = beamIntervalRange.x;
        diveTimer = diveIntervalRange.x;
        fibTimer = fibInterval;
        memoTimer = memoInterval;
        growTimer = growInterval;
    }

    /// <summary>The developer, for aiming dives and caching positions.</summary>
    public void SetTarget(Transform player) => target = player;

    /// <summary>0 at full HP, 1 when nearly dead. Attacks speed up with it.</summary>
    private float Anger => 1f - (float)Hp / MaxHp;

    private void Update()
    {
        if (defeated || IsStunned || splitting) return;
        float dt = Time.deltaTime;

        Drift(dt);

        callTimer += dt;
        float interval = CurrentCallInterval();
        if (callTimer >= interval)
        {
            callTimer -= interval;
            Call();
        }

        if (Tick(ref fireTimer, dt)) { FireSplitShot(); fireTimer = Mathf.Lerp(fireIntervalRange.x, fireIntervalRange.y, Anger); }
        if (Tick(ref beamTimer, dt)) { FireBeam(); beamTimer = Mathf.Lerp(beamIntervalRange.x, beamIntervalRange.y, Anger); }
        if (Tick(ref fibTimer, dt)) { FibonacciVolley(); fibTimer = fibInterval; }

        if (Phase >= 2)
        {
            memoSampleTimer -= dt;
            if (memoSampleTimer <= 0f && target != null)
            {
                memoSampleTimer = memoSampleInterval;
                memoCache.Enqueue(target.position.x);
                while (memoCache.Count > memoCacheSize) memoCache.Dequeue();
            }
            if (Tick(ref memoTimer, dt)) { StartCoroutine(MemoizedStrike()); memoTimer = memoInterval; }
            if (Tick(ref growTimer, dt)) { GrowStack(); growTimer = growInterval; }
            if (Tick(ref diveTimer, dt) && target != null)
            {
                StartCoroutine(TailCallDive());
                float t = Mathf.InverseLerp(1f - phase2At, 1f, Anger);
                diveTimer = Mathf.Lerp(diveIntervalRange.x, diveIntervalRange.y, t);
            }
        }
    }

    /// <summary>Adaptive call rate: an empty tree calls every minCallInterval, a full one every callInterval.</summary>
    private float CurrentCallInterval()
    {
        float fullness = Mathf.Clamp01((frames.Count - roots.Count) / (float)Mathf.Max(1, stackLimit - roots.Count));
        return Mathf.Lerp(minCallInterval, callInterval, fullness) * callMultiplier;
    }

    private static bool Tick(ref float timer, float dt)
    {
        timer -= dt;
        return timer <= 0f;
    }

    private void Drift(float dt)
    {
        driftTime += dt;
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
        int direction = DirectionFor(parent, chosen);
        int hp = direction == 0 ? shieldCloneHp : cloneHp; // the center column shields the root
        int n = parent.N - 1;
        Color color = n == 0 ? baseCaseColor : cloneColor;
        BossFrame child = SpawnFrame(parent, chosen, SlotPosition(parent, chosen), n, hp, color);
        child.name = $"recurse({n})";
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
        Vector3 p = parent.SlotLocalPosition;
        float step = activeSideStep[Mathf.Min(depth, activeSideStep.Length - 1)];
        switch (slot)
        {
            case SlotLeft: p.x -= step; break;
            case SlotRight: p.x += step; break;
            case SlotSide: p.x += parent.Direction * step; break;
        }
        p.y -= rowStep;
        return p;
    }

    private static int DirectionFor(BossFrame parent, int slot)
    {
        if (slot == SlotLeft) return -1;
        if (slot == SlotRight) return 1;
        return parent.Direction;
    }

    private BossFrame SpawnFrame(BossFrame parent, int slot, Vector3 localPosition, int n, int hp, Color color)
    {
        BossFrame frame = Instantiate(framePrefab, transform);
        frame.transform.localPosition = localPosition;
        frame.SlotLocalPosition = localPosition;

        bool root = parent == null;
        int depth = root ? 0 : parent.Depth + 1;
        frame.Init(this, parent, depth, n, root ? 0 : DirectionFor(parent, slot), hp,
                   root ? rootSprite : cloneSprite, digits[Mathf.Clamp(n, 0, digits.Length - 1)], color, digitColor);
        frame.SlotInParent = slot;
        frames.Add(frame);
        return frame;
    }

    // ---------- Damage, phases & unwinding ----------

    public void OnFrameDamaged(BossFrame frame)
    {
        if (!frame.IsRoot) return;
        HpChanged?.Invoke(Hp, MaxHp);
        if (Phase == 3)
        {
            LabelChanged?.Invoke(Label); // per-root HP is in the label
            BarsChanged?.Invoke();
        }
        float fraction = (float)Hp / MaxHp;
        if (Phase == 1 && fraction <= phase2At) Phase = 2;
        if (Phase == 2 && fraction <= phase3At && Hp > 0 && !splitting) StartCoroutine(MutualSplit());
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

        bool lastRoot = false;
        if (frame.IsRoot)
        {
            roots.Remove(frame);
            HpChanged?.Invoke(Hp, MaxHp);
            if (roots.Count == 0) { lastRoot = true; defeated = true; }
            else
            {
                // Mutual recursion: the survivor calls twice as fast.
                callMultiplier = survivorCallMultiplier;
                LabelChanged?.Invoke(Label);
            }
            BarsChanged?.Invoke();
        }
        StartCoroutine(PopLevels(levels, lastRoot, returnValues && !frame.IsRoot));
    }

    private IEnumerator PopLevels(List<List<BossFrame>> levels, bool lastRoot, bool returnValues)
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
        if (lastRoot) Defeated?.Invoke();
    }

    private IEnumerator StackOverflow()
    {
        IsStunned = true;
        StunnedChanged?.Invoke(true);
        LabelChanged?.Invoke(Label);
        foreach (BossFrame f in frames) f.Flash(overflowStunTime);

        yield return new WaitForSeconds(overflowStunTime);

        // The crash clears the deep part of the stack, and the stack shrinks back up.
        var deep = new List<BossFrame>();
        foreach (BossFrame f in frames)
            if (f.Depth == overflowUnwindDepth) deep.Add(f);
        foreach (BossFrame f in deep)
            if (!f.IsDead) Unwind(f, returnValues: false); // crashed calls never return
        ResetStackGrowth();

        IsStunned = false;
        callTimer = 0f;
        StunnedChanged?.Invoke(false);
        LabelChanged?.Invoke(Label);
    }

    /// <summary>
    /// Phase 3: the call tree collapses and the root splits into ping and pong, two
    /// half-size trees that share the remaining HP.
    /// </summary>
    private IEnumerator MutualSplit()
    {
        splitting = true;
        Phase = 3;
        BossFrame old = roots[0];
        int remaining = Mathf.Max(2, old.Hp);
        old.MarkDead(); // untouchable while it splits, so it can't be killed mid-transition

        // Everything below the root unwinds (no return values: this is a restructure, not a return).
        foreach (BossFrame child in new List<BossFrame>(old.Children))
            if (!child.IsDead) Unwind(child, returnValues: false);
        LabelChanged?.Invoke("mutual recursion...");
        old.Flash(splitTelegraphTime);
        yield return new WaitForSeconds(splitTelegraphTime);

        Vector3 at = old.transform.position;
        roots.Remove(old);
        frames.Remove(old);
        if (unwindEffect != null) unwindEffect.Spawn(at, old.Color);
        Destroy(old.gameObject);

        activeSideStep = mutualSideStep;
        ResetStackGrowth();
        int half = Mathf.CeilToInt(remaining / 2f);
        float[] xs = { -mutualRootX, mutualRootX };
        for (int i = 0; i < 2; i++)
        {
            BossFrame r = SpawnFrame(null, 0, new Vector3(xs[i], 0f, 0f), mutualDepth,
                                     i == 0 ? half : remaining - half, mutualColor);
            r.name = $"{MutualNames[i]}({mutualDepth})";
            roots.Add(r);
            mutualRoots.Add(r);
        }
        callTimer = 0f;
        splitting = false;
        HpChanged?.Invoke(Hp, MaxHp);
        LabelChanged?.Invoke(Label);
        BarsChanged?.Invoke();
    }

    private void GrowStack()
    {
        if (growRows >= maxGrowRows) return;
        growRows++;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, baseY - growRows * rowStep, p.z);
    }

    private void ResetStackGrowth()
    {
        growRows = 0;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, baseY, p.z);
    }

    // ---------- Attacks ----------

    private BossFrame RandomRoot() => roots.Count == 0 ? null : roots[UnityEngine.Random.Range(0, roots.Count)];

    /// <summary>An unwound frame "returns" its N: the digit falls straight down.</summary>
    private void ReturnValue(BossFrame frame)
    {
        SplitShot value = Instantiate(shotPrefab, frame.transform.position, Quaternion.identity);
        value.Launch(frame, 0, Vector2.down, frame.Color, digits[Mathf.Clamp(frame.N, 0, digits.Length - 1)], returnValueSpeedScale);
        value.MakeUnparryable();
    }

    private void FireSplitShot()
    {
        BossFrame shooter = RandomRoot();
        if (frames.Count > roots.Count && UnityEngine.Random.value > rootFireChance)
            shooter = frames[UnityEngine.Random.Range(0, frames.Count)];
        if (shooter == null || shooter.IsDead) return;

        Vector3 muzzle = shooter.transform.position + Vector3.down * 0.4f;
        SplitShot shot = Instantiate(shotPrefab, muzzle, Quaternion.identity);
        shot.Launch(shooter, Mathf.Min(shooter.N, maxShotSplits), Vector2.down, shooter.Color);
        shot.MakeUnparryable(); // only purple (Fibonacci) and gold (cache hit) shots can be parried
    }

    /// <summary>Fibonacci volley: 1, 1, 2, 3, 5, 8 shots in a fan, then the sequence repeats.</summary>
    private void FibonacciVolley()
    {
        BossFrame shooter = RandomRoot();
        if (shooter == null) return;
        int count = Fibonacci[fibIndex];
        string label = $"fib({fibIndex + 1})";
        fibIndex = (fibIndex + 1) % Fibonacci.Length;

        Vector3 muzzle = shooter.transform.position + Vector3.down * 0.6f;
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0f : Mathf.Lerp(-fibSpread / 2f, fibSpread / 2f, i / (float)(count - 1));
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.down;
            SplitShot shot = Instantiate(shotPrefab, muzzle, Quaternion.identity);
            shot.Launch(shooter, 0, dir, mutualColor, speedScale: fibSpeedScale, label: label);
            shot.MakeUnshootable(); // parry-only: shots pass through, so the breakpoint is the answer
        }
    }

    /// <summary>
    /// Memoization: the boss caches where the developer has been, marks those spots on the
    /// floor, then drops fast shots on the cached positions.
    /// </summary>
    private IEnumerator MemoizedStrike()
    {
        if (memoCache.Count == 0 || target == null) yield break;
        var cached = new List<float>(memoCache);
        float floorY = target.position.y;

        var marks = new List<SpriteRenderer>();
        foreach (float x in cached)
        {
            var mark = new GameObject("CacheHitMark").AddComponent<SpriteRenderer>();
            mark.sprite = telegraphDot;
            mark.color = baseCaseColor;
            mark.sortingOrder = 3;
            mark.transform.position = new Vector3(x, floorY - 0.7f, 0f);
            marks.Add(mark);
        }
        for (float t = 0f; t < memoTelegraphTime; t += 0.1f)
        {
            foreach (SpriteRenderer m in marks) m.enabled = !m.enabled;
            yield return new WaitForSeconds(0.1f);
        }
        foreach (SpriteRenderer m in marks) Destroy(m.gameObject);
        if (defeated) yield break;

        float top = cam.transform.position.y + cam.orthographicSize - 0.5f;
        foreach (float x in cached)
        {
            SplitShot shot = Instantiate(shotPrefab, new Vector3(x, top, 0f), Quaternion.identity);
            shot.Launch(null, 0, Vector2.down, baseCaseColor, speedScale: memoSpeedScale, label: "cache hit");
            shot.MakeUnshootable(); // a cache hit can't be shot away: dodge it or parry it
        }
    }

    /// <summary>
    /// Stack-trace beams: phase 1 fires one column under a root; later phases fire several
    /// evenly spaced columns at once, centered on the boss so they drift with it.
    /// All columns telegraph together, leaving safe lanes between them.
    /// </summary>
    private void FireBeam()
    {
        BossFrame root = RandomRoot();
        if (beamPrefab == null || root == null) return;
        float top = root.transform.position.y - root.GetComponent<SpriteRenderer>().bounds.extents.y;
        float bottom = cam.transform.position.y - cam.orthographicSize;

        int count = beamColumns[Mathf.Clamp(Phase - 1, 0, beamColumns.Length - 1)];
        float center = count == 1 ? root.transform.position.x : transform.position.x;
        float halfView = cam.orthographicSize * cam.aspect;
        for (int i = 0; i < count; i++)
        {
            float x = center + (i - (count - 1) / 2f) * beamColumnSpacing;
            x = Mathf.Clamp(x, cam.transform.position.x - halfView + 0.5f, cam.transform.position.x + halfView - 0.5f);
            Instantiate(beamPrefab).Fire(x, top, bottom);
        }
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
}
