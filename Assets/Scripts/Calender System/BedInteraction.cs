using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic "go to sleep" bed interaction — works in ANY story phase, at ANY
/// bed in the game. Space/Cancel to sleep: fade out, advance the calendar
/// via CalendarManager.Sleep(), fade in, done.
///
/// Defers entirely during the BedRest phase — BedRestPhaseController owns
/// sleep during that phase specifically, since it needs the player's model
/// to STAY as its bed rig throughout the whole phase rather than reverting
/// to the normal mesh after every sleep the way this script does.
/// </summary>
public class BedInteraction : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private GameObject playerMeshChild;

    [Header("Sleep Model")]
    [Tooltip("Dedicated model used only for the duration of the sleep cutscene — disabled by default in the scene.")]
    [SerializeField] private GameObject sleepModel;
    [SerializeField] private Animator sleepAnimator;
    [SerializeField] private string fallAsleepAnimation;
    [SerializeField] private string wakeUpAnimation;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Fade")]
    [Tooltip("A full-screen black Image on a Canvas set to Screen Space - Overlay. " +
             "Set its alpha to 0 by default.")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Prompt")]
    [Tooltip("A prompt (e.g. \"Space / B: Sleep\") shown only while the player is at the bed and it is actually " +
             "possible to sleep - at Night, and not during the BedRest phase. Keep it INACTIVE in the scene. Optional.")]
    [SerializeField] private GameObject interactPrompt;

    private bool canInteract;
    private bool isSleeping;

    /// <summary>
    /// Fired once ANY sleep sequence (this script or BedRestPhaseController's) has
    /// genuinely finished — fade back in complete, model restored. Static because
    /// it's a single shared signal regardless of which physical bed/script
    /// instance triggered the sleep. Used by OneTimePhaseDialogueTrigger to defer
    /// phase-entry dialogue until the player has actually visually woken up,
    /// rather than firing during the mid-sleep phase transition.
    /// </summary>
    public static event System.Action OnAnyWakeUp;

    /// <summary>Call this to fire OnAnyWakeUp — events can only be invoked from inside their " +
    /// declaring class, so external code (like BedRestPhaseController) calls this instead of " +
    /// trying to invoke the event directly.</summary>
    public static void RaiseWakeUp()
    {
        OnAnyWakeUp?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canInteract = true;
            player = other.GetComponent<PlayerMovement>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canInteract = false;
            player = null;
        }
    }

    private void Start()
    {
        if (sleepModel != null) sleepModel.SetActive(false);
        if (fadeImage != null) SetFadeAlpha(0f);
    }

    private bool promptMine;

    private void OnDisable()
    {
        SharedPrompt.Set(ref promptMine, interactPrompt, false);
    }

    /// <summary>True when pressing the sleep button right now would actually put the player to sleep.</summary>
    private bool CanSleepNow()
    {
        if (!canInteract || isSleeping || player == null || player.interacting) return false;
        if (ActiveDialogueTracker.AnyActive) return false;
        if (StoryManager.instance != null && StoryManager.instance.CurrentPhase == StoryPhase.BedRest) return false;
        return CalendarManager.instance != null && CalendarManager.instance.CurrentPhase == DayPhase.Night;
    }

    private void Update()
    {
        if (interactPrompt != null)
        {
            SharedPrompt.Set(ref promptMine, interactPrompt, CanSleepNow());
        }

        if (!canInteract || isSleeping || player == null || player.interacting) return;

        // Defer entirely during BedRest — BedRestPhaseController owns sleep for that phase.
        if (StoryManager.instance != null && StoryManager.instance.CurrentPhase == StoryPhase.BedRest)
            return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
        {
            if (CalendarManager.instance == null || CalendarManager.instance.CurrentPhase != DayPhase.Night)
                return;

            StartCoroutine(SleepSequence());
        }
    }

    public IEnumerator SleepSequence()
    {
        isSleeping = true;
        player.interacting = true;

        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (sleepModel != null)
        {
            sleepModel.SetActive(true);
            if (sleepAnimator != null && !string.IsNullOrEmpty(fallAsleepAnimation))
                sleepAnimator.Play(fallAsleepAnimation);
        }

        SceneCameraManager.instance.ActivateCamera(activityCamera);

        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(Fade(0f, 1f));

        CalendarManager.instance.Sleep();

        yield return new WaitForSeconds(0.5f);

        if (sleepAnimator != null && !string.IsNullOrEmpty(wakeUpAnimation))
            sleepAnimator.Play(wakeUpAnimation);

        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(Fade(1f, 0f));
        yield return new WaitForSeconds(0.5f);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);

        if (sleepModel != null) sleepModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);

        player.interacting = false;
        isSleeping = false;

        RaiseWakeUp();
        StartCoroutine(FreePlayerAfterWake());
    }

    /// <summary>Safety net: waking up must never leave the player stuck. Once the morning's dialogue(s) are over,
    /// every lock is released (except during BedRest, which owns its own locks).</summary>
    private IEnumerator FreePlayerAfterWake()
    {
        yield return null;
        float guard = 0f;
        while (guard < 60f)
        {
            while (ActiveDialogueTracker.AnyActive) { guard += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(1f);
            if (!ActiveDialogueTracker.AnyActive) break;
        }

        if (player == null || isSleeping) yield break;
        if (StoryManager.instance != null && StoryManager.instance.CurrentPhase == StoryPhase.BedRest) yield break;
        player.movementLocked = false;
        player.cutsceneLocked = false;
        player.interacting = false;
    }

    private IEnumerator Fade(float fromAlpha, float toAlpha)
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, elapsed / fadeDuration));
            yield return null;
        }
        SetFadeAlpha(toAlpha);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}