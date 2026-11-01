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
        public int points = 10;
    }

    [Header("Grid")]
    [SerializeField] private Alien alienPrefab;
    [Tooltip("Rows from top to bottom.")]
    [SerializeField] private AlienRow[] rows;
    [SerializeField] private int columns = 11;
    [SerializeField] private Vector2 spacing = new Vector2(1f, 0.75f);

    [Header("March")]
    [Tooltip("Horizontal distance per step (0.125 = 2 pixels at 16 PPU).")]
    [SerializeField] private float stepX = 0.125f;
    [Tooltip("Distance dropped when the formation hits an edge.")]
    [SerializeField] private float stepDown = 0.25f;
    [Tooltip("Seconds between steps with a full formation.")]
    [SerializeField] private float slowestStepInterval = 0.45f;
    [Tooltip("Seconds between steps when one alien is left.")]
    [SerializeField] private float fastestStepInterval = 0.04f;
    [SerializeField] private float edgePadding = 0.25f;

    /// <summary>Raised when the last alien is destroyed.</summary>
    public event Action Cleared;

    public int AliveCount => aliens.Count;

    private readonly List<Alien> aliens = new List<Alien>();
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
        float width = (columns - 1) * spacing.x;
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 localPos = new Vector3(c * spacing.x - width / 2f, -r * spacing.y, 0f);
                Alien alien = Instantiate(alienPrefab, transform);
                alien.transform.localPosition = localPos;
                alien.name = $"Bug_{r}_{c}";
                alien.Init(rows[r].frameA, rows[r].frameB, rows[r].points);
                alien.Killed += OnAlienKilled;
                aliens.Add(alien);
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
        float remaining = (float)aliens.Count / totalSpawned;
        return Mathf.Lerp(fastestStepInterval, slowestStepInterval, remaining * remaining);
    }

    private void Step()
    {
        float halfView = cam.orthographicSize * cam.aspect - edgePadding;
        GetHorizontalExtents(out float minX, out float maxX);

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
    }

    /// <summary>World-space left/right edges of the living aliens' sprites.</summary>
    private void GetHorizontalExtents(out float minX, out float maxX)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        foreach (Alien alien in aliens)
        {
            Bounds b = alien.Bounds;
            if (b.min.x < minX) minX = b.min.x;
            if (b.max.x > maxX) maxX = b.max.x;
        }
    }

    private void OnAlienKilled(Alien alien)
    {
        alien.Killed -= OnAlienKilled;
        aliens.Remove(alien);
        if (aliens.Count == 0) Cleared?.Invoke();
    }
}
