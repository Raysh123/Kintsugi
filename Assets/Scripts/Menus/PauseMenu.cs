using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The pause menu. Press Start (gamepad) or Escape to open it; the game freezes. Buttons: Resume, Hints (opens the
/// same TutorialBrowser pages as the tutorial), Options, Main Menu. Pressing Start / Escape / B again resumes.
/// Going to the main menu wipes the run's persistent objects (GameReset), like arriving at the menu does.
///
/// Setup: a Canvas with a Menu Panel (keep it INACTIVE) holding the four buttons. Assign the buttons, a TutorialBrowser
/// (copy of the tutorial canvas one, so the hints match) and an OptionsPanel. Add this scene's neighbour,
/// the main menu scene, to Build Profiles. Put one of these in every scene where pausing should work.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button hintsButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Sub-screens")]
    [Tooltip("The hints browser (same pages as the tutorial). Optional - the Hints button does nothing without it.")]
    [SerializeField] private TutorialBrowser hintsBrowser;
    [SerializeField] private OptionsPanel optionsPanel;

    [Header("Button highlight")]
    [Tooltip("The button you're on grows a little and changes colour. Added to the four buttons automatically - " +
             "no need to put SelectableHighlight on them. Set the buttons' own Transition to None.")]
    [SerializeField] private float highlightScale = 1.12f;
    [SerializeField] private Color highlightColor = new Color(1f, 0.84f, 0.3f);

    [Header("Rules")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Optional. Pausing is ignored while the player is held by a cutscene.")]
    [SerializeField] private PlayerMovement player;

    public bool IsPaused { get; private set; }

    private float previousTimeScale = 1f;
    private bool busy;

    private void Start()
    {
        if (menuPanel != null) menuPanel.SetActive(false);

        AddHighlight(resumeButton);
        AddHighlight(hintsButton);
        AddHighlight(optionsButton);
        AddHighlight(mainMenuButton);

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (hintsButton != null) hintsButton.onClick.AddListener(OpenHints);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private void AddHighlight(Button button)
    {
        if (button == null || button.GetComponent<SelectableHighlight>() != null) return;
        button.gameObject.AddComponent<SelectableHighlight>().Setup(highlightScale, highlightColor);
    }

    private void OnDestroy()
    {
        // Never leave the game frozen if this object goes away while paused.
        if (IsPaused) Time.timeScale = 1f;
    }

    private void Update()
    {
        bool startPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);

        if (!IsPaused)
        {
            if (startPressed && CanPause()) Pause();
            return;
        }

        if (busy) return;
        if (optionsPanel != null && (optionsPanel.IsOpen || optionsPanel.LastCloseFrame == Time.frameCount)) return;

        if (startPressed || Input.GetKeyDown(KeyCode.JoystickButton1)) Resume();
    }

    private void LateUpdate()
    {
        // Other systems (match timer, intro panels, slow-mo) put the time scale back to normal from time to time.
        // While paused it is held at zero, after everything else has had its say each frame.
        if (IsPaused && Time.timeScale != 0f) Time.timeScale = 0f;
    }

    private bool CanPause()
    {
        if (busy || ActiveDialogueTracker.AnyActive) return false;
        if (hintsBrowser != null && hintsBrowser.IsOpen && hintsBrowser.gameObject.activeInHierarchy) return false;
        if (player != null && player.cutsceneLocked) return false;
        return true;
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        if (InputHandler.instance != null) InputHandler.instance.SetInput(false);
        if (AudioManager.instance != null) AudioManager.instance.ApplyMusicPauseEffect();

        ShowMenu();
    }

    public void Resume()
    {
        if (!IsPaused || busy) return;
        IsPaused = false;
        if (menuPanel != null) menuPanel.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (InputHandler.instance != null) InputHandler.instance.SetInput(true);
        if (AudioManager.instance != null) AudioManager.instance.ClearMusicPauseEffect();
    }

    private void ShowMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (resumeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private void OpenHints()
    {
        if (busy || hintsBrowser == null) return;
        StartCoroutine(HintsRoutine());
    }

    private IEnumerator HintsRoutine()
    {
        busy = true;
        if (menuPanel != null) menuPanel.SetActive(false);

        // The browser has to be on an ACTIVE object to start; its own panel hides itself, never this object.
        // Switch on the browser and every parent above it (a hidden canvas would keep it invisible).
        for (Transform t = hintsBrowser.transform; t != null; t = t.parent)
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

        hintsBrowser.Open();

        float waited = 0f;
        while (!hintsBrowser.IsOpen && waited < 1f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        while (hintsBrowser.IsOpen && hintsBrowser.gameObject.activeInHierarchy) yield return null;

        // The browser restores time and input when it closes - put the pause back.
        yield return null;
        Time.timeScale = 0f;
        if (InputHandler.instance != null) InputHandler.instance.SetInput(false);
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;

        busy = false;
        ShowMenu();
    }

    private void OpenOptions()
    {
        if (busy || optionsPanel == null) return;
        StartCoroutine(OptionsRoutine());
    }

    private IEnumerator OptionsRoutine()
    {
        busy = true;
        if (menuPanel != null) menuPanel.SetActive(false);

        optionsPanel.Open();
        yield return null;
        while (optionsPanel.IsOpen) yield return null;

        busy = false;
        ShowMenu();
    }

    private void GoToMainMenu()
    {
        if (busy) return;
        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName)) return;

        busy = true;
        GameReset.ResetAll();           // also sets Time.timeScale back to 1
        if (InputHandler.instance != null) InputHandler.instance.SetInput(true);
        SceneManager.LoadScene(mainMenuSceneName);
    }
}