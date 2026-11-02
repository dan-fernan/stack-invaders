using System;
using UnityEngine;

/// <summary>
/// One boon a language can offer (Hades-style: permanent for the run, picking it again
/// levels it up). The number of level descriptions is its max level.
/// </summary>
[Serializable]
public class BoonDefinition
{
    [Tooltip("Stable id the language's IBoonReceiver switches on, e.g. \"spring\".")]
    public string id;
    public string displayName;
    [Tooltip("Short HUD tag, e.g. \"SB\" (shown as SB2 at level 2).")]
    public string tag;
    [Tooltip("What each level does; its length is the max level. Keep lines short (~40 chars).")]
    public string[] levelDescriptions;

    public int MaxLevel => levelDescriptions.Length;
}

/// <summary>
/// Implemented by a language's boon handler: applies a boon at its new level.
/// </summary>
public interface IBoonReceiver
{
    void ApplyBoon(string id, int level);
}

/// <summary>
/// A language weapon that supports the Multithreading boon: every Nth attack fires twice.
/// </summary>
public interface IMultiShotWeapon
{
    /// <summary>0 = off; N = every Nth attack fires an extra shot.</summary>
    int ExtraShotEvery { get; set; }
}

/// <summary>
/// Scripts with state that should start fresh at the beginning of each stage
/// (rather than every time gameplay is paused and resumed).
/// </summary>
public interface IStageReset
{
    void ResetForStage();
}
