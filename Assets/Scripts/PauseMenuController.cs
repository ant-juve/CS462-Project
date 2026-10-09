using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private GameObject pauseButtons;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    void Start()
    {
        SettingsMenuController.ApplySavedSettings(FindFirstObjectByType<PlayerMovement>());
        Resume();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!IsPaused)
                Pause();
            else if (settingsPanel.activeSelf)
                CloseSettings();
            else
                Resume();
        }
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        CloseSettings();
        Time.timeScale = 0f;
        IsPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        settingsPanel.SetActive(false);
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        IsPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OpenSettings()
    {
        pauseButtons.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        pauseButtons.SetActive(true);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    void OnDestroy()
    {
        // Don't leave the game frozen if this scene unloads while paused
        Time.timeScale = 1f;
        IsPaused = false;
    }
}
