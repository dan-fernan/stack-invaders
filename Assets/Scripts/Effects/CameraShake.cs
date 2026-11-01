using System.Collections;
using UnityEngine;

/// <summary>
/// Short screen shake when the player takes a hit. Offsets are whole game pixels
/// so the pixel-perfect camera stays on the pixel grid.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private float duration = 0.2f;
    [Tooltip("Maximum offset in game pixels.")]
    [SerializeField] private int magnitudePixels = 2;
    [SerializeField] private int pixelsPerUnit = 16;

    private Vector3 restPosition;
    private Coroutine shaking;

    private void Awake() => restPosition = transform.localPosition;
    private void OnEnable() => playerHealth.Hit += Shake;
    private void OnDisable() => playerHealth.Hit -= Shake;

    private void Shake()
    {
        if (shaking != null) StopCoroutine(shaking);
        shaking = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float pixel = 1f / pixelsPerUnit;
        // Unscaled time: the shake must finish even if the hit ends the game (timeScale = 0).
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            int x = Random.Range(-magnitudePixels, magnitudePixels + 1);
            int y = Random.Range(-magnitudePixels, magnitudePixels + 1);
            transform.localPosition = restPosition + new Vector3(x * pixel, y * pixel, 0f);
            yield return null;
        }
        transform.localPosition = restPosition;
        shaking = null;
    }
}
