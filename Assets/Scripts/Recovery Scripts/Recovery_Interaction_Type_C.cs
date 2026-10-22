using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Recovery Interaction Type C � same pattern as RecoveryInteraction and
/// Recovery_Interaction_Type_B: this script does NOT listen for the E press
/// itself. PlayerMovement owns that � it detects this object via its trigger
/// tag ("Interact3"), and sets player.interacting = true when E is pressed
/// while typeC is true. This script just polls canInteract (its own trigger,
/// tag "Player") and player.interacting, same as the other two.
///
/// Wraps RhythmSequenceMinigame the way RecoveryInteraction wraps Pointer and
/// Recovery_Interaction_Type_B wraps TimedPointer. RhythmSequenceMinigame
/// itself is untouched � it only tracks input and outcome. This script owns
/// everything around it: the once-per-day/time-cost bookkeeping via
/// ActivityDefinition/CalendarManager, StoryEnvironmentController's daily
/// activity limit, camera/model swap, and applying health/motivation rewards
/// or penalties once the minigame ends.
///
/// One difference from the other two: Pointer/TimedPointer run for a fixed
/// duration, so their wrappers just wait that long then read the result.
/// RhythmSequenceMinigame runs until a win/fail/injury threshold is hit, so
/// this polls `finished` each frame instead of using a fixed WaitForSeconds.
///
/// Setup: tag this object's trigger collider "Interact3", and make sure
/// PlayerMovement has a matching typeC branch (see the updated PlayerMovement.cs).
/// </summary>
public class Recovery_Interaction_Type_C : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("The child GameObject that holds only the player's visible mesh/rig. " +
             "This is toggled off during the activity and back on after.")]
    [SerializeField] private GameObject playerMeshChild;

    [Header("Activity Model")]
    [Tooltip("A dedicated model/rig for this specific interaction. Should be disabled by default in the scene.")]
    [SerializeField] private GameObject activityModel;
    [SerializeField] private Animator activityAnimator;
    [Tooltip("Animation state to play when the activity starts, before the rhythm sequence begins.")]
    [SerializeField] private string startAnimation;

    [Header("Minigame")]
    [Tooltip("The RhythmSequenceMinigame component for this activity. Starts disabled in the scene.")]
    [SerializeField] private RhythmSequenceMinigame meter;
    private bool meterActive;

    [Header("Calendar")]
    [SerializeField] private ActivityDefinition activity;

    [Header("Interact Glow")]
    [Tooltip("Root of the nested particle effect that marks this spot. It plays while the interaction is available, " +
             "and stops during the interaction and once a once-a-day activity has been done (back on the next day). " +
             "Optional.")]
    [SerializeField] private GameObject interactGlow;

    [Tooltip("Optional text (child of the prompt) that shows what this activity gives and costs. Filled from this activity's own values.")]
    [SerializeField] private TMP_Text stakesText;

    [Header("Sounds (this activity only - blank / empty falls back to the Soundscape's overrides and defaults)")]
    [SerializeField] private RecoverySoundscape.ActivitySounds sounds = new RecoverySoundscape.ActivitySounds { startChime = "", win = "", lose = "", injured = "" };

    [Tooltip("A prompt (e.g. \"E / A: Interact\") shown while the player is in range and the activity is available. " +
             "Hidden during the activity and once a once-a-day activity has been done. Keep it INACTIVE in the scene. Optional.")]
    [SerializeField] private GameObject interactPrompt;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Rewards & Penalties")]
    [SerializeField] private float winHealth;
    [SerializeField] private float winMotivation;
    [SerializeField] private float loseHealth;
    [SerializeField] private float loseMotivation;
    [SerializeField] private float injuryHealth;
    [SerializeField] private float injuryMotivation;

    [Header("First-Time Tutorial")]
    [Tooltip("Shown the first time this interaction is ever used, before the minigame starts " +
             "- dismiss input pauses here same as BedRest's onboarding popups. Leave blank for " +
             "no tutorial on this interaction.")]
    [SerializeField] private GameObject onboardingUI;
    [Tooltip("Leave empty to auto-generate one from this component + the activity.")]
    [SerializeField] private string onboardingFlagId;

    private string OnboardingFlagId => string.IsNullOrEmpty(onboardingFlagId)
        ? $"Recovery_Interaction_Type_C_{gameObject.scene.name}_{HierarchyPath()}_onboarding_seen"
        : onboardingFlagId;

    private string HierarchyPath()
    {
        string p = name;
        for (Transform t = transform.parent; t != null; t = t.parent) p = t.name + "/" + p;
        return p;
    }

    private bool canInteract;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            canInteract = true;
            player = other.gameObject.GetComponent<PlayerMovement>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            canInteract = false;
            player = null;
        }
    }

    void Start()
    {
        RecoverySoundscape.SetOwnSounds(activity, sounds);
        ActivityStakes.Fill(stakesText, winHealth, winMotivation, loseHealth, loseMotivation, injuryHealth, injuryMotivation, activity != null ? activity.quadrantTicks : 0);
        if (activityModel != null) activityModel.SetActive(false);
        if (meter != null) meter.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (meter != null) meter.OnBeatResolved += HandleBeatResolved;
    }

    private void HandleBeatResolved(bool hit)
    {
        if (hit) RecoverySoundscape.ActivityAction(activity);
        else RecoverySoundscape.ActivityMiss(activity);
    }

    private void OnDisable()
    {
        if (meter != null) meter.OnBeatResolved -= HandleBeatResolved;
        SetGlow(false);
        SetPrompt(false);
        glowInitialised = false;
    }

    void Update()
    {
        SetGlow(ShouldGlow());
        SetPrompt(canInteract && !ActiveDialogueTracker.AnyActive && ShouldGlow());

        if (canInteract && player != null && player.interacting && !meterActive)
            BeginInteraction();
    }

    private void BeginInteraction()
    {
        if (!CalendarManager.instance.TryConsumeActivity(activity))
        {
            player.interacting = false;
            return;
        }

        if (!StoryEnvironmentController.instance.CanDoActivity())
        {
            player.interacting = false;
            return;
        }

        meterActive = true;
        RecoverySoundscape.ActivityStarted(activity);
        StartCoroutine(RunInteraction());
    }

    private IEnumerator RunInteraction()
    {
        // Tutorial popup pauses here, BEFORE the minigame actually starts - and only ever
        // the first time, same gating pattern as AudioEventTrigger/AudioCollisionTrigger's
        // onlyPlayOnce (PersistentGameFlags), so it won't re-show on later visits.
        if (onboardingUI != null &&
            !(PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(OnboardingFlagId)))
        {
            onboardingUI.SetActive(true);
            yield return new WaitUntil(() =>
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.JoystickButton1) ||
                Input.GetButtonDown("Cancel"));
            onboardingUI.SetActive(false);

            if (PersistentGameFlags.instance != null)
                PersistentGameFlags.instance.SetFlag(OnboardingFlagId);
        }

        // Hide player mesh, show activity model
        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (activityModel != null)
        {
            activityModel.SetActive(true);
            if (activityAnimator != null && !string.IsNullOrEmpty(startAnimation))
                activityAnimator.Play(startAnimation);
        }

        SceneCameraManager.instance.ActivateCamera(activityCamera);
        RecoverySoundscape.ActivityLoopStart(activity);

        // Activating fires Start() the very first time only, so ResetMinigame()
        // is what actually (re)starts it on every attempt, including the
        // first, where it's a harmless no-op on top of Start().
        meter.gameObject.SetActive(true);
        meter.ResetMinigame();

        while (!meter.finished)
            yield return null;

        yield return EndInteraction();
    }

    private IEnumerator EndInteraction()
    {
        if (meter.won)
        {
            RecoverySoundscape.ActivityWon(activity);
            HealthAndMotivation.instance.ModifyHealth(winHealth);
            HealthAndMotivation.instance.ModifyMotivation(winMotivation);
        }
        else if (meter.lost)
        {
            RecoverySoundscape.ActivityLost(activity);
            HealthAndMotivation.instance.ModifyHealth(loseHealth);
            HealthAndMotivation.instance.ModifyMotivation(loseMotivation);
        }
        else if (meter.injured)
        {
            RecoverySoundscape.ActivityInjured(activity);
            HealthAndMotivation.instance.ModifyHealth(injuryHealth);
            HealthAndMotivation.instance.ModifyMotivation(injuryMotivation);
        }

        yield return new WaitForSeconds(0.01f);

        // Restore player mesh, hide activity model
        if (activityModel != null) activityModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);
        CalendarManager.instance.AdvanceTime(activity.quadrantTicks);

        meter.gameObject.SetActive(false);
        RecoverySoundscape.ActivityLoopStop(activity);

        player.interacting = false;
        meterActive = false;
    }

    private bool promptMine;

    private void SetPrompt(bool on)
    {
        // The prompt object / text may be shared with other interactions, so only this script's own show is undone by it.
        if (on)
        {
            if (!promptMine) { promptMine = true; ActivityStakes.Fill(stakesText, winHealth, winMotivation, loseHealth, loseMotivation, injuryHealth, injuryMotivation, activity != null ? activity.quadrantTicks : 0); }
            if (interactPrompt != null && !interactPrompt.activeSelf) interactPrompt.SetActive(true);
            if (stakesText != null && !stakesText.gameObject.activeSelf) stakesText.gameObject.SetActive(true);
        }
        else if (promptMine)
        {
            promptMine = false;
            if (interactPrompt != null && interactPrompt.activeSelf) interactPrompt.SetActive(false);
            if (stakesText != null) stakesText.gameObject.SetActive(false);
        }
    }

    private bool glowInitialised;
    private bool glowOn;

    private bool ShouldGlow()
    {
        if (meterActive) return false;

        return activity == null || !activity.onceLimitPerDay || CalendarManager.instance == null ||
               !CalendarManager.instance.HasDoneToday(activity.activityId);
    }

    private void SetGlow(bool on)
    {
        if (interactGlow == null || (glowInitialised && glowOn == on)) return;

        glowInitialised = true;
        glowOn = on;

        foreach (ParticleSystem ps in interactGlow.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (on) ps.Play(false);
            else ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}