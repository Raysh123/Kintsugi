using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The main menu scene, in three stages:
///
///   1. The LOGO fades in (solid black), then the PRESS START prompt starts pulsing. Any button / key starts.
///   2. The logo shrinks to half size, moves to Y 97 and turns a faint yellow (10% opacity) while the PRESS START panel
///      fades out and the MENU panel fades in; the menu's buttons work straight away (Play, Options, Credits, Quit).
///   3. Pressing a button plays that button's own animation on the character. Its Hit Delay seconds later (default 0.4)
///      the button jerks / wobbles as if it was kicked, and its After Hit Delay seconds after that (0.4) the choice
///      happens. Both times are set per button:
///        Play     -> a message panel pops up; when the player presses A (Enter / E / Space on keyboard) it fades and
///                    loads the first scene. The scene does not change until then.
///        Credits  -> loads the credits scene.
///        Quit     -> closes the game.
///        Options  -> opens the options panel, then returns to the menu.
///
/// Setup: see the field tooltips. Add all the scenes to Build Profiles. Arriving here wipes the previous run's
/// persistent objects (GameReset).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public enum MenuAction { Play, Options, Credits, Quit }

    [System.Serializable]
    public class MenuEntry
    {
        public MenuAction action;
        public Button button;
        [Tooltip("Name of the Animator STATE on the character that plays for this button (the state's name, e.g. 'Kick_High'). " +
                 "Blank = no animation.")]
        public string animState = "";
        [Tooltip("Animator layer the state is on.")]
        public int animLayer = 0;
        [Tooltip("Seconds after the press until this button's kick lands and the button jerks.")]
        public float hitDelay = 0.4f;
        [Tooltip("Seconds after the kick lands until the choice happens (panel / scene / quit).")]
        public float afterHitDelay = 0.4f;
    }

    [Header("1. Logo + Press Start")]
    [Tooltip("The logo (an Image or a TMP text). Put it OUTSIDE the Press Start panel and the Menu panel - it stays on screen " +
             "after start is pressed. It fades in black, then shrinks into a faint yellow mark behind the menu.")]
    [SerializeField] private Graphic logo;
    [SerializeField] private float logoFadeInTime = 1.5f;
    [Tooltip("Seconds of black before the logo starts to fade in.")]
    [SerializeField] private float logoStartDelay = 0.4f;
    [Header("   After start is pressed (the logo becomes a watermark)")]
    [SerializeField] private float logoEndScale = 0.5f;
    [Tooltip("Anchored Y position the logo moves to.")]
    [SerializeField] private float logoEndY = 97f;
    [Range(0f, 1f)][SerializeField] private float logoEndOpacity = 0.1f;
    [Tooltip("Colour the logo fades in with on the press-start screen.")]
    [SerializeField] private Color logoStartColor = Color.white;
    [SerializeField] private Color logoEndColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("Seconds the logo takes to settle into that look (it happens together with the panel fades).")]
    [SerializeField] private float logoSettleTime = 0.5f;

    [Header("   Press Start panel")]
    [Tooltip("The PRESS START prompt's panel (the prompt, no logo). Faded out when the player presses start.")]
    [SerializeField] private GameObject pressStartPanel;
    [Tooltip("Optional: the 'PRESS START' text / group. Hidden until the logo has faded in, then it pulses.")]
    [SerializeField] private CanvasGroup startPrompt;
    [Tooltip("Anything else belonging to the start screen that sits OUTSIDE the Press Start panel (e.g. the prompt icon). Switched off when start is pressed.")]
    [SerializeField] private GameObject[] hideOnStart;
    [SerializeField] private float promptPulseSpeed = 1.1f;
    [SerializeField] private float startFadeTime = 0.5f;
    [Tooltip("Seconds after the prompt starts pulsing before a press is accepted.")]
    [SerializeField] private float startInputDelay = 0.3f;
    [Tooltip("SoundLibrary id played when start is pressed. Blank = none.")]
    [SerializeField] private string startSfxId;

    [Header("2. Menu panel")]
    [Tooltip("Holds the buttons. Switched off until start is pressed, then faded in.")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private float menuFadeTime = 0.5f;
    [Tooltip("Button selected when the menu appears (for keyboard / gamepad).")]
    [SerializeField] private Button firstSelected;

    [Header("Buttons")]
    [SerializeField] private List<MenuEntry> buttons = new List<MenuEntry>();

    [Header("The character (plays each button's animation)")]
    [SerializeField] private Animator character;
    [Tooltip("SoundLibrary id played when a button's animation starts. Blank = none.")]
    [SerializeField] private string kickSfxId;

    [Header("Kick wobble (what happens to the button; the timings are on each button entry)")]
    [Tooltip("How far the button is knocked, in canvas units.")]
    [SerializeField] private float knockDistance = 30f;
    [Tooltip("Direction of the knock (the way the kick pushes it). Normalised.")]
    [SerializeField] private Vector2 knockDirection = new Vector2(1f, 0.25f);
    [Tooltip("Degrees the button twists on the hit.")]
    [SerializeField] private float knockTwist = 14f;
    [Tooltip("Wobbles per second while it settles.")]
    [SerializeField] private float wobbleFrequency = 3.5f;
    [Tooltip("How quickly the wobble dies away (higher = settles sooner).")]
    [SerializeField] private float wobbleDamping = 5f;
    [Tooltip("SoundLibrary id played when the kick lands on the button. Blank = none.")]
    [SerializeField] private string hitSfxId;

    [Header("Play: message panel (shown after the Play animation)")]
    [Tooltip("The panel that pops up after the Play animation (say whatever you like on it). Keep it inactive in the scene.")]
    [SerializeField] private GameObject playPanel;
    [SerializeField] private float playPanelPopTime = 0.3f;
    [Tooltip("Seconds the panel is shown before a press of A is accepted.")]
    [SerializeField] private float playPanelMinShow = 0.8f;
    [Tooltip("SoundLibrary id played when the panel pops up. Blank = none.")]
    [SerializeField] private string playPanelSfxId;

    [Header("Scenes (names, must be in the build)")]
    [Tooltip("The first scene of a new game.")]
    [SerializeField] private string firstSceneName = "GameIntro";
    [SerializeField] private string creditsSceneName = "Credits";

    [Header("Button highlight")]
    [Tooltip("The button you're on grows a little and changes colour. Added to every button automatically.")]
    [SerializeField] private float highlightScale = 1.12f;
    [SerializeField] private Color highlightColor = new Color(1f, 0.84f, 0.3f);

    [Header("Options / Fade")]
    [SerializeField] private OptionsPanel optionsPanel;
    [Tooltip("Optional full-screen BLACK CanvasGroup, faded in before a scene change.")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeTime = 0.6f;
    [SerializeField] private bool allowQuit = true;

    [Header("Audio")]
    [SerializeField] private string musicId;
    [SerializeField] private float musicFadeIn = 1.5f;
    [SerializeField] private float musicFadeOut = 1f;

    private bool busy;
    private bool started;
    private CanvasGroup pressStartGroup;
    private CanvasGroup menuGroup;

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        // Anything left over from the last run is gone before a new game begins.
        GameReset.ResetAll();

        if (fadeOverlay != null) fadeOverlay.alpha = 0f;
        if (playPanel != null) playPanel.SetActive(false);

        foreach (MenuEntry p in buttons)
        {
            if (p.button == null) continue;
            MenuEntry captured = p;
            p.button.onClick.AddListener(() => Press(captured));
            if (p.button.GetComponent<SelectableHighlight>() == null)
                p.button.gameObject.AddComponent<SelectableHighlight>().Setup(highlightScale, highlightColor);
            if (p.action == MenuAction.Quit) p.button.gameObject.SetActive(allowQuit);
        }

        // Menu hidden, press-start screen shown (or straight to the menu if no start panel is assigned).
        if (menuPanel != null)
        {
            menuGroup = GroupOn(menuPanel);
            menuGroup.alpha = 0f;
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
            menuPanel.SetActive(pressStartPanel == null);
        }
        if (pressStartPanel != null)
        {
            pressStartGroup = GroupOn(pressStartPanel);
            pressStartGroup.alpha = 1f;
            pressStartPanel.SetActive(true);
        }
        else
        {
            started = true;
            OpenMenu();
        }

        // the logo starts invisible and solid black
        if (logo != null)
        {
            Color black = logoStartColor; black.a = 0f;
            logo.color = black;
        }
        if (startPrompt != null) startPrompt.alpha = 0f;
        if (pressStartPanel == null && logo != null) StartCoroutine(LogoToWatermark());   // no start screen: straight to the watermark look

        if (!string.IsNullOrEmpty(musicId))
        {
            while (AudioManager.instance == null) yield return null;
            AudioManager.instance.PlayMusic(musicId, musicFadeIn);
        }

        if (pressStartPanel != null) yield return WaitForStart();
    }

    // ------------------------------------------------------------ 1 -> 2
    private IEnumerator WaitForStart()
    {
        // 1) the logo fades in, black
        if (logo != null)
        {
            if (logoStartDelay > 0f) yield return new WaitForSecondsRealtime(logoStartDelay);
            yield return null;
            float f = 0f;
            while (f < logoFadeInTime)
            {
                f += Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // a loading hitch can't swallow the fade in one frame
                Color col = logoStartColor; col.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f / Mathf.Max(0.01f, logoFadeInTime)));
                logo.color = col;
                yield return null;
            }
            logo.color = logoStartColor;
        }

        // 2) the prompt pulses until something is pressed
        float t = 0f;
        bool pressed = false;
        while (!pressed)
        {
            t += Time.unscaledDeltaTime;
            if (startPrompt != null)
                startPrompt.alpha = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * promptPulseSpeed * 2f * Mathf.PI));
            if (t >= startInputDelay && AnyPress()) pressed = true;
            yield return null;
        }
        started = true;

        if (!string.IsNullOrEmpty(startSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(startSfxId);

        // 3) all at once: logo settles into its watermark look, the menu opens, the start panel fades out
        if (logo != null) StartCoroutine(LogoToWatermark());
        OpenMenu();
        if (startPrompt != null) StartCoroutine(Fade(startPrompt, 0f, startFadeTime));
        yield return Fade(pressStartGroup, 0f, startFadeTime);
        pressStartPanel.SetActive(false);
        if (startPrompt != null) startPrompt.gameObject.SetActive(false);
        if (hideOnStart != null)
            foreach (GameObject g in hideOnStart) if (g != null) g.SetActive(false);
    }

    private IEnumerator LogoToWatermark()
    {
        RectTransform rt = logo.rectTransform;
        Vector3 fromScale = rt.localScale;
        Vector2 fromPos = rt.anchoredPosition;
        Color fromColor = logo.color;
        Vector3 toScale = Vector3.one * logoEndScale;
        Vector2 toPos = new Vector2(fromPos.x, logoEndY);
        Color toColor = logoEndColor; toColor.a = logoEndOpacity;

        float t = 0f;
        while (t < logoSettleTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, logoSettleTime));
            k = k * k * (3f - 2f * k);
            rt.localScale = Vector3.Lerp(fromScale, toScale, k);
            rt.anchoredPosition = Vector2.Lerp(fromPos, toPos, k);
            logo.color = Color.Lerp(fromColor, toColor, k);
            yield return null;
        }
        rt.localScale = toScale;
        rt.anchoredPosition = toPos;
        logo.color = toColor;
    }

    /// <summary>Opens the menu: it fades in and its buttons work. Controls switch on one frame later, so the press that
    /// started the game can't also press a button.</summary>
    private void OpenMenu()
    {
        if (menuPanel == null) return;
        menuPanel.SetActive(true);
        if (menuGroup == null) menuGroup = GroupOn(menuPanel);
        menuGroup.alpha = 0f;
        StartCoroutine(Fade(menuGroup, 1f, menuFadeTime));
        StartCoroutine(EnableMenuControls());
    }

    private IEnumerator EnableMenuControls()
    {
        yield return null;
        menuGroup.interactable = true;
        menuGroup.blocksRaycasts = true;
        if (firstSelected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }
    }

    // ------------------------------------------------------------ 3: a button
    private void Press(MenuEntry p)
    {
        if (busy || !started) return;
        busy = true;
        StartCoroutine(ButtonSequence(p));
    }

    private IEnumerator ButtonSequence(MenuEntry p)
    {
        if (menuGroup != null) menuGroup.interactable = false;

        StartButtonAnimation(p);
        yield return new WaitForSecondsRealtime(p.hitDelay);

        // the kick lands: the button jerks and wobbles while the countdown to the choice runs
        if (!string.IsNullOrEmpty(hitSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(hitSfxId);
        if (p.button != null) StartCoroutine(Wobble(p.button.transform as RectTransform, p.afterHitDelay));
        yield return new WaitForSecondsRealtime(p.afterHitDelay);

        switch (p.action)
        {
            case MenuAction.Play:
                yield return PlayPanelThenLoad();
                break;

            case MenuAction.Credits:
                yield return LoadScene(creditsSceneName, false);
                break;

            case MenuAction.Options:
                if (optionsPanel != null)
                {
                    optionsPanel.Open();
                    yield return null;
                    while (optionsPanel.IsOpen) yield return null;
                }
                ReturnToMenu(p);
                break;

            case MenuAction.Quit:
                Quit();
                ReturnToMenu(p);
                break;
        }
    }

    private void StartButtonAnimation(MenuEntry p)
    {
        if (!string.IsNullOrEmpty(kickSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(kickSfxId);

        if (character == null || string.IsNullOrEmpty(p.animState)) return;
        int hash = Animator.StringToHash(p.animState);
        if (character.HasState(p.animLayer, hash)) character.Play(hash, p.animLayer, 0f);
    }

    /// <summary>A kick on the button: knocked away, then a damped wobble back to rest (position and twist).</summary>
    private IEnumerator Wobble(RectTransform rt, float afterHitDelay)
    {
        if (rt == null) yield break;
        Vector2 basePos = rt.anchoredPosition;
        float baseRot = rt.localEulerAngles.z;
        Vector2 dir = knockDirection.sqrMagnitude > 0.0001f ? knockDirection.normalized : Vector2.right;

        float t = 0f;
        float total = Mathf.Max(0.1f, afterHitDelay + 0.3f);
        while (t < total && rt != null)
        {
            t += Time.unscaledDeltaTime;
            // starts at full knock, swings back and forth while it dies away
            float decay = Mathf.Exp(-wobbleDamping * t);
            float swing = Mathf.Cos(2f * Mathf.PI * wobbleFrequency * t) * decay;
            rt.anchoredPosition = basePos + dir * (knockDistance * swing);
            rt.localEulerAngles = new Vector3(0f, 0f, baseRot + knockTwist * swing);
            yield return null;
        }
        if (rt != null)
        {
            rt.anchoredPosition = basePos;
            rt.localEulerAngles = new Vector3(0f, 0f, baseRot);
        }
    }

    private IEnumerator PlayPanelThenLoad()
    {
        if (playPanel != null)
        {
            if (menuGroup != null) yield return Fade(menuGroup, 0f, 0.25f);
            if (menuPanel != null) menuPanel.SetActive(false);

            CanvasGroup g = GroupOn(playPanel);
            Vector3 baseScale = playPanel.transform.localScale;
            g.alpha = 0f;
            playPanel.SetActive(true);
            if (!string.IsNullOrEmpty(playPanelSfxId) && AudioManager.instance != null)
                AudioManager.instance.PlaySFX(playPanelSfxId);

            // pop in: scale up with a small overshoot while fading in
            float t = 0f;
            while (t < playPanelPopTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, playPanelPopTime));
                float pop = 1f + 0.08f * Mathf.Sin(k * Mathf.PI);
                playPanel.transform.localScale = baseScale * Mathf.Lerp(0.8f, 1f, k * k * (3f - 2f * k)) * pop;
                g.alpha = k;
                yield return null;
            }
            playPanel.transform.localScale = baseScale;
            g.alpha = 1f;

            // wait for A (Enter / E / Space on keyboard)
            float shown = 0f;
            while (true)
            {
                shown += Time.unscaledDeltaTime;
                if (shown >= playPanelMinShow && APressed()) break;
                yield return null;
            }
        }

        yield return LoadScene(firstSceneName, true);
    }

    private void ReturnToMenu(MenuEntry p)
    {
        if (menuGroup != null) menuGroup.interactable = true;
        if (p.button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(p.button.gameObject);
        busy = false;
    }

    private IEnumerator LoadScene(string sceneName, bool newGame)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[MainMenu] Scene '{sceneName}' can't be loaded - check the name and Build Profiles.");
            if (menuGroup != null) menuGroup.interactable = true;
            busy = false;
            yield break;
        }

        if (AudioManager.instance != null && !string.IsNullOrEmpty(musicId))
            AudioManager.instance.StopMusic(musicFadeOut);

        if (fadeOverlay != null)
        {
            fadeOverlay.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Clamp01(t / fadeTime);
                yield return null;
            }
            fadeOverlay.alpha = 1f;
        }

        if (newGame) GameReset.ResetAll();
        SceneManager.LoadScene(sceneName);
    }

    // ------------------------------------------------------------ helpers
    private static CanvasGroup GroupOn(GameObject go)
    {
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        return g;
    }

    private static IEnumerator Fade(CanvasGroup g, float target, float time)
    {
        if (g == null) yield break;
        float from = g.alpha, t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / Mathf.Max(0.01f, time)));
            yield return null;
        }
        g.alpha = target;
    }

    /// <summary>Any key, mouse button or gamepad button.</summary>
    private static bool AnyPress()
    {
        if (Input.anyKeyDown) return true;
#if ENABLE_INPUT_SYSTEM
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null)
        {
            if (pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame ||
                pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                pad.buttonNorth.wasPressedThisFrame) return true;
        }
#endif
        return false;
    }

    /// <summary>Gamepad A, or Enter / E / Space on the keyboard.</summary>
    private static bool APressed()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.JoystickButton0)) return true;
        try { if (Input.GetButtonDown("Submit")) return true; } catch (System.Exception) { }
#if ENABLE_INPUT_SYSTEM
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && pad.buttonSouth.wasPressedThisFrame) return true;
#endif
        return false;
    }

    public void Quit()
    {
        if (!allowQuit) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}