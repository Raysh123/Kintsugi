using System.Collections;
using UnityEngine;

/// <summary>
/// Recovery Interaction Type B � now powered by TrackerRhythmMinigame instead
/// of TimedPointer. meter is activated and reset when the activity starts,
/// and this script polls meter.finished instead of a fixed WaitForSeconds �
/// TrackerRhythmMinigame owns its own win/loss/injury timing internally now,
/// so the old external "did they hit winCount in time" tracking is gone.
/// </summary>
public class Recovery_Interaction_Type_B : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("The child GameObject that holds only the player's visible mesh/rig. " +
             "This is toggled off during the activity and back on after.")]
    [SerializeField] private GameObject playerMeshChild;

    [Header("Activity Model")]
    [Tooltip("A dedicated model/rig for this specific interaction. Should be disabled by default in the scene.")]
    [SerializeField] private GameObject activityModel;
    [Tooltip("The Animator on the activity model.")]
    [SerializeField] private Animator activityAnimator;
    [Tooltip("Animation state to play when the interaction begins (the 'hold' or 'doing' pose).")]
    [SerializeField] private string activityAnimation;
    [Tooltip("Animation state to play when the player hits the input key during the interaction (the 'action' moment).")]
    [SerializeField] private string actionAnimation;
    [Tooltip("Animation state to play on a wrong-timing miss � a reaction/flinch, separate from the generic action pose.")]
    [SerializeField] private string missAnimation;
    [Tooltip("Animation state to play specifically on the miss that triggers Injured � a bigger, distinct " +
             "reaction from the regular miss flinch above.")]
    [SerializeField] private string injuredAnimation;

    [Header("Minigame")]
    [Tooltip("The TrackerRhythmMinigame component for this activity. Starts disabled in the scene.")]
    [SerializeField] private TrackerRhythmMinigame meter;
    private bool meterActive = false;

    [Header("Calendar")]
    [SerializeField] private ActivityDefinition activity;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Rewards / Penalties")]
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
        ? $"Recovery_Interaction_Type_B_{GetInstanceID()}_onboarding_seen"
        : onboardingFlagId;

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
        if (meter != null) meter.gameObject.SetActive(false);
        if (activityModel != null) activityModel.SetActive(false);
    }

    private void OnEnable()
    {
        if (meter == null) return;
        meter.OnMissedHit += HandleMissedHit;
        meter.OnInjuryTriggered += HandleInjuryTriggered;
    }

    private void OnDisable()
    {
        if (meter == null) return;
        meter.OnMissedHit -= HandleMissedHit;
        meter.OnInjuryTriggered -= HandleInjuryTriggered;
    }

    private void HandleMissedHit()
    {
        RecoverySoundscape.ActivityMiss(activity);
        if (activityAnimator != null && !string.IsNullOrEmpty(missAnimation))
            activityAnimator.Play(missAnimation);
    }

    private void HandleInjuryTriggered()
    {
        if (activityAnimator != null && !string.IsNullOrEmpty(injuredAnimation))
            activityAnimator.Play(injuredAnimation);
    }

    void Update()
    {
        if (canInteract && player.interacting && !meterActive)
            BeginInteraction();

        // Player hits the action input during the interaction � play the action anim
        // on the activity model. Purely cosmetic, doesn't affect the minigame's own
        // hit detection (that's handled entirely inside TrackerRhythmMinigame).
        if (meterActive && activityAnimator != null)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
            {
                RecoverySoundscape.ActivityAction(activity);
                if (!string.IsNullOrEmpty(actionAnimation))
                    activityAnimator.Play(actionAnimation);
            }
        }
    }

    private void BeginInteraction()
    {
        if (!CalendarManager.instance.TryConsumeActivity(activity))
        {
            player.interacting = false;
            Debug.Log($"{activity.displayName} already done today.");
            return;
        }

        if (!StoryEnvironmentController.instance.CanDoActivity())
        {
            player.interacting = false;
            Debug.Log("Too tired to do more today.");
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
            else
                Debug.LogWarning("[Recovery_Interaction_Type_B] onboardingUI shown but PersistentGameFlags.instance " +
                                  "is null - this won't be remembered across scene reloads.");
        }

        // Hide player mesh, show activity model
        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (activityModel != null)
        {
            activityModel.SetActive(true);
            if (activityAnimator != null && !string.IsNullOrEmpty(activityAnimation))
                activityAnimator.Play(activityAnimation);
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
            Debug.Log("Won");
            RecoverySoundscape.ActivityWon(activity);
            HealthAndMotivation.instance.ModifyHealth(winHealth);
            HealthAndMotivation.instance.ModifyMotivation(winMotivation);
        }
        else if (meter.lost)
        {
            Debug.Log("Lost");
            RecoverySoundscape.ActivityLost(activity);
            HealthAndMotivation.instance.ModifyHealth(loseHealth);
            HealthAndMotivation.instance.ModifyMotivation(loseMotivation);
        }
        else if (meter.injured)
        {
            Debug.Log("Injured");
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
}