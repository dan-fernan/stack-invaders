using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Java's HUD line: "JIT [#####-----]  GC 7s". Event-driven: the text is only
/// rebuilt when the heat bar or the GC countdown actually changes.
/// </summary>
public class JavaHud : MonoBehaviour
{
    [SerializeField] private JavaWeapon weapon;
    [SerializeField] private GarbageCollector gc;
    [SerializeField] private Text label;
    [SerializeField] private float collectedMessageTime = 1f;

    private string heatBar = "";
    private string gcText = "";
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
        heatBar = "JIT [" + new string('#', filled) + new string('-', total - filled) + "]";
        Redraw();
    }

    private void OnGcSeconds(int seconds)
    {
        if (message != null) return; // keep showing the collection message
        gcText = seconds > 0 ? $"GC {seconds}s" : "GC ready";
        Redraw();
    }

    private void OnCollected(int count)
    {
        if (message != null) StopCoroutine(message);
        message = StartCoroutine(ShowCollected(count));
    }

    private IEnumerator ShowCollected(int count)
    {
        gcText = count == 1 ? "System.gc() freed 1 bit" : $"System.gc() freed {count} bits";
        Redraw();
        yield return new WaitForSeconds(collectedMessageTime);
        message = null;
        gcText = gc.IsReady ? "GC ready" : "GC";
        Redraw();
    }

    private void Redraw() => label.text = heatBar + "  " + gcText;
}
