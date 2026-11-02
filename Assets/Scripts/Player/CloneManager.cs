using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Horizontal scaling: one shadow clone of the developer per level, standing at fixed
/// offsets beside them (clone 1 left, clone 2 right) and moving as they move. Each clone
/// throws a weaker copy of every attack and has one heart; all clones respawn at the
/// start of each stage.
/// </summary>
public class CloneManager : MonoBehaviour
{
    [SerializeField] private PlayerClone clonePrefab;
    [SerializeField] private Transform developer;
    [SerializeField] private SpriteRenderer developerSprite;
    [SerializeField] private PlayerController developerMovement;
    [Tooltip("Offset from the developer for each clone, in order (left, right, ...).")]
    [SerializeField] private float[] offsets = { -1.5f, 1.5f };
    [Tooltip("Where clones throw from, relative to themselves (the throwing hand).")]
    [SerializeField] private Vector3 throwOffset = new Vector3(0.3125f, 0.5f, 0f);

    private readonly List<PlayerClone> clones = new List<PlayerClone>();
    private ICloneableAttack weapon;

    /// <summary>Called once the language is chosen.</summary>
    public void SetWeapon(ICloneableAttack attack)
    {
        if (weapon != null) weapon.Attacked -= OnAttacked;
        weapon = attack;
        if (weapon != null) weapon.Attacked += OnAttacked;
    }

    private void OnDestroy() => SetWeapon(null);

    /// <summary>Horizontal scaling level: how many clones exist.</summary>
    public void SetCount(int count)
    {
        count = Mathf.Min(count, offsets.Length);
        while (clones.Count < count)
        {
            PlayerClone clone = Instantiate(clonePrefab, developer.position, Quaternion.identity);
            clone.name = $"Instance {clones.Count + 1}";
            clone.Init(developerSprite);
            clone.Respawn(); // arrives ready, even mid-stage
            clones.Add(clone);
        }
    }

    /// <summary>Start of a stage: every clone comes back with its heart.</summary>
    public void RespawnAll()
    {
        foreach (PlayerClone clone in clones) clone.Respawn();
    }

    private void LateUpdate()
    {
        for (int i = 0; i < clones.Count; i++)
        {
            Vector3 p = developer.position;
            p.x = developerMovement.ClampX(p.x + offsets[i], developerSprite.bounds.extents.x);
            clones[i].transform.position = p;
        }
    }

    private void OnAttacked()
    {
        foreach (PlayerClone clone in clones)
            if (clone.IsAlive) weapon.FireCloneShot(clone.transform.position + throwOffset);
    }
}
