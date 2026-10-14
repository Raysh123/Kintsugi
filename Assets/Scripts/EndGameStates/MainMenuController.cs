using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The main menu scene. Hook your buttons' On Click to StartNewGame() and Quit().
/// Picks the first button automatically so a gamepad / keyboard works straight away.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Name of the scene a new game starts in (the first scene of the game). Must be in the build.")]
    [SerializeField] private string firstSceneName = "GameIntro";
    [Tooltip("Button selected when the menu opens (for keyboard / gamepad).")]
    [SerializeField] private Button firstSelected;
    [Tooltip("Hide the Quit button (and the Quit() call does nothing) - for a show-floor kiosk.")]
    [SerializeField] private GameObject quitButtonObject;
    [SerializeField] private bool allowQuit = true;

    [Header("Audio")]
    [SerializeField] private string musicId;
    [SerializeField] private float musicFadeIn = 1.5f;
    [SerializeField] private float musicFadeOut = 1f;

    private bool starting;

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        // Safety: anything left over from the last run is gone before a new game begins.
        GameReset.ResetAll();

        if (quitButtonObject != null) quitButtonObject.SetActive(allowQuit);

        yield return null; // let the EventSystem initialise
        if (firstSelected != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);

        if (!string.IsNullOrEmpty(musicId))
        {
            while (AudioManager.instance == null) yield return null;
            AudioManager.instance.PlayMusic(musicId, musicFadeIn);
        }
    }

    public void StartNewGame()
    {
        if (starting) return;
        if (!Application.CanStreamedLevelBeLoaded(firstSceneName))
        {
            Debug.LogError($"[MainMenu] Scene '{firstSceneName}' can't be loaded - check the name and Build Profiles.");
            return;
        }
        starting = true;
        GameReset.ResetAll();
        if (AudioManager.instance != null && !string.IsNullOrEmpty(musicId))
            AudioManager.instance.StopMusic(musicFadeOut);
        SceneManager.LoadScene(firstSceneName);
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
