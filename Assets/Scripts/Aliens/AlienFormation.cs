using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// One bug formation. Spawned by the WaveManager, which picks its layout and lane.
/// Marches side to side within its lane, dropping toward the player at each edge.
/// Movement is in discrete steps like the original arcade game; the time between
/// steps shrinks as this formation's aliens die (assignment requirement:
/// "remaining aliens speed up as their compatriots are destroyed").
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AlienFormation : MonoBehaviour
{
    [Serializable]
    public class AlienRow
    {
        public Sprite frameA;
        public Sprite frameB;
        [Tooltip("White silhouettes used when this alien flashes (telegraphing a throw).")]
        public Sprite flashA;
        public Sprite flashB;
        [Tooltip("Body color; thrown bits are tinted with it.")]
        public Color color = Color.white;
        public int points = 10;
    }

    /// <summary>Which part of the screen the formation marches across.</summary>
    public enum Lane { Full, LeftHalf, RightHalf }

    [Header("Grid")]
    [SerializeField] private Alien alienPrefab;
    [Tooltip("Bug types a layout can use, referenced by index (e.g. 0 = beetle, 1 = ladybug, 2 = spider).")]
    [FormerlySerializedAs("rows")]
    [SerializeField] private AlienRow[] rowTypes;
    [SerializeField] private Vector2 spacing = new Vector2(1f, 0.75f);

    [Header("March")]
    [Tooltip("Horizontal distance per step (0.25 = 4 pixels at 16 PPU).")]
    [SerializeField] private float stepX = 0.25f;
    [Tooltip("Distance dropped toward the player each time the formation hits an edge (0.5 = 8 pixels).")]
    [SerializeField] private float stepDown = 0.5f;
    [Tooltip("Seconds between steps with a full formation.")]
    [SerializeField] private float slowestStepInterval = 0.45f;
    [Tooltip("Seconds between steps when one alien is left.")]
    [SerializeField] private float fastestStepInterval = 0.04f;
    [SerializeField] private float edgePadding = 0.25f;
    [Tooltip("Half-width of the gap at the screen's center that half-lane formations bounce off.")]
    [SerializeField] private float midpointGap = 0.125f;
    [Tooltip("Minimum space kept between this formation's bottom and the top of the formation it holds above.")]
    [SerializeField] private float holdGap = 0.75f;

    /// <summary>Raised when the last alien is destroyed.</summary>
    public event Action<AlienFormation> Cleared;

    /// <summary>Raised after every march step (the only time the formation moves).</summary>
    public event Action<AlienFormation> Stepped;

    public int AliveCount => aliens.Count;

    /// <summary>Living aliens (including thrown ones) as a fraction of the starting count.</summary>
    public float FractionRemaining => totalSpawned == 0 ? 1f : (float)aliens.Count / totalSpawned;

    /// <summary>
    /// World-space bottom edge of the lowest alien present in the formation
    /// (thrown aliens are excluded). float.MaxValue if none are present.
    /// </summary>
    public float LowestY
    {
        get
        {
            float lowest = float.MaxValue;
            foreach (Alien alien in aliens)
                if (alien.IsPresent)
                    lowest = Mathf.Min(lowest, alien.Bounds.min.y);
            return lowest;
        }
    }

    /// <summary>World-space top edge of the highest present alien. float.MinValue if none are present.</summary>
    public float HighestY
    {
        get
        {
            float highest = float.MinValue;
            foreach (Alien alien in aliens)
                if (alien.IsPresent)
                    highest = Mathf.Max(highest, alien.Bounds.max.y);
            return highest;
        }
    }

    private static readonly Vector2Int[] NeighborOffsets =
        { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    private readonly List<Alien> aliens = new List<Alien>();
    private Alien[,] grid;
    private int totalSpawned;
    private int direction = 1;
    private float stepTimer;
    private Lane lane;
    private AlienFormation holdAbove;
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        // Kinematic body: the alien colliders become one compound body that we move
        // by hand, rather than static colliders being teleported every step.
        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>
    /// Builds the grid. Call once, right after instantiating.
    /// </summary>
    /// <param name="rowLayout">Bug type index for each row, top to bottom.</param>
    /// <param name="holdAboveFormation">If set, this formation won't drop onto that one while it has bugs.</param>
    public void Configure(int[] rowLayout, int columns, Lane marchLane, AlienFormation holdAboveFormation)
    {
        lane = marchLane;
        holdAbove = holdAboveFormation;

        grid = new Alien[rowLayout.Length, columns];
        float width = (columns - 1) * spacing.x;
        for (int r = 0; r < rowLayout.Length; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 localPos = new Vector3(c * spacing.x - width / 2f, -r * spacing.y, 0f);
                Alien alien = Instantiate(alienPrefab, transform.TransformPoint(localPos), Quaternion.identity, transform);
                alien.name = $"Bug_{r}_{c}";
                alien.Init(rowTypes[rowLayout[r]], r, c);
                alien.Killed += OnAlienKilled;
                aliens.Add(alien);
                grid[r, c] = alien;
            }
        }
        totalSpawned = aliens.Count;
    }

    private void Update()
    {
        if (aliens.Count == 0) return;

        stepTimer += Time.deltaTime;
        float interval = CurrentStepInterval();
        if (stepTimer < interval) return;
        stepTimer -= interval;

        Step();
    }

    /// <summary>
    /// Interpolates between slowest and fastest by fraction of aliens remaining.
    /// Squaring the fraction makes the speed-up ramp harder near the end,
    /// matching the arcade feel where the last few bugs get frantic.
    /// </summary>
    private float CurrentStepInterval()
    {
        float remaining = FractionRemaining;
        return Mathf.Lerp(fastestStepInterval, slowestStepInterval, remaining * remaining);
    }

    private void Step()
    {
        // Every remaining alien is thrown: hold position until one comes home.
        if (!GetHorizontalExtents(out float minX, out float maxX)) return;

        GetLaneBounds(out float laneMin, out float laneMax);
        bool hitEdge = (direction > 0 && maxX + stepX > laneMax)
                    || (direction < 0 && minX - stepX < laneMin);

        if (hitEdge)
        {
            if (CanDrop()) transform.position += Vector3.down * stepDown;
            direction = -direction;
        }
        else
        {
            transform.position += Vector3.right * (stepX * direction);
        }

        foreach (Alien alien in aliens)
            alien.AdvanceFrame();

        Stepped?.Invoke(this);
    }

    /// <summary>Back formations hold above the front one until it's cleared.</summary>
    private bool CanDrop()
    {
        if (holdAbove == null || holdAbove.AliveCount == 0) return true; // destroyed or empty
        return LowestY - stepDown >= holdAbove.HighestY + holdGap;
    }

    private void GetLaneBounds(out float min, out float max)
    {
        float center = cam.transform.position.x;
        float half = cam.orthographicSize * cam.aspect - edgePadding;
        switch (lane)
        {
            case Lane.LeftHalf:  min = center - half;       max = center - midpointGap; break;
            case Lane.RightHalf: min = center + midpointGap; max = center + half;      break;
            default:             min = center - half;       max = center + half;      break;
        }
    }

    /// <summary>
    /// World-space left/right edges of the sprites of aliens present in the formation.
    /// Returns false if no alien is present (all remaining ones are thrown).
    /// </summary>
    private bool GetHorizontalExtents(out float minX, out float maxX)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        bool any = false;
        foreach (Alien alien in aliens)
        {
            if (!alien.IsPresent) continue;
            Bounds b = alien.Bounds;
            if (b.min.x < minX) minX = b.min.x;
            if (b.max.x > maxX) maxX = b.max.x;
            any = true;
        }
        return any;
    }

    /// <summary>
    /// Picks a random present alien that has a present grid neighbor (left/right/above/below).
    /// The neighbor "picks it up" and throws it at the player.
    /// </summary>
    public bool TryPickThrow(out Alien target, out Alien neighbor)
    {
        var candidates = new List<(Alien target, Alien neighbor)>();
        foreach (Alien alien in aliens)
        {
            if (!alien.IsPresent) continue;
            Alien n = RandomPresentNeighbor(alien);
            if (n != null) candidates.Add((alien, n));
        }

        if (candidates.Count == 0)
        {
            target = neighbor = null;
            return false;
        }

        (target, neighbor) = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private Alien RandomPresentNeighbor(Alien alien)
    {
        int start = UnityEngine.Random.Range(0, NeighborOffsets.Length);
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            Vector2Int o = NeighborOffsets[(start + i) % NeighborOffsets.Length];
            int r = alien.Row + o.y, c = alien.Column + o.x;
            if (r < 0 || r >= grid.GetLength(0) || c < 0 || c >= grid.GetLength(1)) continue;
            Alien n = grid[r, c];
            if (n != null && n.IsPresent) return n;
        }
        return null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Debug only: kills every bug in this formation.</summary>
    public void DebugKillAll()
    {
        foreach (Alien alien in new List<Alien>(aliens))
            alien.Kill();
    }
#endif

    private void OnAlienKilled(Alien alien)
    {
        alien.Killed -= OnAlienKilled;
        aliens.Remove(alien);
        grid[alien.Row, alien.Column] = null;
        if (aliens.Count == 0) Cleared?.Invoke(this);
    }
}
