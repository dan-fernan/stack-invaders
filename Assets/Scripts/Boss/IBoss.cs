using System;
using UnityEngine;

/// <summary>
/// What GameManager and the boss health bar need from any boss.
/// </summary>
public interface IBoss
{
    /// <summary>(current, max) health.</summary>
    event Action<int, int> HpChanged;
    /// <summary>The name/status shown over the health bar changed.</summary>
    event Action<string> LabelChanged;
    /// <summary>The boss became stunned/vulnerable-state (true) or recovered (false).</summary>
    event Action<bool> StunnedChanged;
    event Action Defeated;

    int Hp { get; }
    int MaxHp { get; }
    string Label { get; }

    /// <summary>The developer, for bosses that aim at them.</summary>
    void SetTarget(Transform player);
}

/// <summary>
/// A boss body part a parried projectile can hit (recursion frames, search cells).
/// </summary>
public interface IEnemyBody : IShootable
{
}
