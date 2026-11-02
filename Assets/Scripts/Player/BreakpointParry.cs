using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The developer's native parry: set a breakpoint (Shift) and any enemy projectile
/// (IParryable) that reaches you during the short window is paused, then "stepped into":
/// a fleet bit is reflected back home (harmless; its bug rebuilds); a boss shot flies back and hits a frame.
/// A successful parry has no cooldown, so the 0 and the 1 can both be caught;
/// a whiffed one detaches the debugger for a moment.
/// </summary>
public class BreakpointParry : MonoBehaviour
{
    [SerializeField] private float window = 0.2f;
    [Tooltip("Seconds before you can parry again after a window that caught nothing.")]
    [SerializeField] private float missCooldown = 1f;
    [Tooltip("Projectiles within this distance of the developer are caught while the breakpoint is set.")]
    [SerializeField] private float catchRadius = 0.9f;
    [Tooltip("Real-time freeze on a catch, to sell the 'paused' moment.")]
    [SerializeField] private float hitStop = 0.06f;

    [Header("Feedback")]
    [Tooltip("The red breakpoint dot beside the developer.")]
    [SerializeField] private SpriteRenderer marker;
    [SerializeField] private Color armedColor = new Color32(0xf8, 0x51, 0x49, 0xff);
    [SerializeField] private Color cooldownColor = new Color32(0x48, 0x4f, 0x58, 0xff);
    [SerializeField] private FloatingText popupPrefab;
    [SerializeField] private RectTransform popupCanvas;

    private float windowEnd;
    private float readyTime;
    private bool windowOpen;
    private int catches;
    private Camera cam;

    // Copy of the active set, since catching a projectile changes its state.
    private readonly List<IParryable> candidates = new List<IParryable>();

    private void Awake() => cam = Camera.main;

    private void OnDisable()
    {
        windowOpen = false;
        if (marker != null) marker.enabled = false;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        bool pressed = kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame);
        if (pressed && !windowOpen && Time.time >= readyTime)
            Open();

        if (windowOpen)
        {
            CatchNearby();
            if (Time.time >= windowEnd) Close();
        }

        UpdateMarker();
    }

    /// <summary>Called by a projectile that touches the developer; true if the breakpoint caught it.</summary>
    public bool TryCatch(IParryable projectile)
    {
        if (!windowOpen) return false;
        return Catch(projectile);
    }

    private void Open()
    {
        windowOpen = true;
        windowEnd = Time.time + window;
        catches = 0;
    }

    private void Close()
    {
        windowOpen = false;
        readyTime = catches > 0 ? Time.time : Time.time + missCooldown;
    }

    private void CatchNearby()
    {
        candidates.Clear();
        candidates.AddRange(Parryables.Active);
        Vector2 center = transform.position;
        foreach (IParryable projectile in candidates)
        {
            if (projectile.IsDangerous && Vector2.Distance(projectile.Position, center) <= catchRadius)
                Catch(projectile);
        }
    }

    private bool Catch(IParryable projectile)
    {
        Vector3 at = projectile.Position;
        if (!projectile.StepInto()) return false;
        catches++;
        if (popupPrefab != null)
            popupPrefab.Spawn(popupCanvas, cam, at, $"● paused: {projectile.ParryLabel}", armedColor);
        StartCoroutine(HitStop());
        return true;
    }

    private IEnumerator HitStop()
    {
        if (Time.timeScale == 0f) yield break; // already paused (or another hit-stop running)
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(hitStop);
        // Only resume if nothing else (e.g. the game manager) paused the game meanwhile.
        if (enabled && Time.timeScale == 0f) Time.timeScale = 1f;
    }

    private void UpdateMarker()
    {
        if (windowOpen)
        {
            marker.enabled = true;
            marker.color = armedColor;
        }
        else if (Time.time < readyTime)
        {
            marker.enabled = true;
            marker.color = cooldownColor; // debugger detached
        }
        else
        {
            marker.enabled = false;
        }
    }
}
