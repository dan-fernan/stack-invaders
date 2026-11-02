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

    private Coroutine message;

    private void OnEnable()
    {
        weapon.HeatSegmentsChanged += OnHeat;
        gc.SecondsLeftChanged += OnGcSeconds;
        gc.Collected += OnCollected;
    }

    private void OnDisable()
    {
        weapon.HeatSegmentsChanged -= OnHeat;
        gc.SecondsLeftChanged -= OnGcSeconds;
        gc.Collected -= OnCollected;
    }

    private void OnHeat(int filled, int total)
    {
        jitLabel.text = "JIT [" + new string('#', filled) + new string('-', total - filled) + "]";
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
