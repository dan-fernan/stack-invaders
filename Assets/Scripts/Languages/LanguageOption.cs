using System;
using UnityEngine;

/// <summary>
/// One entry in the language select menu: what the developer holds and which
/// scripts (weapon, abilities, HUD) run while that language is chosen.
/// </summary>
[Serializable]
public class LanguageOption
{
    public string displayName = "Java";
    [Tooltip("Short description shown in the menu.")]
    public string tagline = "slow start, strong finish";
    [Tooltip("Locked languages show as 'coming soon' and can't be picked.")]
    public bool locked;

    [Tooltip("Separate sprite for the item the developer holds. Leave empty if the item is drawn into the developer's frames (Java's mug).")]
    public Sprite heldItem;
    [Tooltip("Weapon and ability scripts enabled only while playing this language.")]
    public MonoBehaviour[] behaviours;
    [Tooltip("Language-specific HUD (e.g. Java's JIT/GC meter). Optional.")]
    public GameObject hud;
}
