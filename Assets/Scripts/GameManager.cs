using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the win/lose rules.
/// Assignment requirement: "Detect when the aliens have reached a point on the screen
/// that the game is no longer possible to win." Once the lowest bug reaches the top of
/// the ship it can no longer be shot before it collides, so the game ends immediately,
/// regardless of hearts remaining.
/// </summary>
public class GameManager : MonoBehaviour
{
    private enum State { Playing, GameOver }

    [SerializeField] private AlienFormation formation;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private MonoBehaviour[] disableOnGameOver;

    [Header("Game over screen")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverText;

    private State state = State.Playing;

    private void Awake()
    {
        // timeScale is static and survives scene reloads, so reset it on every load.
        Time.timeScale = 1f;
        gameOverPanel.SetActive(false);
    }

    private void OnEnable()
    {
        formation.Stepped += CheckInvasion;
        playerHealth.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        formation.Stepped -= CheckInvasion;
        playerHealth.Died -= OnPlayerDied;
    }

    private void Update()
    {
        if (state == State.GameOver && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void CheckInvasion()
    {
        if (state != State.Playing || formation.AliveCount == 0) return;

        if (formation.LowestY <= playerSprite.bounds.max.y)
        {
            GameOver(
                "Unhandled exception: BugsInProductionException",
                "  at Formation.March() reached the deploy line",
                "  at Developer.Ship() 0 tests caught it");
        }
    }

    private void OnPlayerDied()
    {
        if (state != State.Playing) return;

        GameOver(
            "Unhandled exception: OutOfHeartsException",
            "  at Developer.TakeHit() hearts == 0",
            "  at Program.Main() no try/catch left");
    }

    private void GameOver(params string[] stackTrace)
    {
        state = State.GameOver;
        Time.timeScale = 0f;
        foreach (MonoBehaviour behaviour in disableOnGameOver)
            behaviour.enabled = false;

        gameOverText.text =
            "GAME OVER\n\n" +
            string.Join("\n", stackTrace) +
            "\n\nProcess exited with code 1\n\n> press R to recompile";
        gameOverPanel.SetActive(true);
    }
}
