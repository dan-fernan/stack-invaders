using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss health bar across the top of the screen: the boss's label (e.g. "recurse(4)") over
/// a pixel bar whose fill drains on hits (flashing white), with a lighter "recent damage"
/// trail that catches up after a moment. Turns gray while the boss is stunned.
/// Works with any IBoss; an IMultiBarBoss with several bars (the recursion boss's ping and
/// pong) gets one bar per part, side by side, left to right.
/// Only animates (Update enabled) while the fill flash or trail is still moving.
/// </summary>
public class BossHud : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private RectTransform fill;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform trail;
    [Tooltip("Full bar width in UI pixels (1 UI unit = 1 game pixel).")]
    [SerializeField] private float barWidth = 160f;
    [SerializeField] private Color fillColor = new Color32(0xf8, 0x51, 0x49, 0xff);
    [SerializeField] private Color stunnedColor = new Color32(0x8b, 0x94, 0x9e, 0xff);
    [SerializeField] private float flashTime = 0.06f;
    [Tooltip("Seconds the damage trail waits before catching up.")]
    [SerializeField] private float trailDelay = 0.4f;
    [Tooltip("Fraction of the bar per second the trail drains.")]
    [SerializeField] private float trailSpeed = 0.6f;
    [Tooltip("Gap between side-by-side bars, in UI pixels.")]
    [SerializeField] private float multiBarGap = 4f;

    private IBoss boss;
    private IMultiBarBoss multiBoss;
    private readonly List<Image> partBars = new List<Image>();
    private float fraction = 1f;
    private float trailFraction = 1f;
    private float trailWait;
    private float flashUntil;
    private bool overflowing;

    public void Bind(IBoss target)
    {
        Unbind();
        boss = target;
        boss.HpChanged += OnHp;
        boss.LabelChanged += OnLabel;
        boss.StunnedChanged += OnStunned;
        multiBoss = target as IMultiBarBoss;
        if (multiBoss != null) multiBoss.BarsChanged += RefreshPartBars;
        RefreshPartBars();
        fraction = trailFraction = (float)boss.Hp / boss.MaxHp;
        overflowing = false;
        label.text = boss.Label;
        SetWidth(fill, fraction);
        SetWidth(trail, trailFraction);
        fillImage.color = fillColor;
        enabled = false;
    }

    private void OnDestroy() => Unbind();

    private void Unbind()
    {
        if (boss == null) return;
        boss.HpChanged -= OnHp;
        boss.LabelChanged -= OnLabel;
        boss.StunnedChanged -= OnStunned;
        if (multiBoss != null) multiBoss.BarsChanged -= RefreshPartBars;
        multiBoss = null;
        boss = null;
    }

    private void OnHp(int hp, int max)
    {
        fraction = Mathf.Clamp01((float)hp / max);
        SetWidth(fill, fraction);
        trailWait = trailDelay;
        flashUntil = Time.unscaledTime + flashTime;
        fillImage.color = Color.white;
        enabled = true; // animate the flash and trail
    }

    private void OnLabel(string text) => label.text = text;

    private void OnStunned(bool active)
    {
        overflowing = active;
        if (!enabled) fillImage.color = active ? stunnedColor : fillColor;
        foreach (Image bar in partBars) bar.color = active ? stunnedColor : fillColor;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool flashing = Time.unscaledTime < flashUntil;
        fillImage.color = flashing ? Color.white : overflowing ? stunnedColor : fillColor;

        if (trailWait > 0f) trailWait -= dt;
        else trailFraction = Mathf.MoveTowards(trailFraction, fraction, trailSpeed * dt);
        SetWidth(trail, trailFraction);

        if (!flashing && Mathf.Approximately(trailFraction, fraction)) enabled = false;
    }

    /// <summary>
    /// One bar per boss part. With a single part the normal bar (fill + damage trail) is
    /// used; with several, it's hidden and replaced by equal-width bars with gaps between.
    /// </summary>
    private void RefreshPartBars()
    {
        int count = multiBoss != null ? multiBoss.BarCount : 1;
        bool split = count > 1;
        fill.gameObject.SetActive(!split);
        trail.gameObject.SetActive(!split);

        while (partBars.Count < count && split) partBars.Add(CreatePartBar(partBars.Count));
        float width = (barWidth - multiBarGap * (count - 1)) / count;
        for (int i = 0; i < partBars.Count; i++)
        {
            bool used = split && i < count;
            partBars[i].gameObject.SetActive(used);
            if (!used) continue;
            multiBoss.GetBar(i, out int hp, out int max);
            var rect = partBars[i].rectTransform;
            rect.anchoredPosition = new Vector2(fill.anchoredPosition.x + i * (width + multiBarGap), 0f);
            rect.sizeDelta = new Vector2(Mathf.Round(Mathf.Clamp01((float)hp / max) * width), fill.sizeDelta.y);
            partBars[i].color = overflowing ? stunnedColor : fillColor;
        }
    }

    private Image CreatePartBar(int index)
    {
        var go = new GameObject($"PartBar{index}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(fill.parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = fill.anchorMin;
        rect.pivot = fill.pivot;
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void SetWidth(RectTransform bar, float f)
    {
        // Whole pixels, so the bar edge stays crisp.
        bar.sizeDelta = new Vector2(Mathf.Round(f * barWidth), bar.sizeDelta.y);
    }
}
