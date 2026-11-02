using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the sequence of waves. Each wave is one or more formations, each with its own
/// layout, lane and throw pacing (every formation brings its own FleetThrower).
/// GameManager decides the order of waves and bosses; this class only spawns, pauses
/// and reports.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Serializable]
    public class FormationSpawn
    {
        public string name = "Middle";
        [Tooltip("Where the formation's top-center starts.")]
        public Vector2 position = new Vector2(0f, 4f);
        [Tooltip("Bug type per row, top to bottom (0 = beetle, 1 = ladybug, 2 = spider).")]
        public int[] rowLayout = { 0, 1, 1, 2, 2 };
        public int columns = 11;
        public AlienFormation.Lane lane = AlienFormation.Lane.Full;
        [Tooltip("Index (within this wave) of a formation this one must stay above until it's cleared. -1 = none.")]
        public int holdAboveIndex = -1;
        [Tooltip("Multiplies the delay between this formation's throws (1.5 = less often).")]
        public float throwIntervalMultiplier = 1f;
    }

    [Serializable]
    public class Wave
    {
        public string name = "Wave";
        public FormationSpawn[] formations;
    }

    [SerializeField] private AlienFormation formationPrefab;
    [SerializeField] private Transform player;
    [SerializeField] private Wave[] waves;

    /// <summary>After any formation's march step.</summary>
    public event Action Stepped;
    /// <summary>(index of the wave just cleared, total waves).</summary>
    public event Action<int, int> WaveCleared;
    /// <summary>(index, total) when a wave spawns.</summary>
    public event Action<int, int> WaveStarted;
    /// <summary>A bug in the current wave died, at this position.</summary>
    public event Action<Vector3> BugKilled;

    /// <summary>Bugs the current wave started with.</summary>
    public int CurrentWaveBugCount { get; private set; }

    public int WaveCount => waves.Length;
    public int CurrentWave { get; private set; } = -1;
    public Wave Current => waves[CurrentWave];

    /// <summary>Total bugs across every wave.</summary>
    public int TotalBugs
    {
        get
        {
            int total = 0;
            foreach (Wave wave in waves)
                foreach (FormationSpawn f in wave.formations)
                    total += f.rowLayout.Length * f.columns;
            return total;
        }
    }

    /// <summary>Bottom edge of the lowest bug present in any active formation.</summary>
    public float LowestY
    {
        get
        {
            float lowest = float.MaxValue;
            foreach (AlienFormation f in active)
                lowest = Mathf.Min(lowest, f.LowestY);
            return lowest;
        }
    }

    private readonly List<AlienFormation> active = new List<AlienFormation>();
    private bool running;

    /// <summary>Spawns wave <paramref name="index"/>. Formations start paused until SetRunning(true).</summary>
    public void StartWave(int index)
    {
        CurrentWave = index;
        Wave wave = waves[index];
        CurrentWaveBugCount = 0;
        foreach (FormationSpawn f in wave.formations) CurrentWaveBugCount += f.rowLayout.Length * f.columns;
        var spawned = new AlienFormation[wave.formations.Length];

        for (int i = 0; i < wave.formations.Length; i++)
        {
            FormationSpawn spec = wave.formations[i];
            AlienFormation formation = Instantiate(formationPrefab, spec.position, Quaternion.identity, transform);
            formation.name = $"{wave.name} - {spec.name}";
            AlienFormation holdAbove = spec.holdAboveIndex >= 0 ? spawned[spec.holdAboveIndex] : null;
            formation.Configure(spec.rowLayout, spec.columns, spec.lane, holdAbove);
            formation.GetComponent<FleetThrower>().Init(player, spec.throwIntervalMultiplier);
            formation.Stepped += OnFormationStepped;
            formation.Cleared += OnFormationCleared;
            formation.AlienKilled += OnAlienKilled;
            spawned[i] = formation;
            active.Add(formation);
        }

        SetRunning(running);
        WaveStarted?.Invoke(index, waves.Length);
    }

    /// <summary>The current wave's formations (for the entrance animation).</summary>
    public Transform[] ActiveFormationTransforms()
    {
        var result = new Transform[active.Count];
        for (int i = 0; i < active.Count; i++) result[i] = active[i].transform;
        return result;
    }

    /// <summary>Pauses or resumes marching and throwing for every active formation.</summary>
    public void SetRunning(bool run)
    {
        running = run;
        foreach (AlienFormation formation in active)
        {
            formation.enabled = run;
            formation.GetComponent<FleetThrower>().enabled = run;
        }
    }

    private void OnFormationStepped(AlienFormation formation) => Stepped?.Invoke();

    private void OnAlienKilled(Alien alien) => BugKilled?.Invoke(alien.transform.position);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Debug only: kills every bug in the current wave.</summary>
    public void DebugClearWave()
    {
        foreach (AlienFormation formation in new List<AlienFormation>(active))
            formation.DebugKillAll();
    }
#endif

    private void OnFormationCleared(AlienFormation formation)
    {
        formation.Stepped -= OnFormationStepped;
        formation.Cleared -= OnFormationCleared;
        formation.AlienKilled -= OnAlienKilled;
        active.Remove(formation);
        Destroy(formation.gameObject);

        if (active.Count == 0) WaveCleared?.Invoke(CurrentWave, waves.Length);
    }
}
