using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An enemy projectile the breakpoint parry can catch (fleet bits, boss split shots).
/// </summary>
public interface IParryable
{
    /// <summary>Still a threat to the player (not already caught, returning or resolved).</summary>
    bool IsDangerous { get; }
    Vector3 Position { get; }
    /// <summary>Shown in the parry pop-up, e.g. "0" or "recurse(2)".</summary>
    string ParryLabel { get; }
    /// <summary>Caught: send it back. Returns false if it could no longer be caught.</summary>
    bool StepInto();
}

/// <summary>Registry of parryable projectiles in play, so the parry needn't search the scene.</summary>
public static class Parryables
{
    private static readonly HashSet<IParryable> active = new HashSet<IParryable>();

    public static IReadOnlyCollection<IParryable> Active => active;
    public static void Register(IParryable p) => active.Add(p);
    public static void Unregister(IParryable p) => active.Remove(p);
}
