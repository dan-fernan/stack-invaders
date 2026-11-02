using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Between-stage transitions, played over the (paused) game on unscaled time:
///   - Wave: a banner slides across the middle ("WAVE 2/3" + a terminal line) while the
///     next formation marches in from above.
///   - Boss warning: the screen dims, red hazard stripes frame a flashing "!! WARNING !!"
///     banner naming the boss, the camera shakes on each flash, and the boss descends.
/// GameManager supplies what to move in; this class only animates.
/// </summary>
public class StageTransition : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private CanvasGroup group;
    [Tooltip("Dark band across the middle of the screen behind the text.")]
    [SerializeField] private RectTransform band;
    [SerializeField] private Image bandImage;
    [SerializeField] private Text title;
    [SerializeField] private Text subtitle;
    [Tooltip("Full-screen dim, used by the boss warning.")]
    [SerializeField] private Image dim;
    [Tooltip("Hazard stripes above and below the band (boss warning only).")]
    [SerializeField] private Image stripesTop;
    [SerializeField] private Image stripesBottom;
    [SerializeField] private Transform cameraToShake;

    [Header("Look")]
    [SerializeField] private Color waveTitleColor = new Color32(0x39, 0xd3, 0x53, 0xff);
    [SerializeField] private Color warningColor = new Color32(0xf8, 0x51, 0x49, 0xff);
    [SerializeField] private Color subtitleColor = new Color32(0x8b, 0x94, 0x9e, 0xff);
    [SerializeField] private Color bandColor = new Color32(0x0d, 0x11, 0x17, 0xe6);

    [Header("Timing")]
    [SerializeField] private float waveDuration = 1.6f;
    [SerializeField] private float warningDuration = 2.6f;
    [SerializeField] private float slideTime = 0.25f;
    [Tooltip("How far above its spot a stage starts when it moves in (world units).")]
    [SerializeField] private float entranceHeight = 6f;
    [SerializeField] private int shakePixels = 2;

    private const float UiWidth = 320f;

    private void Awake() => Hide();

    private void Hide()
    {
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>Light transition into a wave; <paramref name="arrivals"/> march down into place.</summary>
    public IEnumerator PlayWave(string heading, string line, Transform[] arrivals)
    {
        Show(heading, line, waveTitleColor, warning: false);
        yield return Run(waveDuration, arrivals, warning: false);
        Hide();
    }

    /// <summary>Boss warning transition; <paramref name="arrival"/> descends into place.</summary>
    public IEnumerator PlayBossWarning(string bossName, string line, Transform arrival)
    {
        Show("!! WARNING !!", $"{bossName} incoming\n{line}", warningColor, warning: true);
        yield return Run(warningDuration, new[] { arrival }, warning: true);
        Hide();
    }

    private void Show(string heading, string line, Color titleColor, bool warning)
    {
        gameObject.SetActive(true);
        group.alpha = 1f;
        title.text = heading;
        title.color = titleColor;
        subtitle.text = line;
        subtitle.color = warning ? Color.Lerp(warningColor, Color.white, 0.4f) : subtitleColor;
        bandImage.color = bandColor;
        dim.enabled = warning;
        stripesTop.enabled = stripesBottom.enabled = warning;
        stripesTop.color = stripesBottom.color = warningColor;
    }

    private IEnumerator Run(float duration, Transform[] arrivals, bool warning)
    {
        // Everything that moves in starts above its spot.
        var targets = new Vector3[arrivals.Length];
        for (int i = 0; i < arrivals.Length; i++)
        {
            targets[i] = arrivals[i].position;
            arrivals[i].position = targets[i] + Vector3.up * entranceHeight;
        }
        Vector3 cameraRest = cameraToShake != null ? cameraToShake.localPosition : Vector3.zero;
        int lastBlink = -1;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            // Banner slides in from the left, holds, slides out to the right.
            float x = t < slideTime ? Mathf.Lerp(-UiWidth, 0f, Ease(t / slideTime))
                    : t > duration - slideTime ? Mathf.Lerp(0f, UiWidth, Ease((t - (duration - slideTime)) / slideTime))
                    : 0f;
            band.anchoredPosition = new Vector2(Mathf.Round(x), band.anchoredPosition.y);

            // Arrivals descend during the middle of the transition, landing before it ends.
            float move = Mathf.Clamp01((t - slideTime) / (duration - 2f * slideTime));
            for (int i = 0; i < arrivals.Length; i++)
                if (arrivals[i] != null)
                    arrivals[i].position = Vector3.Lerp(targets[i] + Vector3.up * entranceHeight, targets[i], Ease(move));

            if (warning)
            {
                int blink = (int)(t / 0.25f);
                bool on = blink % 2 == 0;
                title.enabled = on;
                stripesTop.enabled = stripesBottom.enabled = on;
                // Shake on each flash, in whole game pixels.
                if (cameraToShake != null && blink != lastBlink)
                {
                    lastBlink = blink;
                    float px = 1f / 16f;
                    cameraToShake.localPosition = on
                        ? cameraRest + new Vector3(Random.Range(-shakePixels, shakePixels + 1) * px, Random.Range(-shakePixels, shakePixels + 1) * px, 0f)
                        : cameraRest;
                }
            }
            yield return null;
        }

        for (int i = 0; i < arrivals.Length; i++)
            if (arrivals[i] != null) arrivals[i].position = targets[i];
        if (cameraToShake != null) cameraToShake.localPosition = cameraRest;
        title.enabled = true;
    }

    private static float Ease(float t) => 1f - (1f - t) * (1f - t);
}
