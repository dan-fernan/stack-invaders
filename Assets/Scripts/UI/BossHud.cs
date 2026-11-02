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
    [Tooltip("Background behind the single bar; hidden when split so each part shows its own track.")]
    [SerializeField] private Image barBackground;
    [SerializeField] private Color trackColor = new Color32(0x30, 0x36, 0x3d, 0xff);
    [SerializeField] private Color partLabelColor = new Color32(0xc9, 0xd1, 0xd9, 0xff);
    [SerializeField] private int partLabelSize = 10;

    private IBoss boss;
    private IMultiBarBoss multiBoss;
    private class PartBar
    {
        public Image track;
        public Image fill;
        public Text caption;
    }
    private readonly List<PartBar> partBars = new List<PartBar>();
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

    private bool IsSplit => multiBoss != null && multiBoss.BarCount > 1;

    /// <summary>
    /// While split, each bar's caption takes the label row; the shared label only comes back
    /// for the stun message.
    /// </summary>
    private void UpdateLabelRow()
    {
        bool captions = IsSplit && !overflowing;
        label.enabled = !captions;
        foreach (PartBar part in partBars) part.caption.enabled = captions;
    }

    private void OnStunned(bool active)
    {
        overflowing = active;
        if (!enabled) fillImage.color = active ? stunnedColor : fillColor;
        foreach (PartBar bar in partBars) bar.fill.color = active ? stunnedColor : fillColor;
        UpdateLabelRow();
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
    /// used; with several, it's hidden and replaced by equal-width bars, each on its own
    /// track with a caption above it (in the label row) naming the part and its HP ("ping 12/35 HP").
    /// </summary>
    private void RefreshPartBars()
    {
        int count = multiBoss != null ? multiBoss.BarCount : 1;
        bool split = count > 1;
        fill.gameObject.SetActive(!split);
        trail.gameObject.SetActive(!split);
        if (barBackground != null) barBackground.enabled = !split;

        while (partBars.Count < count && split) partBars.Add(CreatePartBar(partBars.Count));
        RectTransform bg = (RectTransform)fill.parent;
        float trackWidth = (bg.sizeDelta.x - multiBarGap * (count - 1)) / count;
        float fillWidth = trackWidth - 2f; // 1px border inside the track, like the single bar
        for (int i = 0; i < partBars.Count; i++)
        {
            PartBar part = partBars[i];
            bool used = split && i < count;
            part.track.gameObject.SetActive(used);
            if (!used) continue;
            multiBoss.GetBar(i, out int hp, out int max);

            float x = Mathf.Round(i * (trackWidth + multiBarGap));
            var trackRect = part.track.rectTransform;
            trackRect.anchoredPosition = new Vector2(x, 0f);
            trackRect.sizeDelta = new Vector2(Mathf.Round(trackWidth), bg.sizeDelta.y);
            part.fill.rectTransform.sizeDelta = new Vector2(Mathf.Round(Mathf.Clamp01((float)hp / max) * fillWidth), fill.sizeDelta.y);
            part.fill.color = overflowing ? stunnedColor : fillColor;
            part.caption.rectTransform.sizeDelta = new Vector2(Mathf.Round(trackWidth), label.rectTransform.sizeDelta.y);
            part.caption.text = multiBoss.GetBarLabel(i);
            part.caption.color = hp > 0 ? partLabelColor : stunnedColor;
        }
        UpdateLabelRow();
    }

    private PartBar CreatePartBar(int index)
    {
        RectTransform bg = (RectTransform)fill.parent;
        var part = new PartBar();

        // Track: left-aligned in the background's area, same height.
        var trackRect = NewRect($"PartTrack{index}", bg);
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(0f, 1f);
        trackRect.pivot = new Vector2(0f, 1f);
        part.track = trackRect.gameObject.AddComponent<Image>();
        part.track.color = trackColor;
        part.track.raycastTarget = false;

        var fillRect = NewRect("Fill", trackRect);
        fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(1f, 0f);
        part.fill = fillRect.gameObject.AddComponent<Image>();
        part.fill.raycastTarget = false;

        // Caption in the label row, centered over its own track.
        var captionRect = NewRect("Caption", trackRect);
        captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 1f);
        captionRect.pivot = new Vector2(0.5f, 0f);
        captionRect.anchoredPosition = new Vector2(0f, 0f);
        part.caption = captionRect.gameObject.AddComponent<Text>();
        part.caption.font = label.font;
        part.caption.fontSize = partLabelSize;
        part.caption.alignment = TextAnchor.UpperCenter;
        part.caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        part.caption.verticalOverflow = VerticalWrapMode.Overflow;
        part.caption.raycastTarget = false;
        return part;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private void SetWidth(RectTransform bar, float f)
    {
        // Whole pixels, so the bar edge stays crisp.
        bar.sizeDelta = new Vector2(Mathf.Round(f * barWidth), bar.sizeDelta.y);
    }
}
