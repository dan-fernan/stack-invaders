using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Miniboss: binary search. A bug hides in one cell of a sorted array. Shooting a cell is
/// a comparison: a miss shows &lt; or &gt; toward the bug and eliminates the half of the
/// array that can't contain it (grayed out, shots pass through), exactly as a binary
/// search discards half its range. Hitting the bug's cell is a find (1 damage).
/// Each search has a comparison budget; running out re-sorts the array (the bug moves,
/// hints clear) and every live cell fires at once. Live cells drop shots meanwhile,
/// so searching efficiently also means less incoming fire.
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

    [Header("Search")]
    [Tooltip("Finds needed to defeat it.")]
    [SerializeField] private int finds = 3;
    [Tooltip("Comparisons allowed per search (an optimal search of 16 cells needs at most 5).")]
    [SerializeField] private int comparisonBudget = 6;
    [Tooltip("Seconds the found bug / last hint stays visible before the re-sort scramble.")]
    [SerializeField] private float revealTime = 0.5f;
    [SerializeField] private float resortTime = 1.2f;
    [SerializeField] private OneShotAnimation popEffect;

    [Header("Attack")]
    [SerializeField] private SplitShot shotPrefab;
    [Tooltip("Seconds between shots at full HP / on the last find.")]
    [SerializeField] private Vector2 fireIntervalRange = new Vector2(0.9f, 0.5f);

    public event Action<int, int> HpChanged;
    public event Action<string> LabelChanged;
    public event Action<bool> StunnedChanged;
    public event Action Defeated;

    public int Hp { get; private set; }
    public int MaxHp => finds;
    public string Label => resorting ? "re-sorting..." : $"binarySearch()  cmp {used}/{comparisonBudget}";

    private SearchCell[] cells;
    private int target;
    private int lo, hi; // inclusive search range
    private int used;
    private bool resorting;
    private bool defeated;
    private float fireTimer;

    private void Awake()
    {
        Hp = finds;
        cells = new SearchCell[cellCount];
        float width = (cellCount - 1) * spacing;
        for (int i = 0; i < cellCount; i++)
        {
            cells[i] = Instantiate(cellPrefab, transform);
            cells[i].transform.localPosition = new Vector3(i * spacing - width / 2f, 0f, 0f);
            cells[i].Init(this, i);
        }
        NewSearch();
        fireTimer = fireIntervalRange.x;
    }

    public void SetTarget(Transform player) { } // cells fire straight down; no aiming needed

    private void Update()
    {
        if (defeated || resorting) return;

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            FireFrom(RandomLiveCell());
            float progress = 1f - (float)Hp / MaxHp;
            fireTimer = Mathf.Lerp(fireIntervalRange.x, fireIntervalRange.y, progress);
        }
    }

    /// <summary>A comparison: called when the player (or a parried shot) hits a live cell.</summary>
    public void Probe(SearchCell cell)
    {
        if (defeated || resorting || !cell.IsLive) return;
        used++;

        if (cell.Index == target)
        {
            Hp--;
            cell.Show(bugGlyph, foundColor, false);
            if (popEffect != null) popEffect.Spawn(cell.transform.position, foundColor);
            HpChanged?.Invoke(Hp, MaxHp);
            if (Hp <= 0) StartCoroutine(Defeat());
            else StartCoroutine(Resort(punish: false));
            return;
        }

        // Miss: point toward the bug and discard the half that can't contain it.
        if (cell.Index < target) lo = Mathf.Max(lo, cell.Index + 1);
        else hi = Mathf.Min(hi, cell.Index - 1);
        RefreshCells(hintedCell: cell);
        LabelChanged?.Invoke(Label);

        if (used >= comparisonBudget) StartCoroutine(Resort(punish: true));
    }

    private void RefreshCells(SearchCell hintedCell)
    {
        foreach (SearchCell c in cells)
        {
            bool live = c.Index >= lo && c.Index <= hi;
            if (c == hintedCell)
                c.Show(c.Index < target ? greaterGlyph : lessGlyph, hintColor, false);
            else if (!live && c.IsLive)
                c.Show(unknownGlyph, eliminatedColor, false);
        }
    }

    private void NewSearch()
    {
        target = UnityEngine.Random.Range(0, cellCount);
        lo = 0;
        hi = cellCount - 1;
        used = 0;
        foreach (SearchCell c in cells) c.Show(unknownGlyph, unknownColor, true);
        LabelChanged?.Invoke(Label);
    }

    /// <summary>The array re-sorts: the bug moves and the hints clear.</summary>
    private IEnumerator Resort(bool punish)
    {
        if (punish)
            foreach (SearchCell c in cells)
                if (c.IsLive) FireFrom(c);

        resorting = true;
        StunnedChanged?.Invoke(true);
        LabelChanged?.Invoke(Label);

        // Hold the result (the found bug, or the last hint) on screen for a moment.
        yield return new WaitForSeconds(revealTime);

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
        LabelChanged?.Invoke("return index;");
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
