using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The two ways the game ends, in the RPG scene (one per scene):
///
///   BOTH PARTNERS BEATEN (any time)        ->  (optional dialogue) -> ending comic cutscene ->
///                                              fade out -> Credits scene
///   GAME OVER (deadline missed)            ->  fade out -> Game Over screen -> main menu
///
/// It listens to StoryManager itself (OnPhaseChanged -> TournamentDay, OnGameOver), so nothing
/// else needs to call it. Waits for any dialogue in progress to finish first.
///
/// Setup: see the field tooltips. Both target scenes must be in File > Build Profiles.
/// </summary>
public class EndGameController : MonoBehaviour
{
    [Header("Scenes (names, must be in the build)")]
    [SerializeField] private string creditsSceneName = "Credits";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Fade")]
    [Tooltip("A full-screen BLACK Image with a CanvasGroup (on a Canvas drawn on top of everything). " +
             "Used to fade out/in. Optional - without it the screens just cut.")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeTime = 1.5f;

    [Header("Tournament ending")]
    [Tooltip("OPTIONAL. Leave empty if the comic cutscene below tells the ending - no separate " +
             "dialogue is needed. If set, it plays before the comic.")]
    [SerializeField] private DialogueRunner endingDialogue;
    [Tooltip("The ending comic cutscene (its own panels + Music Id). This is the ending. " +
             "Empty = straight to the credits.")]
    [SerializeField] private ComicCutscene endingCutscene;
    [Tooltip("A win panel (an image) shown INSTEAD of the comic cutscene. Keep it inactive in the scene. " +
             "Leave empty to use the comic cutscene.")]
    [SerializeField] private GameObject endingPanel;
    [Tooltip("Minimum seconds the win panel is shown before a press (E / gamepad A) is accepted.")]
    [SerializeField] private float endingPanelMinShow = 2f;
    [Tooltip("Goes on to the credits by itself after this long. 0 = waits for a press forever.")]
    [SerializeField] private float endingPanelAutoSeconds = 0f;
    [Tooltip("Seconds of black between the cutscene and the credits.")]
    [SerializeField] private float blackBeforeCredits = 0.8f;

    [Header("Game over")]
    [Tooltip("The Game Over screen (enabled when it shows). Put your 'GAME OVER' art/text on it.")]
    [SerializeField] private GameObject gameOverScreen;
    [SerializeField] private CanvasGroup gameOverGroup;
    [Tooltip("Optional line of text on the screen.")]
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private string gameOverMessage = "You missed the tournament.";
    [Tooltip("SoundLibrary id played when the screen appears. Blank = none.")]
    [SerializeField] private string gameOverSfxId;
    [Tooltip("Minimum seconds shown before a press (Enter / gamepad A) is accepted.")]
    [SerializeField] private float gameOverMinShow = 2f;
    [Tooltip("Goes to the main menu by itself after this long. 0 = waits for a press forever.")]
    [SerializeField] private float gameOverAutoSeconds = 20f;

    private bool ending;
    private StoryManager subscribed;

    private void OnEnable() => StartCoroutine(Subscribe());

    private void OnDisable()
    {
        if (subscribed != null)
        {
            subscribed.OnPhaseChanged -= OnPhaseChanged;
            subscribed.OnGameOver -= OnGameOver;
            subscribed = null;
        }
    }

    private IEnumerator Subscribe()
    {
        while (StoryManager.instance == null) yield return null;
        subscribed = StoryManager.instance;
        subscribed.OnPhaseChanged += OnPhaseChanged;
        subscribed.OnGameOver += OnGameOver;

        if (fadeOverlay != null) fadeOverlay.alpha = 0f;
        if (gameOverScreen != null) gameOverScreen.SetActive(false);
        if (endingPanel != null) endingPanel.SetActive(false);

        // Loaded straight into the final phase (e.g. testing) - run the ending.
        if (StoryManager.instance.CurrentPhase == StoryPhase.TournamentDay) OnPhaseChanged(StoryPhase.TournamentDay, StoryPhase.TournamentDay);
    }

    private void OnPhaseChanged(StoryPhase oldPhase, StoryPhase newPhase)
    {
        if (newPhase != StoryPhase.TournamentDay || ending) return;
        ending = true;
        StartCoroutine(TournamentEnding());
    }

    private void OnGameOver()
    {
        if (ending) return;
        ending = true;
        StartCoroutine(GameOverSequence());
    }

    // ------------------------------------------------------------ tournament
    private IEnumerator TournamentEnding()
    {
        yield return WaitForDialogue();

        if (endingDialogue != null)
        {
            endingDialogue.Show();
            yield return null;
            while (endingDialogue.IsRunning) yield return null;
        }

        yield return Fade(1f);

        if (endingPanel != null)
        {
            endingPanel.SetActive(true);
            yield return Fade(0f);

            float shown = 0f;
            while (true)
            {
                shown += Time.unscaledDeltaTime;
                if (shown >= endingPanelMinShow && SkipInput.Pressed()) break;
                if (endingPanelAutoSeconds > 0f && shown >= endingPanelAutoSeconds) break;
                yield return null;
            }

            yield return Fade(1f);
            endingPanel.SetActive(false);
        }
        else if (endingCutscene != null)
        {
            bool done = false;
            void OnDone() => done = true;
            endingCutscene.OnCutsceneComplete += OnDone;
            // The cutscene shows its own panels over black; fade the overlay away so they're visible.
            endingCutscene.Play();
            yield return Fade(0f);
            while (!done) yield return null;
            endingCutscene.OnCutsceneComplete -= OnDone;
            yield return Fade(1f);
        }

        if (blackBeforeCredits > 0f) yield return new WaitForSecondsRealtime(blackBeforeCredits);
        LoadScene(creditsSceneName);
    }

    // ------------------------------------------------------------ game over
    private IEnumerator GameOverSequence()
    {
        yield return WaitForDialogue();
        yield return Fade(1f);

        if (gameOverText != null) gameOverText.text = gameOverMessage;
        if (gameOverScreen != null) gameOverScreen.SetActive(true);
        if (gameOverGroup != null) gameOverGroup.alpha = 1f;
        if (!string.IsNullOrEmpty(gameOverSfxId)) Sfx.Play(gameOverSfxId);
        yield return Fade(0f);

        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            if (t >= gameOverMinShow && SkipInput.Pressed()) break;
            if (gameOverAutoSeconds > 0f && t >= gameOverAutoSeconds) break;
            yield return null;
        }

        yield return Fade(1f);
        GameReset.ResetAll();
        LoadScene(mainMenuSceneName);
    }

    // ------------------------------------------------------------ helpers
    private IEnumerator WaitForDialogue()
    {
        float wait = 0f;
        while (ActiveDialogueTracker.AnyActive && wait < 30f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator Fade(float target)
    {
        if (fadeOverlay == null || fadeTime <= 0f) yield break;
        float from = fadeOverlay.alpha;
        float t = 0f;
        fadeOverlay.blocksRaycasts = target > 0.5f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / fadeTime));
            yield return null;
        }
        fadeOverlay.alpha = target;
    }

    private static void LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[EndGame] Scene '{sceneName}' can't be loaded - is it spelled exactly right and added to Build Profiles?");
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}