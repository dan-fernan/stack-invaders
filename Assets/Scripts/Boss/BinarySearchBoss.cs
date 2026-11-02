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
///   - IndexOutOfBounds: cannons at arr[-1] and arr[n] fire aimed shots in from the edges,
///     faster as the boss weakens.
///   - Linear-search taunt: probing right next to your last probe while 3+ cells are still
///     in range (something a binary search never does) flashes O(n)! and doubles the
///     array's fire rate until the next re-sort.
///   - Running out of comparisons re-sorts the array (the bug moves, hints clear, walls
///     open) and every live cell fires at once. Live cells also drop shots meanwhile.
/// </summary>
public class BinarySearchBoss : MonoBehaviour, IBoss
{
    [Header("Array")]
    [SerializeField] private SearchCell cellPrefab;
    [SerializeField] private int cellCount = 16;
    [Tooltip("Distance between cell centers (wide enough that a coffee splash probes one cell).")]
    [SerializeField] private float spacing = 1.125f;
    [SerializeField] private Sprite unknownGlyph;
    [SerializeField] private Sprite lessGlyph;
    [SerializeField] private Sprite greaterGlyph;
    [SerializeField] private Sprite bugGlyph;
    [SerializeField] private Color unknownColor = new Color32(0x58, 0xa6, 0xff, 0xff);
    [SerializeField] private Color hintColor = new Color32(0xe3, 0xb3, 0x41, 0xff);
    [SerializeField] private Color eliminatedColor = new Color32(0x30, 0x36, 0x3d, 0xff);
    [SerializeField] private Color foundColor = new Color32(0xf8, 0x51, 0x49, 0xff);

    [Header("Health")]
    [SerializeField] private int maxHp = 160;
    [SerializeField] private int baseFindDamage = 15;
    [Tooltip("Extra damage per comparison left unused when the bug is found.")]
    [SerializeField] private int damagePerSavedComparison = 6;

    [Header("Search")]
    [Tooltip("Comparisons allowed per search (an optimal search of 16 cells needs at most 5).")]
    [SerializeField] private int comparisonBudget = 6;
    [Tooltip("Seconds the last hint stays visible before a re-sort after running out of comparisons.")]
    [SerializeField] private float revealTime = 0.5f;
    [Tooltip("Seconds the found-bug moment lasts (cell pops and blinks, damage pop-up) before the re-sort.")]
    [SerializeField] private float findRevealTime = 1.5f;
    [Tooltip("Whole-number scale the found cell pops to.")]
    [SerializeField] private int foundPopScale = 2;
    [SerializeField] private FloatingText popupPrefab;
    [SerializeField] private float resortTime = 1.2f;
    [SerializeField] private OneShotAnimation popEffect;

    [Header("IndexOutOfBounds cannons")]
    [Tooltip("Placed at arr[-1] and arr[n]; not shootable.")]
    [SerializeField] private SpriteRenderer cannonPrefab;
    [Tooltip("Seconds between cannon shots at full HP / near death (the cannons alternate).")]
    [SerializeField] private Vector2 cannonIntervalRange = new Vector2(4f, 1.5f);
    [SerializeField] private float cannonTelegraphTime = 0.35f;
    [SerializeField] private Color cannonColor = new Color32(0xf8, 0x51, 0x49, 0xff);

    [Header("Linear-search taunt")]
    [Tooltip("Probing next to the previous probe while at least this many cells are in range counts as linear search.")]
    [SerializeField] private int linearRangeThreshold = 3;
    [Tooltip("Fire-interval multiplier while taunting (0.5 = twice as fast).")]
    [SerializeField] private float tauntFireMultiplier = 0.5f;

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
            string label = $"binarySearch()  cmp {comparisonsUsed}/{comparisonBudget}";
            return taunting ? label + "  O(n)!" : label;
        }
    }

    /// <summary>0 at full HP, 1 near death. Attacks speed up with it.</summary>
    private float Anger => 1f - (float)Hp / maxHp;

    private SearchCell[] cells;
    private bool[] probed;
    private int target;
    private int lo, hi; // inclusive search range
    private int comparisonsUsed;
    private int lastProbe = -1;
    private bool taunting;
    private bool resorting;
    private bool defeated;
    private float fireTimer;
    private float cannonTimer;
    private bool leftCannonNext = true;
    private SpriteRenderer leftCannon, rightCannon;

    private Camera cam;
    private RectTransform popupCanvas;
    private PlayerController playerMovement;
    private SpriteRenderer leftWall, rightWall;
    private float wallMin, wallMax;          // current (animated) room edges
    private float targetMin, targetMax;      // where the walls are heading

    private void Awake()
    {
        cam = Camera.main;
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) popupCanvas = (RectTransform)canvas.transform;
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

        leftCannon = SpawnCannon(-1, width);
        rightCannon = SpawnCannon(cellCount, width);

        leftWall = Instantiate(wallPrefab, transform);
        rightWall = Instantiate(wallPrefab, transform);
        GetScreenEdges(out wallMin, out wallMax);
        targetMin = wallMin;
        targetMax = wallMax;

        NewSearch();
        fireTimer = fireIntervalRange.x;
        cannonTimer = cannonIntervalRange.x;
    }

    private SpriteRenderer SpawnCannon(int index, float arrayWidth)
    {
        SpriteRenderer cannon = Instantiate(cannonPrefab, transform);
        cannon.transform.localPosition = new Vector3(index * spacing - arrayWidth / 2f, 0f, 0f);
        cannon.name = $"arr[{index}]";
        cannon.color = cannonColor;
        foreach (SpriteRenderer part in cannon.GetComponentsInChildren<SpriteRenderer>()) part.color = cannonColor;
        return cannon;
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
            fireTimer = Mathf.Lerp(fireIntervalRange.x, fireIntervalRange.y, Anger)
                      * (taunting ? tauntFireMultiplier : 1f);
        }

        cannonTimer -= Time.deltaTime;
        if (cannonTimer <= 0f)
        {
            StartCoroutine(FireCannon(leftCannonNext ? leftCannon : rightCannon));
            leftCannonNext = !leftCannonNext;
            cannonTimer = Mathf.Lerp(cannonIntervalRange.x, cannonIntervalRange.y, Anger);
        }
    }

    // ---------- Searching ----------

    /// <summary>A comparison: called when the player (or a parried shot) hits a live cell.</summary>
    public void Probe(SearchCell cell)
    {
        if (defeated || resorting || !cell.IsLive) return;
        int i = cell.Index;
        comparisonsUsed++;
        probed[i] = true;

        // Linear search: a binary search never probes next to its last probe while 3+ cells remain.
        bool linear = lastProbe >= 0 && Mathf.Abs(i - lastProbe) == 1 && hi - lo + 1 >= linearRangeThreshold;
        lastProbe = i;
        if (linear) taunting = true;

        // Off-by-one: the neighbors retaliate.
        if (i > 0) FireFrom(cells[i - 1]);
        if (i < cellCount - 1) FireFrom(cells[i + 1]);

        if (i == target)
        {
            Find(cell);
            return;
        }

        // Miss: point toward the bug and discard the half that can't contain it.
        bool bugIsRight = i < target;
        if (bugIsRight) lo = Mathf.Max(lo, i + 1);
        else hi = Mathf.Min(hi, i - 1);
        cell.Show(bugIsRight ? greaterGlyph : lessGlyph, hintColor, false);
        RefreshCells();
        LabelChanged?.Invoke(Label);

        if (comparisonsUsed >= comparisonBudget) StartCoroutine(Resort(punish: true));
    }

    private void Find(SearchCell cell)
    {
        int damage = baseFindDamage + damagePerSavedComparison * Mathf.Max(0, comparisonBudget - comparisonsUsed);
        Hp = Mathf.Max(0, Hp - damage);
        resorting = true; // stop firing and probing for the whole found sequence
        StunnedChanged?.Invoke(true);
        HpChanged?.Invoke(Hp, maxHp);
        LabelChanged?.Invoke($"found arr[{cell.Index}] in {comparisonsUsed} cmp: -{damage}");
        StartCoroutine(FoundSequence(cell, damage));
    }

    /// <summary>
    /// The found moment: everything else dims, the bug's cell pops to 2x and blinks,
    /// a pop-up shows the damage. Then the re-sort (or the defeat).
    /// </summary>
    private IEnumerator FoundSequence(SearchCell cell, int damage)
    {
        foreach (SearchCell c in cells)
            if (c != cell) c.Show(unknownGlyph, eliminatedColor, false);
        cell.Show(bugGlyph, foundColor, false);
        if (popEffect != null) popEffect.Spawn(cell.transform.position, foundColor);
        if (popupPrefab != null && popupCanvas != null)
            popupPrefab.Spawn(popupCanvas, cam, cell.transform.position + Vector3.down * 1.25f,
                              $"found arr[{cell.Index}]!  -{damage}", foundColor, findRevealTime);

        // Draw the popped cell over its (overlapped) neighbors.
        SpriteRenderer[] parts = cell.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer part in parts) part.sortingOrder += 10;

        Transform t = cell.transform;
        t.localScale = Vector3.one * foundPopScale;
        var wait = new WaitForSeconds(0.12f);
        bool white = false;
        for (float time = 0f; time < findRevealTime; time += 0.12f)
        {
            white = !white;
            cell.Show(bugGlyph, white ? Color.white : foundColor, false);
            if (time > findRevealTime * 0.7f) t.localScale = Vector3.one; // the bug ducks back in
            yield return wait;
        }
        t.localScale = Vector3.one;
        foreach (SpriteRenderer part in parts) part.sortingOrder -= 10;
        cell.Show(bugGlyph, foundColor, false);

        if (Hp <= 0) StartCoroutine(Defeat());
        else StartCoroutine(Resort(punish: false, holdResult: false));
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
        comparisonsUsed = 0;
        lastProbe = -1;
        taunting = false;
        Array.Clear(probed, 0, probed.Length);
        foreach (SearchCell c in cells) c.Show(unknownGlyph, unknownColor, true);
        OpenWalls();
        LabelChanged?.Invoke(Label);
    }

    /// <summary>The array re-sorts: the bug moves, hints clear, walls open.</summary>
    /// <param name="holdResult">Keep the last hint on screen for a moment first (a find has its own sequence).</param>
    private IEnumerator Resort(bool punish, bool holdResult = true)
    {
        if (punish)
            foreach (SearchCell c in cells)
                if (c.IsLive) FireFrom(c);

        resorting = true;
        StunnedChanged?.Invoke(true);

        if (holdResult) yield return new WaitForSeconds(revealTime);
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

    private IEnumerator Defeat()
    {
        defeated = true;
        OpenWalls();
        LabelChanged?.Invoke($"return {target};");
        yield return new WaitForSeconds(0.3f);
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

    /// <summary>An out-of-bounds cannon flashes, then fires an aimed shot from the array's edge.</summary>
    private IEnumerator FireCannon(SpriteRenderer cannon)
    {
        var parts = cannon.GetComponentsInChildren<SpriteRenderer>();
        for (float t = 0f; t < cannonTelegraphTime; t += 0.07f)
        {
            foreach (SpriteRenderer part in parts) part.color = part.color == cannonColor ? Color.white : cannonColor;
            yield return new WaitForSeconds(0.07f);
        }
        foreach (SpriteRenderer part in parts) part.color = cannonColor;
        if (defeated || resorting || playerMovement == null) yield break;

        Vector3 muzzle = cannon.transform.position + Vector3.down * 0.5f;
        Vector2 aim = (Vector2)(playerMovement.transform.position - muzzle);
        SplitShot shot = Instantiate(shotPrefab, muzzle, Quaternion.identity);
        shot.Launch(null, 0, aim, cannonColor, label: "IndexOutOfBounds");
    }

    private void FireFrom(SearchCell cell)
    {
        if (cell == null) return;
        Vector3 muzzle = cell.transform.position + Vector3.down * 0.5f;
        SplitShot shot = Instantiate(shotPrefab, muzzle, Quaternion.identity);
        shot.Launch(null, 0, Vector2.down, unknownColor, label: cell.name);
    }
}
