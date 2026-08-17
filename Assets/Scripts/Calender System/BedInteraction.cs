using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bed interaction for the BedRest story phase. While the story is in BedRest,
/// the player's normal model stays hidden for the ENTIRE phase — not just
/// during an activity — replaced by this script's activityModel, which loops
/// bedIdleAnimation by default and only switches to recoveryAnimation /
/// fallAsleepAnimation / wakeUpAnimation for the relevant moment. The moment
/// the story advances past BedRest, the normal model comes back and this
/// script gets out of the way entirely.
///
/// Division of responsibility: StoryEnvironmentController already locks
/// PlayerMovement (movementLocked) and gates the daily activity count by
/// health during BedRest — this script only owns the bed's own model/anim
/// swap and its two interactions:
///
///   E / Submit      — do a physio recovery activity on the bed
///   Space / Cancel  — go to sleep (fade out, wake up anim, fade in, new day)
/// </summary>
public class BedInteraction : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private GameObject playerMeshChild;

    [Header("Activity Model")]
    [Tooltip("Dedicated model for bed interactions — this becomes the player's visible " +
             "representation for the entire BedRest phase, not just during an activity.")]
    [SerializeField] private GameObject activityModel;
    [SerializeField] private Animator activityAnimator;
    [Tooltip("Looping animation played whenever the player is in bed but not doing an " +
             "activity or sleeping — the default state for the whole BedRest phase.")]
    [SerializeField] private string bedIdleAnimation;
    [Tooltip("Animation that plays while doing a bed recovery activity.")]
    [SerializeField] private string recoveryAnimation;
    [Tooltip("Animation that plays when the player falls asleep.")]
    [SerializeField] private string fallAsleepAnimation;
    [Tooltip("Animation that plays when the player wakes up.")]
    [SerializeField] private string wakeUpAnimation;

    [Header("Bed Rest Phase Entry/Exit (plays once, not every night)")]
    [Tooltip("Plays once when BedRest begins — waking up in bed for the first time, " +
             "before settling into the idle loop.")]
    [SerializeField] private string bedRestEntryAnimation;
    [SerializeField] private float bedRestEntryDuration = 1.5f;
    [Tooltip("Plays once when BedRest ends — getting out of bed for good, before " +
             "handing back to the normal player model.")]
    [SerializeField] private string bedRestExitAnimation;
    [SerializeField] private float bedRestExitDuration = 1.5f;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Fade")]
    [Tooltip("A full-screen black Image on a Canvas set to Screen Space - Overlay. " +
             "Set its alpha to 0 by default.")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Calendar")]
    [SerializeField] private ActivityDefinition recoveryActivity;

    [Header("Rewards & Penalties")]
    [SerializeField] private float winHealth;
    [SerializeField] private float winMotivation;
    [SerializeField] private float loseHealth;
    [SerializeField] private float loseMotivation;
    [SerializeField] private float injuryHealth;
    [SerializeField] private float injuryMotivation;

    [Header("Minigame (optional bed recovery)")]
    [SerializeField] private Pointer pointer;
    [SerializeField] private GameObject meter;

    private bool canInteract;
    private bool meterActive;
    private bool isSleeping;
    private bool wasBedRestActive;

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
        if (meter != null) meter.SetActive(false);
        if (fadeImage != null) SetFadeAlpha(0f);

        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (StoryManager.instance == null) yield return null;

        StoryManager.instance.OnPhaseChanged += HandlePhaseChanged;
        ApplyPhaseVisual(StoryManager.instance.CurrentPhase);
    }

    private void OnDestroy()
    {
        if (StoryManager.instance != null)
            StoryManager.instance.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void Update()
    {
        if (!canInteract || isSleeping) return;

        // E — start bed recovery activity
        if (!meterActive && !player.interacting)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit"))
                BeginRecovery();
        }

        // Space — go to sleep (available any time while in trigger, not just during activity)
        if (!meterActive && !player.interacting)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
                StartCoroutine(SleepSequence());
        }
    }

    // ── Phase-driven model swap ──────────────────────────────────────────────────

    private void HandlePhaseChanged(StoryPhase old, StoryPhase newPhase)
    {
        ApplyPhaseVisual(newPhase);
    }

    /// <summary>
    /// Handles both the one-time BedRest entry/exit transition (playing
    /// bedRestEntryAnimation / bedRestExitAnimation) and the routine case of
    /// just resuming bedIdleAnimation after an activity/sleep sequence ends
    /// while still in BedRest. Skipped while an activity/sleep sequence is
    /// mid-flight — those own the visual state moment-to-moment and call this
    /// themselves once they finish.
    /// </summary>
    private void ApplyPhaseVisual(StoryPhase phase)
    {
        if (meterActive || isSleeping) return;

        bool isBedRest = phase == StoryPhase.BedRest;

        if (isBedRest == wasBedRestActive)
        {
            // No transition — just make sure the idle loop is running (e.g. after an activity ends)
            if (isBedRest && activityAnimator != null && !string.IsNullOrEmpty(bedIdleAnimation))
                activityAnimator.Play(bedIdleAnimation);
            return;
        }

        wasBedRestActive = isBedRest;
        StartCoroutine(TransitionPhaseVisual(isBedRest));
    }

    private IEnumerator TransitionPhaseVisual(bool enteringBedRest)
    {
        if (enteringBedRest)
        {
            if (playerMeshChild != null) playerMeshChild.SetActive(false);
            if (activityModel != null) activityModel.SetActive(true);

            if (activityAnimator != null && !string.IsNullOrEmpty(bedRestEntryAnimation))
            {
                activityAnimator.Play(bedRestEntryAnimation);
                yield return new WaitForSeconds(bedRestEntryDuration);
            }

            if (activityAnimator != null && !string.IsNullOrEmpty(bedIdleAnimation))
                activityAnimator.Play(bedIdleAnimation);
        }
        else
        {
            if (activityAnimator != null && !string.IsNullOrEmpty(bedRestExitAnimation))
            {
                activityAnimator.Play(bedRestExitAnimation);
                yield return new WaitForSeconds(bedRestExitDuration);
            }

            if (activityModel != null) activityModel.SetActive(false);
            if (playerMeshChild != null) playerMeshChild.SetActive(true);
        }
    }

    // ── Recovery activity ────────────────────────────────────────────────────────

    private void BeginRecovery()
    {
        if (recoveryActivity == null) return;

        if (!CalendarManager.instance.TryConsumeActivity(recoveryActivity))
        {
            Debug.Log($"{recoveryActivity.displayName} already done today.");
            return;
        }

        if (!StoryEnvironmentController.instance.CanDoActivity())
        {
            Debug.Log("Too tired to do more today — rest and try again tomorrow.");
            return;
        }

        player.interacting = true;
        if (activityAnimator != null && !string.IsNullOrEmpty(recoveryAnimation))
            activityAnimator.Play(recoveryAnimation);

        SceneCameraManager.instance.ActivateCamera(activityCamera);
        meterActive = true;
        if (meter != null) meter.SetActive(true);
        StartCoroutine(RecoveryRoutine());
    }

    private IEnumerator RecoveryRoutine()
    {
        yield return new WaitForSeconds(10f);
        StartCoroutine(EndRecovery());
    }

    private IEnumerator EndRecovery()
    {
        if (pointer != null)
        {
            if (pointer.won)
            {
                Debug.Log("Won");
                HealthAndMotivation.instance.ModifyHealth(winHealth);
                HealthAndMotivation.instance.ModifyMotivation(winMotivation);
            }
            else if (pointer.lost)
            {
                Debug.Log("Lost");
                HealthAndMotivation.instance.ModifyHealth(loseHealth);
                HealthAndMotivation.instance.ModifyMotivation(loseMotivation);
            }
            else if (pointer.injured)
            {
                Debug.Log("Injured");
                HealthAndMotivation.instance.ModifyHealth(injuryHealth);
                HealthAndMotivation.instance.ModifyMotivation(injuryMotivation);
            }
        }

        yield return new WaitForSeconds(0.01f);
        SceneCameraManager.instance.DeactivateCamera(activityCamera);
        CalendarManager.instance.AdvanceTime(recoveryActivity.timeCostMinutes);
        if (meter != null) meter.SetActive(false);
        player.interacting = false;
        meterActive = false;

        // Back to bed-idle if still BedRest, or hand off to the normal model if the
        // phase advanced while this activity was running (e.g. crossed into a new day).
        ApplyPhaseVisual(StoryManager.instance.CurrentPhase);
    }

    // ── Sleep sequence ──────────────────────────────────────────────────────────

    private IEnumerator SleepSequence()
    {
        isSleeping = true;
        player.interacting = true;

        if (activityAnimator != null && !string.IsNullOrEmpty(fallAsleepAnimation))
            activityAnimator.Play(fallAsleepAnimation);

        SceneCameraManager.instance.ActivateCamera(activityCamera);

        // Wait a moment for the fall-asleep anim to play before fading
        yield return new WaitForSeconds(1.5f);

        // Fade to black
        yield return StartCoroutine(Fade(0f, 1f));

        // Advance the calendar to next morning while screen is black — this may
        // trigger StoryManager to advance past BedRest if the day threshold is hit
        CalendarManager.instance.Sleep();

        // Short pause on black screen
        yield return new WaitForSeconds(0.5f);

        bool stillBedRest = StoryManager.instance != null
            && StoryManager.instance.CurrentPhase == StoryPhase.BedRest;

        // Only play the in-bed wake-up beat if the story is still in BedRest —
        // if last night's sleep pushed the story past it, skip straight to
        // handing control back once we fade in.
        if (stillBedRest && activityAnimator != null && !string.IsNullOrEmpty(wakeUpAnimation))
            activityAnimator.Play(wakeUpAnimation);

        // Wait for wake-up anim to get going before fading back in
        yield return new WaitForSeconds(1f);

        // Fade back in
        yield return StartCoroutine(Fade(1f, 0f));

        // Small pause, then cut back to player camera
        yield return new WaitForSeconds(0.5f);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);

        player.interacting = false;
        isSleeping = false;

        // Bed-idle if still BedRest, or hand off to the normal model/control
        // if the phase advanced overnight.
        ApplyPhaseVisual(StoryManager.instance.CurrentPhase);
    }

    // ── Fade helper ─────────────────────────────────────────────────────────────

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
