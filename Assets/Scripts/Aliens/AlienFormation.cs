using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns the bug grid and marches it side to side, dropping a row at each edge.
/// Movement is in discrete steps like the original arcade game; the time between
/// steps shrinks as aliens die (assignment requirement: "remaining aliens speed up").
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

    [Header("Grid")]
    [SerializeField] private Alien alienPrefab;
    [Tooltip("Rows from top to bottom.")]
    [SerializeField] private AlienRow[] rows;
    [SerializeField] private int columns = 11;
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

    /// <summary>Raised when the last alien is destroyed.</summary>
    public event Action Cleared;

    /// <summary>Raised after every march step (the only time the formation moves).</summary>
    public event Action Stepped;

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

    private static readonly Vector2Int[] NeighborOffsets =
        { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    private readonly List<Alien> aliens = new List<Alien>();
    private Alien[,] grid;
    private int totalSpawned;
    private int direction = 1;
    private float stepTimer;
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        // Kinematic body: the alien colliders become one compound body that we move
        // by hand, rather than static colliders being teleported every step.
        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
    }

    private void Start()
    {
        Spawn();
    }

    private void Spawn()
    {
        grid = new Alien[rows.Length, columns];
        float width = (columns - 1) * spacing.x;
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 localPos = new Vector3(c * spacing.x - width / 2f, -r * spacing.y, 0f);
                Alien alien = Instantiate(alienPrefab, transform.TransformPoint(localPos), Quaternion.identity, transform);
                alien.name = $"Bug_{r}_{c}";
                alien.Init(rows[r], r, c);
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

        float halfView = cam.orthographicSize * cam.aspect - edgePadding;

        bool hitEdge = (direction > 0 && maxX + stepX > cam.transform.position.x + halfView)
                    || (direction < 0 && minX - stepX < cam.transform.position.x - halfView);

        if (hitEdge)
        {
            transform.position += Vector3.down * stepDown;
            direction = -direction;
        }
        else
        {
            transform.position += Vector3.right * (stepX * direction);
        }

        foreach (Alien alien in aliens)
            alien.AdvanceFrame();

        Stepped?.Invoke();
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

    private void OnAlienKilled(Alien alien)
    {
        alien.Killed -= OnAlienKilled;
        aliens.Remove(alien);
        grid[alien.Row, alien.Column] = null;
        if (aliens.Count == 0) Cleared?.Invoke();
    }
}
