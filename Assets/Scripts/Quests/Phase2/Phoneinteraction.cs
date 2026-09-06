using System.Collections;
using UnityEngine;

/// <summary>
/// A phone the player interacts with to trigger a call. The trigger collider
/// itself stays disabled until DojoRejection reaches Evening — the player
/// can't even enter the trigger zone outside that window, not just blocked
/// from acting once inside it. Disabled again permanently once the call ends
/// (this is a one-time story beat, not repeatable).
///
/// Locks player movement and camera orbit rotation for the duration of the
/// call, and advances the calendar straight to Night once it finishes.
///
/// Uses ConversationRunner rather than a plain DialogueRunner since this is a
/// genuine back-and-forth (sensei speaks, player responds, back to sensei).
///
/// Applies motivationLossOnComplete once, the first time this call's
/// conversation finishes — e.g. the sensei's rejection landing.
/// </summary>
public class PhoneInteraction : MonoBehaviour
{
    [SerializeField] private ConversationRunner conversationRunner;
    [Tooltip("Optional prompt — a world space UI that says 'Press E to call' etc.")]
    [SerializeField] private GameObject interactPrompt;
    [Tooltip("Motivation lost once this call's conversation finishes for the first time. 0 = no change.")]
    [SerializeField] private float motivationLossOnComplete = 0f;

    [Header("Availability Window")]
    [Tooltip("Only usable during DojoRejection, Evening. Auto-resolved from this GameObject if left blank.")]
    [SerializeField] private Collider triggerCollider;

    [Header("Lock During Call")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("Optional direct reference. If left blank, resolved automatically from Camera.main at Start.")]
    [SerializeField] private TopDownCameraFollow cameraFollow;

    private bool playerInRange;
    private bool motivationLossApplied;
    private bool hasBeenUsed;
    private bool cameraOrbitWasEnabled;

    private void Start()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.enabled = false;

        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<TopDownCameraFollow>();

        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (StoryManager.instance == null || CalendarManager.instance == null)
            yield return null;

        StoryManager.instance.OnPhaseChanged += HandleStoryPhaseChanged;
        CalendarManager.instance.OnPhaseChanged += HandleTimeOfDayChanged;
        UpdateColliderAvailability();
    }

    private void OnDestroy()
    {
        if (StoryManager.instance != null)
            StoryManager.instance.OnPhaseChanged -= HandleStoryPhaseChanged;
        if (CalendarManager.instance != null)
            CalendarManager.instance.OnPhaseChanged -= HandleTimeOfDayChanged;
    }

    private void HandleStoryPhaseChanged(StoryPhase old, StoryPhase newPhase) => UpdateColliderAvailability();
    private void HandleTimeOfDayChanged(DayPhase newTimeOfDay) => UpdateColliderAvailability();

    private void UpdateColliderAvailability()
    {
        if (hasBeenUsed || triggerCollider == null) return;

        bool available = StoryManager.instance != null
            && StoryManager.instance.CurrentPhase == StoryPhase.DojoRejection
            && CalendarManager.instance != null
            && CalendarManager.instance.CurrentPhase == DayPhase.Evening;

        triggerCollider.enabled = available;
    }

    private void OnEnable()
    {
        if (conversationRunner != null)
            conversationRunner.OnDialogueComplete += HandleDialogueComplete;
    }

    private void OnDisable()
    {
        if (conversationRunner != null)
            conversationRunner.OnDialogueComplete -= HandleDialogueComplete;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        player = other.GetComponent<PlayerMovement>();
        if (interactPrompt != null) interactPrompt.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (!playerInRange || conversationRunner == null || conversationRunner.IsRunning) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            StartCall();
    }

    private void StartCall()
    {
        if (player != null) player.movementLocked = true;

        if (cameraFollow != null)
        {
            cameraOrbitWasEnabled = cameraFollow.enableOrbitRotation;
            cameraFollow.enableOrbitRotation = false;
        }

        conversationRunner.Show();
    }

    private void HandleDialogueComplete()
    {
        if (!motivationLossApplied && motivationLossOnComplete != 0f && HealthAndMotivation.instance != null)
        {
            HealthAndMotivation.instance.ModifyMotivation(-Mathf.Abs(motivationLossOnComplete));
            motivationLossApplied = true;
        }

        if (player != null) player.movementLocked = false;
        if (cameraFollow != null) cameraFollow.enableOrbitRotation = cameraOrbitWasEnabled;

        if (triggerCollider != null) triggerCollider.enabled = false;
        hasBeenUsed = true;

        if (CalendarManager.instance != null)
        {
            int ticksToNight = CalendarManager.TicksPerQuadrant - CalendarManager.instance.TickWithinQuadrant;
            CalendarManager.instance.AdvanceTime(ticksToNight);
        }

        Destroy(gameObject);
    }
}