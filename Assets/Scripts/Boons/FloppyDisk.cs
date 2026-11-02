using System;
using UnityEngine;

/// <summary>
/// A boon drop: falls from the bug that dropped it, rests on the floor, and is picked
/// up when the developer touches it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FloppyDisk : MonoBehaviour
{
    [SerializeField] private float fallSpeed = 2.5f;
    [Tooltip("Gentle bob while resting on the floor, in pixels.")]
    [SerializeField] private float bobPixels = 1f;

    public event Action<FloppyDisk> PickedUp;

    private float floorY;
    private bool landed;
    private bool taken;
    private float age;

    /// <param name="floor">World y where the disk comes to rest (the developer's feet level).</param>
    public void Init(float floor) => floorY = floor;

    private void Update()
    {
        age += Time.deltaTime;
        Vector3 p = transform.position;
        if (!landed)
        {
            p.y -= fallSpeed * Time.deltaTime;
            if (p.y <= floorY) { p.y = floorY; landed = true; }
        }
        else
        {
            // Whole-pixel bob so it stays on the pixel grid.
            p.y = floorY + (Mathf.Repeat(age, 1f) < 0.5f ? bobPixels / 16f : 0f);
        }
        transform.position = p;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || !other.TryGetComponent(out PlayerHealth _)) return;
        Take();
    }

    /// <summary>Picked up by the developer (or collected automatically when the wave ends).</summary>
    public void Take()
    {
        if (taken) return;
        taken = true;
        PickedUp?.Invoke(this);
        Destroy(gameObject);
    }
}
