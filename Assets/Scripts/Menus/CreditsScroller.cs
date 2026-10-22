using System.Collections;
using UnityEngine;
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

    [Header("After the credits")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Holding B (gamepad) or pressing Escape skips the credits, but only after this many seconds so a stray press doesn't.")]
    [SerializeField] private float skipAvailableAfter = 2f;

    [Tooltip("Hold B (gamepad) this long to skip.")]
    [SerializeField] private float holdToSkipDuration = 1f;
    private float skipHold;
    private bool leaving;

    private IEnumerator Start()
    {
        // The run is over - clear persistent state now so the menu starts a clean game.
        GameReset.ResetAll();

        if (!string.IsNullOrEmpty(musicId))
        {
            float waited = 0f;
            while (AudioManager.instance == null && waited < 2f) { waited += Time.unscaledDeltaTime; yield return null; }   // never hold the scroll up for music
            if (AudioManager.instance != null) AudioManager.instance.PlayMusic(musicId, musicFadeIn);
        }

        if (content != null)
        {
            // The scroll maths needs the top of Content to be its reference point, so force top anchors + top pivot
            // (a middle pivot started it half-way up), then measure its real height.
            content.anchorMin = new Vector2(content.anchorMin.x, 1f);
            content.anchorMax = new Vector2(content.anchorMax.x, 1f);
            content.pivot = new Vector2(content.pivot.x, 1f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        if (content != null && viewport != null)
        {
            // Start just below the bottom edge.
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, -viewport.rect.height);
        }

        yield return new WaitForSecondsRealtime(startDelay);

        if (content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(content);   // text blocks in a layout group: measure after layout
        float viewH = viewport != null ? viewport.rect.height : Screen.height;
        float contentH = content != null ? content.rect.height : 0f;
        float startY = content != null ? content.anchoredPosition.y : 0f;
        float totalDistance = viewH + contentH;   // from fully below the screen to fully above it
        float scrolled = 0f;

        float t = 0f;
        while (!leaving && content != null && scrolled < totalDistance)
        {
            t += Time.unscaledDeltaTime;
            if (t > skipAvailableAfter)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { Leave(); yield break; }
                if (Input.GetKey(KeyCode.JoystickButton1))
                {
                    skipHold += Time.unscaledDeltaTime;
                    if (skipHold >= holdToSkipDuration) { Leave(); yield break; }
                }
                else skipHold = 0f;
            }

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
            return;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}