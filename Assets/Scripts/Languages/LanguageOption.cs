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

    [Header("Start page (name + description should stay under ~57 characters)")]
    [Tooltip("Primary weapon name, e.g. \"coffee cup\".")]
    public string weaponName;
    [Tooltip("What the weapon does, e.g. \"splashes a bug and both neighbors\".")]
    public string weapon;
    [Tooltip("Passive trait name, e.g. \"just-in-time compilation\".")]
    public string passiveName;
    [Tooltip("What the passive does.")]
    public string passive;
    [Tooltip("Native ability name, e.g. \"garbage collector\".")]
    public string abilityName;
    [Tooltip("What the ability does.")]
    public string ability;

    [Tooltip("Separate sprite for the item the developer holds. Leave empty if the item is drawn into the developer's frames (Java's mug).")]
    public Sprite heldItem;
    [Tooltip("Weapon and ability scripts enabled only while playing this language.")]
    public MonoBehaviour[] behaviours;
    [Tooltip("Language-specific HUD (e.g. Java's JIT/GC meter). Optional.")]
    public GameObject hud;

    [Header("Boons (floppy disk drops)")]
    [Tooltip("This language's boon pool.")]
    public BoonDefinition[] boons;
    [Tooltip("Component implementing IBoonReceiver that applies this language's boons.")]
    public MonoBehaviour boonReceiver;
}
