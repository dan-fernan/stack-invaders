using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows "WAVE 2/3" at the top of the screen; redrawn only when a wave starts.</summary>
public class WaveHud : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Text label;

    private void OnEnable()
    {
        waveManager.WaveStarted += Show;
        if (waveManager.CurrentWave >= 0) Show(waveManager.CurrentWave, waveManager.WaveCount);
    }

    private void OnDisable() => waveManager.WaveStarted -= Show;

    private void Show(int index, int total) => label.text = $"WAVE {index + 1}/{total}";
}
