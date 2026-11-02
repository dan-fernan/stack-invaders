using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boons every language can be offered:
///   compute  - Compute Upgrade (vertical scaling): +1 max heart per level.
///   ssd      - SSD upgrade: faster movement.
///   revert   - git revert: once per stage, the first lost heart comes back if you avoid
///              another hit for a few seconds.
///   threads  - Multithreading: every Nth attack fires twice.
///   firewall - Firewall: small static barriers above the developer absorb enemy shots;
///              rebuilt every stage.
/// </summary>
public class UniversalBoons : MonoBehaviour, IBoonReceiver, IStageReset
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerController movement;

    [Header("SSD upgrade")]
    [SerializeField] private float[] ssdSpeed = { 1.2f, 1.4f };

    [Header("git revert")]
    [Tooltip("Seconds without another hit before the heart comes back, per level.")]
    [SerializeField] private float[] revertDelay = { 5f, 3f };
    [SerializeField] private FloatingText popupPrefab;
    [SerializeField] private RectTransform popupCanvas;
    [SerializeField] private Color revertColor = new Color32(0xbc, 0x8c, 0xff, 0xff);

    [Header("Multithreading")]
    [Tooltip("Every Nth attack fires twice, per level.")]
    [SerializeField] private int[] extraShotEvery = { 3, 2 };

    [Header("Firewall")]
    [SerializeField] private FirewallBlock firewallPrefab;
    [Tooltip("Barrier x positions, in the order they're added (level 1 builds the first two).")]
    [SerializeField] private float[] firewallX = { -4.5f, 4.5f, 0f };
    [SerializeField] private int[] firewallCount = { 2, 3 };
    [SerializeField] private float firewallY = -2.75f;

    private IMultiShotWeapon weapon;
    private readonly List<FirewallBlock> firewalls = new List<FirewallBlock>();
    private int revertLevel;
    private bool revertUsedThisStage;
    private Coroutine pendingRevert;

    private void OnEnable() => health.Hit += OnHit;
    private void OnDisable() => health.Hit -= OnHit;

    /// <summary>Called once the language is chosen (Multithreading needs its weapon).</summary>
    public void SetLanguage(LanguageOption language)
    {
        weapon = null;
        foreach (MonoBehaviour behaviour in language.behaviours)
            if (behaviour is IMultiShotWeapon w) weapon = w;
    }

    public void ApplyBoon(string id, int level)
    {
        int i = level - 1;
        switch (id)
        {
            case "compute":
                health.AddMaxHeart();
                break;
            case "ssd":
                movement.SpeedMultiplier = ssdSpeed[Mathf.Min(i, ssdSpeed.Length - 1)];
                break;
            case "revert":
                revertLevel = level;
                break;
            case "threads":
                if (weapon != null) weapon.ExtraShotEvery = extraShotEvery[Mathf.Min(i, extraShotEvery.Length - 1)];
                break;
            case "firewall":
                int count = Mathf.Min(firewallCount[Mathf.Min(i, firewallCount.Length - 1)], firewallX.Length);
                while (firewalls.Count < count)
                {
                    FirewallBlock wall = Instantiate(firewallPrefab, new Vector3(firewallX[firewalls.Count], firewallY, 0f), Quaternion.identity);
                    wall.name = $"Firewall {firewalls.Count + 1}";
                    firewalls.Add(wall);
                }
                break;
            default:
                Debug.LogWarning($"UniversalBoons: unknown boon '{id}'");
                break;
        }
    }

    /// <summary>Start of each stage: firewalls rebuilt, git revert available again.</summary>
    public void ResetForStage()
    {
        foreach (FirewallBlock wall in firewalls) wall.Rebuild();
        revertUsedThisStage = false;
        if (pendingRevert != null) { StopCoroutine(pendingRevert); pendingRevert = null; }
    }

    // ---------- git revert ----------

    private void OnHit()
    {
        if (revertLevel == 0) return;
        // A second hit while a revert is pending cancels it ("the commit was overwritten").
        if (pendingRevert != null)
        {
            StopCoroutine(pendingRevert);
            pendingRevert = null;
            Popup("revert aborted: conflict");
            return;
        }
        if (revertUsedThisStage || health.IsDead) return;
        revertUsedThisStage = true;
        pendingRevert = StartCoroutine(Revert(revertDelay[Mathf.Min(revertLevel - 1, revertDelay.Length - 1)]));
        Popup("git revert pending...");
    }

    private IEnumerator Revert(float delay)
    {
        yield return new WaitForSeconds(delay);
        pendingRevert = null;
        if (health.Heal(1)) Popup("git revert HEAD: +1 heart");
    }

    private void Popup(string text)
    {
        if (popupPrefab != null && popupCanvas != null)
            popupPrefab.Spawn(popupCanvas, Camera.main, health.transform.position + Vector3.up * 1.2f, text, revertColor, 1.2f);
    }
}
