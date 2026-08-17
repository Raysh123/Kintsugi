using System.Collections;
using UnityEngine;

/// <summary>
/// Recovery Interaction Type A — now powered by FillBarMinigame instead of
/// Pointer. meter is activated and reset when the activity starts, and this
/// script polls meter.finished instead of waiting on a fixed duration —
/// FillBarMinigame still runs for its own internal fixed duration, but this
/// keeps the wrapper's shape consistent with Type B/C rather than hardcoding
/// the timing twice in two places.
/// </summary>
public class RecoveryInteraction : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("The child GameObject that holds only the player's visible mesh/rig. " +
             "This is toggled off during the activity and back on after — nothing " +
             "else on the player is touched.")]
    [SerializeField] private GameObject playerMeshChild;

    [Header("Activity Model")]
    [Tooltip("A dedicated model/rig for this specific interaction. Enabled only while " +
             "the activity is running, then disabled again. Should be disabled by default in the scene.")]
    [SerializeField] private GameObject activityModel;
    [Tooltip("The Animator on the activity model.")]
    [SerializeField] private Animator activityAnimator;
    [Tooltip("Name of the animation state to play on the activity model.")]
    [SerializeField] private string activityAnimation;

    [Header("Minigame")]
    [Tooltip("The FillBarMinigame component for this activity. Starts disabled in the scene.")]
    [SerializeField] private FillBarMinigame meter;
    private bool meterActive = false;

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
        if (meter != null) meter.gameObject.SetActive(false);
        if (activityModel != null) activityModel.SetActive(false);
    }

    void Update()
    {
        if (canInteract && player.interacting && !meterActive)
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
            if (activityAnimator != null && !string.IsNullOrEmpty(activityAnimation))
                activityAnimator.Play(activityAnimation);
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