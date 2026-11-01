using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the sequence of waves. Each wave is one or more formations, each with its own
/// layout, lane and throw pacing (every formation brings its own FleetThrower).
/// GameManager decides what happens between waves; this class only spawns, pauses
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
        [Tooltip("Terminal lines shown before this wave starts (not used for the first wave).")]
        [TextArea] public string intro;
        public FormationSpawn[] formations;
    }

    [SerializeField] private AlienFormation formationPrefab;
    [SerializeField] private Transform player;
    [SerializeField] private Wave[] waves;

    /// <summary>After any formation's march step.</summary>
    public event Action Stepped;
    /// <summary>(index of the wave just cleared, total waves). Not raised for the last wave.</summary>
    public event Action<int, int> WaveCleared;
    /// <summary>The last wave is cleared.</summary>
    public event Action AllWavesCleared;
    /// <summary>(index, total) when a wave spawns.</summary>
    public event Action<int, int> WaveStarted;

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
            spawned[i] = formation;
            active.Add(formation);
        }

        SetRunning(running);
        WaveStarted?.Invoke(index, waves.Length);
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
        active.Remove(formation);
        Destroy(formation.gameObject);

        if (active.Count > 0) return;

        if (CurrentWave + 1 < waves.Length) WaveCleared?.Invoke(CurrentWave, waves.Length);
        else AllWavesCleared?.Invoke();
    }
}
