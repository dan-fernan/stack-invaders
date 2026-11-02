using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Java's HUD, two short right-aligned lines in the top-right corner (kept narrow so they
/// never reach the centered wave/boss HUD): "JIT [#####-----]" and "GC 7s" / "GC ready".
/// Event-driven: each line is only rebuilt when its value actually changes.
/// </summary>
public class JavaHud : MonoBehaviour
{
    [SerializeField] private JavaWeapon weapon;
    [SerializeField] private GarbageCollector gc;
    [FormerlySerializedAs("label")]
    [SerializeField] private Text jitLabel;
    [SerializeField] private Text gcLabel;
    [SerializeField] private float collectedMessageTime = 1f;
    [SerializeField] private float deoptimizedMessageTime = 0.6f;

    private Coroutine message;
    private Coroutine jitMessage;
    private int heatFilled, heatTotal;

    private void OnEnable()
    {
        weapon.HeatSegmentsChanged += OnHeat;
        weapon.Deoptimized += OnDeoptimized;
        gc.SecondsLeftChanged += OnGcSeconds;
        gc.Collected += OnCollected;
    }

    private void OnDisable()
    {
        weapon.HeatSegmentsChanged -= OnHeat;
        weapon.Deoptimized -= OnDeoptimized;
        gc.SecondsLeftChanged -= OnGcSeconds;
        gc.Collected -= OnCollected;
        // A message cut short by disabling (its coroutine stops too) falls back to the bar.
        if (jitMessage != null) { jitMessage = null; ShowHeat(); }
    }

    private void OnHeat(int filled, int total)
    {
        heatFilled = filled;
        heatTotal = total;
        if (jitMessage == null) ShowHeat();
    }

    private void ShowHeat() =>
        jitLabel.text = "JIT [" + new string('#', heatFilled) + new string('-', heatTotal - heatFilled) + "]";

    private void OnDeoptimized()
    {
        if (jitMessage != null) StopCoroutine(jitMessage);
        jitMessage = StartCoroutine(ShowDeoptimized());
    }

    /// <summary>The bar briefly reads "deoptimized", then shows the reduced heat.</summary>
    private IEnumerator ShowDeoptimized()
    {
        jitLabel.text = "JIT deoptimized";
        yield return new WaitForSeconds(deoptimizedMessageTime);
        jitMessage = null;
        ShowHeat();
    }

    private void OnGcSeconds(int seconds)
    {
        if (message != null) return; // keep showing the collection message
        gcLabel.text = seconds > 0 ? $"GC {seconds}s" : "GC ready";
    }

    private void OnCollected(int count)
    {
        if (message != null) StopCoroutine(message);
        message = StartCoroutine(ShowCollected(count));
    }

    private IEnumerator ShowCollected(int count)
    {
        gcLabel.text = count == 1 ? "freed 1 bit" : $"freed {count} bits";
        yield return new WaitForSeconds(collectedMessageTime);
        message = null;
        gcLabel.text = gc.IsReady ? "GC ready" : "GC";
    }
}
