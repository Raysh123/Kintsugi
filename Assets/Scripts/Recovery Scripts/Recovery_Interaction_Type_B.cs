using System.Collections;
using UnityEngine;

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
    [Tooltip("Animation state to play when the player hits Space during the interaction (the 'action' moment).")]
    [SerializeField] private string actionAnimation;

    [Header("Minigame")]
    [SerializeField] private GameObject meter;
    [SerializeField] private TimedPointer timedPointer;
    [SerializeField] private GameObject timedPointerObj;
    private bool meterActive = false;
    public bool lost;
    public bool ending;

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
        lost = false;
        ending = false;
        meter.gameObject.SetActive(false);
        if (activityModel != null) activityModel.SetActive(false);
    }

    void Update()
    {
        if (canInteract && player.interacting && !meterActive)
            BeginInteraction();

        // Player hits action input during interaction — play the action anim on the activity model
        if (meterActive && activityAnimator != null)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
            {
                if (!string.IsNullOrEmpty(actionAnimation))
                    activityAnimator.Play(actionAnimation);
            }
        }

        if (timedPointer.won || timedPointer.injured)
        {
            ending = true;
            if (ending) EndMinigame();
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
        meter.gameObject.SetActive(true);
        StartCoroutine(Interaction());
    }

    public void EndMinigame()
    {
        StopAllCoroutines();
        StartCoroutine(EndInteraction());
        ending = false;
    }

    IEnumerator Interaction()
    {
        yield return new WaitForSeconds(20);
        if (timedPointer.currentSuccessNumber != timedPointer.winCount)
            lost = true;
        StartCoroutine(EndInteraction());
    }

    IEnumerator EndInteraction()
    {
        if (timedPointer.won)
        {
            Debug.Log("Won");
            HealthAndMotivation.instance.ModifyHealth(winHealth);
            HealthAndMotivation.instance.ModifyMotivation(winMotivation);
        }
        else if (lost)
        {
            Debug.Log("Lost");
            HealthAndMotivation.instance.ModifyHealth(loseHealth);
            HealthAndMotivation.instance.ModifyMotivation(loseMotivation);
        }
        else if (timedPointer.injured)
        {
            Debug.Log("Injured");
            HealthAndMotivation.instance.ModifyHealth(injuryHealth);
            HealthAndMotivation.instance.ModifyMotivation(injuryMotivation);
        }

        timedPointer.won = false;
        timedPointer.injured = false;
        timedPointer.currentFailNumber = 0;
        timedPointer.currentSuccessNumber = 0;

        yield return new WaitForSeconds(0.01f);

        // Restore player mesh, hide activity model
        if (activityModel != null) activityModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);
        CalendarManager.instance.AdvanceTime(activity.timeCostMinutes);
        lost = false;
        meter.gameObject.SetActive(false);
        player.interacting = false;
        meterActive = false;
    }
}