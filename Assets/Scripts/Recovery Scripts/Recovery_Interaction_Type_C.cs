using System.Collections;
using UnityEngine;

/// <summary>
/// Recovery Interaction Type C — same pattern as RecoveryInteraction and
/// Recovery_Interaction_Type_B: this script does NOT listen for the E press
/// itself. PlayerMovement owns that — it detects this object via its trigger
/// tag ("Interact3"), and sets player.interacting = true when E is pressed
/// while typeC is true. This script just polls canInteract (its own trigger,
/// tag "Player") and player.interacting, same as the other two.
///
/// Wraps RhythmSequenceMinigame the way RecoveryInteraction wraps Pointer and
/// Recovery_Interaction_Type_B wraps TimedPointer. RhythmSequenceMinigame
/// itself is untouched — it only tracks input and outcome. This script owns
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

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Rewards & Penalties")]
    [SerializeField] private float winHealth;
    [SerializeField] private float winMotivation;
    [SerializeField] private float loseHealth;
    [SerializeField] private float loseMotivation;
    [SerializeField] private float injuryHealth;
    [SerializeField] private float injuryMotivation;

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
        if (activityModel != null) activityModel.SetActive(false);
        if (meter != null) meter.gameObject.SetActive(false);
    }

    void Update()
    {
        if (canInteract && player != null && player.interacting && !meterActive)
            BeginInteraction();
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

        // Hide player mesh, show activity model
        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (activityModel != null)
        {
            activityModel.SetActive(true);
            if (activityAnimator != null && !string.IsNullOrEmpty(startAnimation))
                activityAnimator.Play(startAnimation);
        }

        SceneCameraManager.instance.ActivateCamera(activityCamera);

        meterActive = true;

        // Activating fires Start() the very first time only, so ResetMinigame()
        // is what actually (re)starts it on every attempt — including the
        // first, where it's a harmless no-op on top of Start().
        meter.gameObject.SetActive(true);
        meter.ResetMinigame();

        StartCoroutine(WaitForMeter());
    }

    private IEnumerator WaitForMeter()
    {
        while (!meter.finished)
            yield return null;

        yield return EndInteraction();
    }

    private IEnumerator EndInteraction()
    {
        if (meter.won)
        {
            Debug.Log("Won");
            HealthAndMotivation.instance.ModifyHealth(winHealth);
            HealthAndMotivation.instance.ModifyMotivation(winMotivation);
        }
        else if (meter.lost)
        {
            Debug.Log("Lost");
            HealthAndMotivation.instance.ModifyHealth(loseHealth);
            HealthAndMotivation.instance.ModifyMotivation(loseMotivation);
        }
        else if (meter.injured)
        {
            Debug.Log("Injured");
            HealthAndMotivation.instance.ModifyHealth(injuryHealth);
            HealthAndMotivation.instance.ModifyMotivation(injuryMotivation);
        }

        yield return new WaitForSeconds(0.01f);

        // Restore player mesh, hide activity model
        if (activityModel != null) activityModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);
        CalendarManager.instance.AdvanceTime(activity.timeCostMinutes);

        meter.gameObject.SetActive(false);

        player.interacting = false;
        meterActive = false;
    }
}