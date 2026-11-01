using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Miniboss: binary search. A bug hides in one cell of a sorted array. Shooting a cell is
/// a comparison: a miss shows &lt; or &gt; toward the bug and eliminates the half of the
/// array that can't contain it (grayed out, shots pass through), exactly as a binary
/// search discards half its range. Hitting the bug's cell is a find.
///
///   - Efficient searches hurt more: a find deals base damage plus a bonus for every
///     comparison left in the budget.
///   - The search space is your space: walls close in under the eliminated cells,
///     confining the developer to the area under the live range until the re-sort.
///   - Off-by-one: every probe makes the probed cell's two neighbors fire.
///   - Unsorted input (low HP): binary search needs sorted data, so one hint per search
///     lies (wrong arrow, faint flicker, eliminates nothing).
///   - Running out of comparisons re-sorts the array (the bug moves, hints clear, walls
///     open) and every live cell fires at once. Live cells also drop shots meanwhile.
/// </summary>
public class BinarySearchBoss : MonoBehaviour, IBoss
{
    [Header("Array")]
    [SerializeField] private SearchCell cellPrefab;
    [SerializeField] private int cellCount = 16;
    [Tooltip("Distance between cell centers (wide enough that a coffee splash probes one cell).")]
    [SerializeField] private float spacing = 1.1875f;
    [SerializeField] private Sprite unknownGlyph;
    [SerializeField] private Sprite lessGlyph;
    [SerializeField] private Sprite greaterGlyph;
    [SerializeField] private Sprite bugGlyph;
    [SerializeField] private Color unknownColor = new Color32(0x58, 0xa6, 0xff, 0xff);
    [SerializeField] private Color hintColor = new Color32(0xe3, 0xb3, 0x41, 0xff);
    [SerializeField] private Color eliminatedColor = new Color32(0x30, 0x36, 0x3d, 0xff);
    [SerializeField] private Color foundColor = new Color32(0xf8, 0x51, 0x49, 0xff);

    [Header("Health")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int baseFindDamage = 20;
    [Tooltip("Extra damage per comparison left unused when the bug is found.")]
    [SerializeField] private int damagePerSavedComparison = 8;

    [Header("Search")]
    [Tooltip("Comparisons allowed per search (an optimal search of 16 cells needs at most 5).")]
    [SerializeField] private int comparisonBudget = 6;
    [Tooltip("Seconds the found bug / last hint stays visible before the re-sort scramble.")]
    [SerializeField] private float revealTime = 0.5f;
    [SerializeField] private float resortTime = 1.2f;
    [SerializeField] private OneShotAnimation popEffect;

    [Header("Unsorted input")]
    [Tooltip("At or below this HP fraction, one hint per search lies.")]
    [Range(0f, 1f)] [SerializeField] private float unsortedHpFraction = 0.3f;
    [Tooltip("Extra comparisons allowed per search while the input is unsorted.")]
    [SerializeField] private int unsortedExtraComparisons = 1;

    [Header("Closing walls")]
    [SerializeField] private SpriteRenderer wallPrefab;
    [Tooltip("The developer always keeps at least this much room.")]
    [SerializeField] private float minRoom = 3f;
    [Tooltip("How fast the walls slide, in units per second.")]
    [SerializeField] private float wallSpeed = 6f;

    [Header("Attack")]
    [SerializeField] private SplitShot shotPrefab;
    [Tooltip("Seconds between shots at full HP / near death.")]
    [SerializeField] private Vector2 fireIntervalRange = new Vector2(0.9f, 0.5f);

    public event Action<int, int> HpChanged;
    public event Action<string> LabelChanged;
    public event Action<bool> StunnedChanged;
    public event Action Defeated;

    public int Hp { get; private set; }
    public int MaxHp => maxHp;
    public string Label
    {
        get
        {
            if (resorting) return "re-sorting...";
            string label = $"binarySearch()  cmp {used}/{Budget}";
            return Unsorted ? label + "  (unsorted!)" : label;
        }
    }

    private bool Unsorted => (float)Hp / maxHp <= unsortedHpFraction;
    private int Budget => comparisonBudget + (searchUnsorted ? unsortedExtraComparisons : 0);

    private SearchCell[] cells;
    private bool[] probed;
    private int target;
    private int lo, hi; // inclusive search range
    private int used;
    private int misses;
    private int lieOnMiss; // 0 = no lie this search
    private bool searchUnsorted;
    private bool resorting;
    private bool defeated;
    private float fireTimer;

    private Camera cam;
    private PlayerController playerMovement;
    private SpriteRenderer leftWall, rightWall;
    private float wallMin, wallMax;          // current (animated) room edges
    private float targetMin, targetMax;      // where the walls are heading

    private void Awake()
    {
        cam = Camera.main;
        Hp = maxHp;
        cells = new SearchCell[cellCount];
        probed = new bool[cellCount];
        float width = (cellCount - 1) * spacing;
        for (int i = 0; i < cellCount; i++)
        {
            cells[i] = Instantiate(cellPrefab, transform);
            cells[i].transform.localPosition = new Vector3(i * spacing - width / 2f, 0f, 0f);
            cells[i].Init(this, i);
        }

        leftWall = Instantiate(wallPrefab, transform);
        rightWall = Instantiate(wallPrefab, transform);
        GetScreenEdges(out wallMin, out wallMax);
        targetMin = wallMin;
        targetMax = wallMax;

        NewSearch();
        fireTimer = fireIntervalRange.x;
    }

    public void SetTarget(Transform player) => playerMovement = player.GetComponent<PlayerController>();

    private void OnDestroy()
    {
        if (playerMovement != null) playerMovement.ClearMovementLimits();
    }

    private void Update()
    {
        UpdateWalls();
        if (defeated || resorting) return;

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            FireFrom(RandomLiveCell());
            float anger = 1f - (float)Hp / maxHp;
            fireTimer = Mathf.Lerp(fireIntervalRange.x, fireIntervalRange.y, anger);
        }
    }

    // ---------- Searching ----------

    /// <summary>A comparison: called when the player (or a parried shot) hits a live cell.</summary>
    public void Probe(SearchCell cell)
    {
        if (defeated || resorting || !cell.IsLive) return;
        int i = cell.Index;
        used++;
        probed[i] = true;

        // Off-by-one: the neighbors retaliate.
        if (i > 0) FireFrom(cells[i - 1]);
        if (i < cellCount - 1) FireFrom(cells[i + 1]);

        if (i == target)
        {
            Find(cell);
            return;
        }

        misses++;
        bool lie = misses == lieOnMiss;
        bool bugIsRight = i < target;
        if (lie)
        {
            // Unsorted input: the wrong arrow, and nothing is eliminated.
            cell.Show(bugIsRight ? lessGlyph : greaterGlyph, hintColor, false);
            StartCoroutine(Flicker(cell));
        }
        else
        {
            if (bugIsRight) lo = Mathf.Max(lo, i + 1);
            else hi = Mathf.Min(hi, i - 1);
            cell.Show(bugIsRight ? greaterGlyph : lessGlyph, hintColor, false);
        }
        RefreshCells();
        LabelChanged?.Invoke(Label);

        if (used >= Budget) StartCoroutine(Resort(punish: true));
    }

    private void Find(SearchCell cell)
    {
        int damage = baseFindDamage + damagePerSavedComparison * Mathf.Max(0, Budget - used);
        Hp = Mathf.Max(0, Hp - damage);
        cell.Show(bugGlyph, foundColor, false);
        if (popEffect != null) popEffect.Spawn(cell.transform.position, foundColor);
        HpChanged?.Invoke(Hp, maxHp);
        LabelChanged?.Invoke($"found in {used} cmp: -{damage}");

        if (Hp <= 0) StartCoroutine(Defeat());
        else StartCoroutine(Resort(punish: false));
    }

    /// <summary>Grays out cells outside the range (or already probed) and moves the walls in.</summary>
    private void RefreshCells()
    {
        foreach (SearchCell c in cells)
        {
            if (!c.IsLive) continue;
            bool live = c.Index >= lo && c.Index <= hi && !probed[c.Index];
            if (!live) c.Show(unknownGlyph, eliminatedColor, false);
        }
        CloseWallsAround(lo, hi);
    }

    private void NewSearch()
    {
        target = UnityEngine.Random.Range(0, cellCount);
        lo = 0;
        hi = cellCount - 1;
        used = 0;
        misses = 0;
        searchUnsorted = Unsorted;
        // One of the first two misses lies, while the input is unsorted.
        lieOnMiss = searchUnsorted ? UnityEngine.Random.Range(1, 3) : 0;
        Array.Clear(probed, 0, probed.Length);
        foreach (SearchCell c in cells) c.Show(unknownGlyph, unknownColor, true);
        OpenWalls();
        LabelChanged?.Invoke(Label);
    }

    /// <summary>The array re-sorts: the bug moves, hints clear, walls open.</summary>
    private IEnumerator Resort(bool punish)
    {
        if (punish)
            foreach (SearchCell c in cells)
                if (c.IsLive) FireFrom(c);

        resorting = true;
        StunnedChanged?.Invoke(true);

        // Hold the result (the found bug, or the last hint) on screen for a moment.
        yield return new WaitForSeconds(revealTime);
        LabelChanged?.Invoke(Label);
        OpenWalls();

        // Scramble the glyphs while the array sorts itself again.
        Sprite[] scramble = { unknownGlyph, lessGlyph, greaterGlyph };
        for (float t = 0f; t < resortTime; t += 0.1f)
        {
            foreach (SearchCell c in cells)
                c.Show(scramble[UnityEngine.Random.Range(0, scramble.Length)], eliminatedColor, false);
            yield return new WaitForSeconds(0.1f);
        }

        resorting = false;
        StunnedChanged?.Invoke(false);
        NewSearch();
    }

    /// <summary>A lying hint flickers faintly, the only tell that the input is unsorted.</summary>
    private IEnumerator Flicker(SearchCell cell)
    {
        var glyph = cell.transform.Find("Glyph").GetComponent<SpriteRenderer>();
        var wait = new WaitForSeconds(0.15f);
        while (cell != null && glyph.sprite != unknownGlyph && !resorting)
        {
            Color c = glyph.color;
            glyph.color = new Color(c.r, c.g, c.b, c.a > 0.75f ? 0.55f : 1f);
            yield return wait;
        }
    }

    private IEnumerator Defeat()
    {
        defeated = true;
        OpenWalls();
        LabelChanged?.Invoke("return index;");
        yield return new WaitForSeconds(revealTime);
        // Pop the cells from the bug outward.
        for (int d = 0; d < cellCount; d++)
        {
            foreach (int i in new[] { target - d, target + d })
            {
                if (i < 0 || i >= cellCount || cells[i] == null) continue;
                if (popEffect != null) popEffect.Spawn(cells[i].transform.position, unknownColor);
                Destroy(cells[i].gameObject);
                cells[i] = null;
            }
            yield return new WaitForSeconds(0.05f);
        }
        Defeated?.Invoke();
    }

    // ---------- Closing walls ----------

    private void CloseWallsAround(int first, int last)
    {
        float half = spacing / 2f;
        float min = cells[first].transform.position.x - half;
        float max = cells[last].transform.position.x + half;

        // Never leave less than minRoom.
        float missing = minRoom - (max - min);
        if (missing > 0f) { min -= missing / 2f; max += missing / 2f; }

        GetScreenEdges(out float left, out float right);
        if (min < left) { max += left - min; min = left; }
        if (max > right) { min -= max - right; max = right; }
        targetMin = min;
        targetMax = max;
    }

    private void OpenWalls() => GetScreenEdges(out targetMin, out targetMax);

    private void GetScreenEdges(out float left, out float right)
    {
        float half = cam.orthographicSize * cam.aspect;
        left = cam.transform.position.x - half;
        right = cam.transform.position.x + half;
    }

    /// <summary>Slides the walls toward their targets and keeps the developer between them.</summary>
    private void UpdateWalls()
    {
        float step = wallSpeed * Time.deltaTime;
        wallMin = Mathf.MoveTowards(wallMin, targetMin, step);
        wallMax = Mathf.MoveTowards(wallMax, targetMax, step);

        GetScreenEdges(out float left, out float right);
        float top = transform.position.y - 0.5f;
        float bottom = cam.transform.position.y - cam.orthographicSize;
        PlaceWall(leftWall, left, wallMin, top, bottom);
        PlaceWall(rightWall, wallMax, right, top, bottom);

        if (playerMovement == null) return;
        if (wallMin <= left && wallMax >= right) playerMovement.ClearMovementLimits();
        else playerMovement.SetMovementLimits(wallMin, wallMax);
    }

    private static void PlaceWall(SpriteRenderer wall, float x0, float x1, float top, float bottom)
    {
        float width = x1 - x0;
        wall.enabled = width > 0.01f;
        if (!wall.enabled) return;
        wall.transform.position = new Vector3((x0 + x1) / 2f, (top + bottom) / 2f, 0f);
        wall.size = new Vector2(width, top - bottom);
    }

    // ---------- Attack ----------

    private SearchCell RandomLiveCell()
    {
        int count = 0;
        foreach (SearchCell c in cells) if (c != null && c.IsLive) count++;
        if (count == 0) return null;
        int pick = UnityEngine.Random.Range(0, count);
        foreach (SearchCell c in cells)
            if (c != null && c.IsLive && pick-- == 0) return c;
        return null;
    }

    private void FireFrom(SearchCell cell)
    {
        if (cell == null) return;
        Vector3 muzzle = cell.transform.position + Vector3.down * 0.5f;
        SplitShot shot = Instantiate(shotPrefab, muzzle, Quaternion.identity);
        shot.Launch(null, 0, Vector2.down, unknownColor, label: cell.name);
    }
}
