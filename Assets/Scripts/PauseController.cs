using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the HUD pause overlay and pause-to-main-menu navigation.
/// </summary>
public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    private bool isPaused;

    /// <summary>
    /// Toggles the game between paused and running states.
    /// </summary>
    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    /// <summary>Sets the pause state explicitly for overlays that use the existing pause system.</summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);
    }

    /// <summary>
    /// Clears run-local state and reloads the current gameplay scene from its configured start state.
    /// </summary>
    public void RestartGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.ClearLog();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Resumes time before returning to the main menu.
    /// </summary>
    public void LoadMainMenu()
    {
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
