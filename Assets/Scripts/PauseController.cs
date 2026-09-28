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
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);
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
