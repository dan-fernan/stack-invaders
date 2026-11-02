using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hades-style boons. Wave n (1-based) drops n floppy disks, spread evenly over the
/// wave's kills. Each disk offers up to 3 of the language's boons; picking one you
/// already have levels it up. Boons last the whole run.
/// GameManager pauses the game and shows the offer; this class owns the pool, levels,
/// drops, the offer menu and the HUD line.
/// </summary>
public class BoonManager : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private FloppyDisk diskPrefab;
    [Tooltip("World y the disks fall to (the developer's feet).")]
    [SerializeField] private float floorY = -4.875f;
    [SerializeField] private int offerSize = 3;
    [Tooltip("Shows installed boons, e.g. \"SB2 JU1\".")]
    [SerializeField] private Text hudLabel;

    /// <summary>A disk was picked up; GameManager should pause and show an offer.</summary>
    public event Action DiskPickedUp;

    public int PendingOffers { get; private set; }

    private BoonDefinition[] pool = Array.Empty<BoonDefinition>();
    private IBoonReceiver receiver;
    private readonly Dictionary<string, int> levels = new Dictionary<string, int>();
    private readonly List<BoonDefinition> offer = new List<BoonDefinition>();
    private readonly List<FloppyDisk> disksOnField = new List<FloppyDisk>();
    private int selected;

    // Drop schedule for the current wave.
    private readonly Queue<int> dropAtKill = new Queue<int>();
    private int killsThisWave;

    private void OnEnable() => waveManager.BugKilled += OnBugKilled;
    private void OnDisable() => waveManager.BugKilled -= OnBugKilled;

    /// <summary>Called once the language is chosen.</summary>
    public void SetLanguage(LanguageOption language)
    {
        pool = language.boons ?? Array.Empty<BoonDefinition>();
        receiver = language.boonReceiver as IBoonReceiver;
        levels.Clear();
        RefreshHud();
    }

    public int LevelOf(string id) => levels.TryGetValue(id, out int l) ? l : 0;

    // ---------- Drops ----------

    /// <summary>A wave started: wave n (0-based index) drops n + 1 disks over its kills.</summary>
    public void BeginWave(int waveIndex)
    {
        dropAtKill.Clear();
        killsThisWave = 0;
        if (pool.Length == 0) return;

        int drops = waveIndex + 1;
        int bugs = waveManager.CurrentWaveBugCount;
        for (int i = 1; i <= drops; i++)
            dropAtKill.Enqueue(Mathf.Max(1, Mathf.RoundToInt(bugs * i / (float)(drops + 1))));
    }

    /// <summary>A boss stage: no drops.</summary>
    public void BeginBoss() => dropAtKill.Clear();

    private void OnBugKilled(Vector3 position)
    {
        killsThisWave++;
        while (dropAtKill.Count > 0 && killsThisWave >= dropAtKill.Peek())
        {
            dropAtKill.Dequeue();
            SpawnDisk(position);
        }
    }

    private void SpawnDisk(Vector3 position)
    {
        FloppyDisk disk = Instantiate(diskPrefab, position, Quaternion.identity);
        disk.Init(floorY);
        disk.PickedUp += OnDiskPickedUp;
        disksOnField.Add(disk);
    }

    private void OnDiskPickedUp(FloppyDisk disk)
    {
        disk.PickedUp -= OnDiskPickedUp;
        disksOnField.Remove(disk);
        PendingOffers++;
        DiskPickedUp?.Invoke();
    }

    /// <summary>Wave over: disks still on the floor (and drops not yet made) are collected automatically.</summary>
    public void CollectRemaining()
    {
        foreach (FloppyDisk disk in new List<FloppyDisk>(disksOnField))
        {
            disk.PickedUp -= OnDiskPickedUp;
            Destroy(disk.gameObject);
            PendingOffers++;
        }
        disksOnField.Clear();
        PendingOffers += dropAtKill.Count;
        dropAtKill.Clear();
        // Nothing left to offer if every boon is maxed.
        if (!AnyUpgradeLeft()) PendingOffers = 0;
    }

    // ---------- Offers ----------

    /// <summary>Rolls up to offerSize boons that aren't maxed yet. False if none are left.</summary>
    public bool PrepareOffer()
    {
        offer.Clear();
        selected = 0;
        var available = new List<BoonDefinition>();
        foreach (BoonDefinition b in pool)
            if (LevelOf(b.id) < b.MaxLevel) available.Add(b);

        while (offer.Count < offerSize && available.Count > 0)
        {
            int i = UnityEngine.Random.Range(0, available.Count);
            offer.Add(available[i]);
            available.RemoveAt(i);
        }
        if (offer.Count == 0) PendingOffers = 0;
        return offer.Count > 0;
    }

    /// <summary>Menu keys; returns true once a boon is installed.</summary>
    public bool HandleInput(Keyboard kb, out bool changed)
    {
        changed = false;
        int move = 0;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) move = -1;
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) move = 1;
        if (move != 0)
        {
            selected = (selected + move + offer.Count) % offer.Count;
            changed = true;
        }
        for (int i = 0; i < offer.Count; i++)
        {
            if (kb[Key.Digit1 + i].wasPressedThisFrame)
            {
                selected = i;
                Install(offer[i]);
                return true;
            }
        }
        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
        {
            Install(offer[selected]);
            return true;
        }
        return false;
    }

    /// <summary>Installs (or levels up) a boon.</summary>
    public void Install(BoonDefinition boon)
    {
        int level = LevelOf(boon.id) + 1;
        levels[boon.id] = level;
        PendingOffers = Mathf.Max(0, PendingOffers - 1);
        receiver?.ApplyBoon(boon.id, level);
        RefreshHud();
    }

    public string RenderOffer()
    {
        var sb = new StringBuilder("Floppy disk found!\n$ install --dependency\n\n");
        for (int i = 0; i < offer.Count; i++)
        {
            BoonDefinition b = offer[i];
            int next = LevelOf(b.id) + 1;
            sb.Append(i == selected ? "> " : "  ");
            sb.Append($"[{i + 1}] {b.displayName,-12} Lv{next}\n");
        }
        BoonDefinition chosen = offer[selected];
        sb.Append($"\n  {chosen.levelDescriptions[LevelOf(chosen.id)]}\n");
        if (PendingOffers > 1) sb.Append($"\n  ({PendingOffers - 1} more disk{(PendingOffers > 2 ? "s" : "")} after this)\n");
        sb.Append("\nW/S to choose, SPACE to install");
        return sb.ToString();
    }

    private bool AnyUpgradeLeft()
    {
        foreach (BoonDefinition b in pool)
            if (LevelOf(b.id) < b.MaxLevel) return true;
        return false;
    }

    private void RefreshHud()
    {
        if (hudLabel == null) return;
        var sb = new StringBuilder();
        foreach (BoonDefinition b in pool)
        {
            int l = LevelOf(b.id);
            if (l == 0) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(b.tag).Append(l);
        }
        hudLabel.text = sb.ToString();
    }
}
