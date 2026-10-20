using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The main menu scene: Play, Options, Credits, Quit, as a normal controller / keyboard menu. Pressing a button
/// plays that button's own kick animation on the character (four kicks at four heights, one per button), waits for
/// it to finish, and then the choice happens: Play / Credits change scene, Options opens the options panel, Quit
/// closes the game.
///
/// Setup: one entry per button in Buttons - pick its Action, assign the Button, the Animator trigger that starts its
/// kick, and how long that kick takes. Assign the character's Animator. Add all the scenes to Build Profiles.
/// Arriving here wipes the previous run's persistent objects (GameReset).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public enum MenuAction { Play, Options, Credits, Quit }

    [System.Serializable]
    public class MenuEntry
    {
        public MenuAction action;
        public Button button;
        [Tooltip("Animator trigger that plays this button's kick.")]
        public string kickTrigger = "Kick";
        [Tooltip("Seconds to wait for the kick animation to play before the choice happens.")]
        public float kickDuration = 1.5f;
    }

    [Header("Buttons")]
    [SerializeField] private List<MenuEntry> buttons = new List<MenuEntry>();
    [Tooltip("Button selected when the menu opens (for keyboard / gamepad).")]
    [SerializeField] private Button firstSelected;

    [Header("Scenes (names, must be in the build)")]
    [Tooltip("The first scene of a new game.")]
    [SerializeField] private string firstSceneName = "GameIntro";
    [SerializeField] private string creditsSceneName = "Credits";

    [Header("Button highlight")]
    [Tooltip("The button you're on grows a little and changes colour. Added to every button automatically - no need to " +
             "put SelectableHighlight on them. Set the buttons' own Transition to None.")]
    [SerializeField] private float highlightScale = 1.12f;
    [SerializeField] private Color highlightColor = new Color(1f, 0.84f, 0.3f);

    [Header("The kick")]
    [SerializeField] private Animator character;
    [Tooltip("SoundLibrary id played when the kick starts. Blank = none.")]
    [SerializeField] private string kickSfxId;

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

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        // Anything left over from the last run is gone before a new game begins.
        GameReset.ResetAll();

        if (fadeOverlay != null) fadeOverlay.alpha = 0f;

        foreach (MenuEntry p in buttons)
        {
            if (p.button == null) continue;
            MenuEntry captured = p;
            p.button.onClick.AddListener(() => Press(captured));
            if (p.button.GetComponent<SelectableHighlight>() == null)
                p.button.gameObject.AddComponent<SelectableHighlight>().Setup(highlightScale, highlightColor);
            if (p.action == MenuAction.Quit) p.button.gameObject.SetActive(allowQuit);
        }

        yield return null; // let the EventSystem initialise
        if (firstSelected != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);

        if (!string.IsNullOrEmpty(musicId))
        {
            while (AudioManager.instance == null) yield return null;
            AudioManager.instance.PlayMusic(musicId, musicFadeIn);
        }
    }

    private void Press(MenuEntry p)
    {
        if (busy) return;
        busy = true;
        StartCoroutine(KickSequence(p));
    }

    private IEnumerator KickSequence(MenuEntry p)
    {
        if (character != null && !string.IsNullOrEmpty(p.kickTrigger))
            character.SetTrigger(p.kickTrigger);
        if (!string.IsNullOrEmpty(kickSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(kickSfxId);

        yield return new WaitForSecondsRealtime(p.kickDuration);

        switch (p.action)
        {
            case MenuAction.Play:
                yield return LoadScene(firstSceneName, true);
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

    private void ReturnToMenu(MenuEntry p)
    {
        if (p.button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(p.button.gameObject);
        busy = false;
    }

    private IEnumerator LoadScene(string sceneName, bool newGame)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[MainMenu] Scene '{sceneName}' can't be loaded - check the name and Build Profiles.");
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