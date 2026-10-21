using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the HUD pause overlay and pause-to-main-menu navigation.
/// </summary>
public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    private GameObject pauseRaycastBlocker;

    private bool isPaused;

    private void Awake()
    {
        CreatePauseRaycastBlocker();
        if (pauseRaycastBlocker != null)
            pauseRaycastBlocker.SetActive(false);
    }

    private void CreatePauseRaycastBlocker()
    {
        if (pausePanel == null || pausePanel.transform.parent == null)
            return;

        Transform panelParent = pausePanel.transform.parent;
        Transform existingBlocker = panelParent.Find("PauseRaycastBlocker");
        if (existingBlocker != null)
        {
            pauseRaycastBlocker = existingBlocker.gameObject;
        }
        else
        {
            pauseRaycastBlocker = new GameObject("PauseRaycastBlocker", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            pauseRaycastBlocker.transform.SetParent(panelParent, false);
        }

        pauseRaycastBlocker.layer = pausePanel.layer;
        RectTransform blockerRect = pauseRaycastBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        blockerRect.localScale = Vector3.one;
        blockerRect.SetSiblingIndex(pausePanel.transform.GetSiblingIndex());

        Image blockerImage = pauseRaycastBlocker.GetComponent<Image>();
        if (blockerImage == null)
            blockerImage = pauseRaycastBlocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        blockerImage.raycastTarget = true;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        InfectionSpawner infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (infectionSpawner != null && infectionSpawner.IsAwaitingInfectionTargetSelection)
            return;

        GameObject tutorialPanelObject = GameObject.Find("TutorialPanel");
        if (tutorialPanelObject != null && tutorialPanelObject.activeInHierarchy)
            return;

        if (isPaused && (pausePanel == null || !pausePanel.activeInHierarchy))
            return;

        TogglePause();
    }

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

        if (pauseRaycastBlocker != null)
        {
            pauseRaycastBlocker.SetActive(isPaused);
            if (isPaused && pausePanel != null)
            {
                pauseRaycastBlocker.transform.SetSiblingIndex(pausePanel.transform.GetSiblingIndex());
                pausePanel.transform.SetAsLastSibling();
            }
        }

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);
    }

    /// <summary>
    /// Clears run-local state and reloads the current gameplay scene from its configured start state.
    /// </summary>
    public void RestartGame()
    {
        SetPaused(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.ClearLog();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Resumes time before returning to the main menu.
    /// </summary>
    public void LoadMainMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene("MainMenu");
    }
}
