using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The Credits scene. Scrolls a block of text up the screen, then goes to the main menu.
///
/// Setup: Canvas with a RectTransform "Content" (your credits text, tall enough for all the
/// lines, anchored to the TOP-centre, pivot top) inside a full-screen parent. Put this script on
/// any object, assign the references. The Credits scene needs an AudioManager only if you set a
/// music id. Add the scene (and the menu scene) to Build Profiles.
/// </summary>
public class CreditsScroller : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [Tooltip("The full-screen canvas rect, used to know when the text has fully left the screen.")]
    [SerializeField] private RectTransform viewport;
    [Tooltip("Pixels (canvas units) per second.")]
    [SerializeField] private float scrollSpeed = 60f;
    [Tooltip("Seconds of black before the scroll starts.")]
    [SerializeField] private float startDelay = 1.5f;
    [Tooltip("Seconds to hold after the last line has left before moving on.")]
    [SerializeField] private float endHold = 2f;
    [Tooltip("Hold Fire / gamepad A to scroll this many times faster.")]
    [SerializeField] private float fastForward = 4f;

    [Header("Audio")]
    [SerializeField] private string musicId;
    [SerializeField] private float musicFadeIn = 2f;
    [SerializeField] private float musicFadeOut = 2f;

    [Header("Skip")]
    [Tooltip("A button that goes straight to the main menu. Optional.")]
    [SerializeField] private Button skipButton;

    [Header("After the credits")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Escape skips the credits (after this many seconds, so a stray press doesn't).")]
    [SerializeField] private float skipAvailableAfter = 2f;

    private bool leaving;

    private IEnumerator Start()
    {
        // The run is over - clear persistent state now so the menu starts a clean game.
        GameReset.ResetAll();

        if (skipButton != null)
        {
            skipButton.onClick.AddListener(Leave);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(skipButton.gameObject);
        }

        if (!string.IsNullOrEmpty(musicId))
        {
            while (AudioManager.instance == null) yield return null;
            AudioManager.instance.PlayMusic(musicId, musicFadeIn);
        }

        if (content != null && viewport != null)
        {
            // Start just below the bottom edge.
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, -viewport.rect.height);
        }

        yield return new WaitForSecondsRealtime(startDelay);

        float viewH = viewport != null ? viewport.rect.height : Screen.height;
        float contentH = content != null ? content.rect.height : 0f;
        float startY = content != null ? content.anchoredPosition.y : 0f;
        float totalDistance = viewH + contentH;   // from fully below the screen to fully above it
        float scrolled = 0f;

        float t = 0f;
        while (!leaving && content != null && scrolled < totalDistance)
        {
            t += Time.unscaledDeltaTime;
            if (t > skipAvailableAfter && Input.GetKeyDown(KeyCode.Escape)) break;

            bool fast = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.JoystickButton0);
            scrolled += scrollSpeed * (fast ? fastForward : 1f) * Time.unscaledDeltaTime;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, startY + scrolled);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(endHold);
        Leave();
    }

    private void Leave()
    {
        if (leaving) return;
        leaving = true;
        if (AudioManager.instance != null && !string.IsNullOrEmpty(musicId))
            AudioManager.instance.StopMusic(musicFadeOut);
        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"[Credits] Scene '{mainMenuSceneName}' can't be loaded - check the name and Build Profiles.");
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
