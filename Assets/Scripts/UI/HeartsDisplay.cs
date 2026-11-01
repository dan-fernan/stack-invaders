using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws one pixel heart per life (full or empty) and refreshes when health changes.
/// </summary>
public class HeartsDisplay : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;
    [Tooltip("Gap between hearts in UI pixels (1 UI unit = 1 game pixel).")]
    [SerializeField] private float spacing = 2f;

    private readonly List<Image> hearts = new List<Image>();

    private void OnEnable() => health.Changed += Refresh;
    private void OnDisable() => health.Changed -= Refresh;

    private void Refresh(int current, int max)
    {
        while (hearts.Count < max) hearts.Add(CreateHeart(hearts.Count));

        for (int i = 0; i < hearts.Count; i++)
        {
            hearts[i].gameObject.SetActive(i < max);
            hearts[i].sprite = i < current ? fullHeart : emptyHeart;
        }
    }

    private Image CreateHeart(int index)
    {
        var go = new GameObject($"Heart_{index}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        var image = go.GetComponent<Image>();
        image.sprite = fullHeart;

        var rect = (RectTransform)go.transform;
        // Size in sprite pixels (1 UI unit = 1 game pixel). SetNativeSize() would scale
        // by the canvas's 100 PPU instead of our 16 PPU art and make hearts ~6x too big.
        rect.sizeDelta = fullHeart.rect.size;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(index * (rect.sizeDelta.x + spacing), 0f);
        return image;
    }
}
