using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything specific to the BedRest story phase: the phase-locked model
/// swap (player's normal model hidden for the WHOLE phase, replaced by this
/// script's activityModel), the entry/exit transition poses, the 6-slot
/// recovery activity system, and BedRest's own sleep sequence — which keeps
/// activityModel active throughout rather than reverting to the normal mesh
/// after every night, unlike the generic BedInteraction.
///
/// This script is always resident and self-gates on the current story phase
/// internally — it's never meant to be enabled/disabled externally. Outside
/// BedRest, its Update() simply no-ops; BedInteraction (a separate, generic
/// script) handles ordinary sleep everywhere else in the game instead.
///
/// The recovery activity itself runs off an ordered list of slots
/// (BedActivitySlot) — Type A morning, Type B afternoon, Type C evening,
/// repeated across however many days BedRest lasts. Completing one slot
/// advances to the next automatically. Three shared minigame instances (one
/// per type) are reused across every slot that needs that type — slots are
/// just configuration (which ActivityDefinition, rewards, onboarding UI,
/// whether the health/motivation meter shows), not separate GameObjects.
/// </summary>
public class BedRestPhaseController : MonoBehaviour
{
    public enum BedActivityType { TypeA, TypeB, TypeC }

    [System.Serializable]
    public class BedActivitySlot
    {
        [Tooltip("Inspector-only label, e.g. 'Day 1 - Morning'. Not used at runtime.")]
        public string label;
        public BedActivityType activityType;
        public ActivityDefinition activity;

        [Header("Minigame Instance (fill in only the one matching Activity Type above)")]
        [Tooltip("Each slot gets its own dedicated instance — not shared — so Day 1 and Day 2 " +
                 "versions of the same type can each have independently tuned base difficulty, " +
                 "not just different rewards.")]
        public FillBarMinigame typeAMinigame;
        public TrackerRhythmMinigame typeBMinigame;
        public RhythmSequenceMinigame typeCMinigame;

        [Header("Activity Pose")]
        [Tooltip("Base pose animation for this specific activity — e.g. a deadlift pose for Type A, " +
                 "holding kick pads for Type B. Falls back to the shared Recovery Animation on the " +
                 "controller if left blank.")]
        public string activityAnimationState;

        [Header("Type A Animation (ignored for other types)")]
        [Tooltip("Animator float parameter driving Motion Time on the pose above — set that state to " +
                 "use Motion Time in the Animator Controller and assign it this parameter name.")]
        public string fillProgressParam;
        public string typeAWinAnimation;
        public string typeALoseAnimation;
        public string typeAInjuredAnimation;

        [Header("Type B Animation (ignored for other types)")]
        public string typeBMissAnimation;
        public string typeBInjuredAnimation;

        [Header("Rewards / Penalties")]
        public float winHealth;
        public float winMotivation;
        public float loseHealth;
        public float loseMotivation;
        public float injuryHealth;
        public float injuryMotivation;

        [Header("Presentation")]
        [Tooltip("Shown while this slot's minigame runs — e.g. a first-time tutorial/onboarding hint.")]
        public GameObject onboardingUI;
        [Tooltip("This activity's own gameplay UI — e.g. its FillBarMeterUI/TrackerRhythmMeterUI/" +
                 "BoardPromptUI panel. Shown once onboarding is dismissed and the minigame actually " +
                 "starts, hidden again once it finishes.")]
        public GameObject activityUI;
        [Tooltip("If true, the shared health/motivation meter UI is shown during this slot.")]
        public bool showHealthMotivationMeter;
    }

    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private GameObject playerMeshChild;

    [Header("Activity Model")]
    [Tooltip("Becomes the player's visible representation for the ENTIRE BedRest phase, " +
             "not just during an activity or sleep.")]
    [SerializeField] private GameObject activityModel;
    [SerializeField] private Animator activityAnimator;
    [Tooltip("Looping animation played whenever the player is in bed but not doing an " +
             "activity or sleeping — the default state for the whole phase.")]
    [SerializeField] private string bedIdleAnimation;
    [Tooltip("Fallback pose used when a slot doesn't specify its own Activity Animation State.")]
    [SerializeField] private string recoveryAnimation;
    [SerializeField] private string fallAsleepAnimation;
    [SerializeField] private string wakeUpAnimation;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Fade")]
    [SerializeField] private UnityEngine.UI.Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Bed Rest Activity Slots")]
    [Tooltip("Runs in order — completing one automatically advances to the next.")]
    [SerializeField] private List<BedActivitySlot> slots = new List<BedActivitySlot>();
    private int currentSlotIndex;

    [Header("Shared UI")]
    [SerializeField] private GameObject healthMotivationMeterUI;

    private bool canInteract;
    private bool meterActive;
    private bool isSleeping;

    private bool IsBedRestPhase =>
        StoryManager.instance != null && StoryManager.instance.CurrentPhase == StoryPhase.BedRest;

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
        if (fadeImage != null) SetFadeAlpha(0f);

        if (healthMotivationMeterUI != null) healthMotivationMeterUI.SetActive(false);
        foreach (BedActivitySlot slot in slots)
        {
            if (slot.onboardingUI != null) slot.onboardingUI.SetActive(false);
            if (slot.activityUI != null) slot.activityUI.SetActive(false);
            if (slot.typeAMinigame != null) slot.typeAMinigame.gameObject.SetActive(false);
            if (slot.typeBMinigame != null) slot.typeBMinigame.gameObject.SetActive(false);
            if (slot.typeCMinigame != null) slot.typeCMinigame.gameObject.SetActive(false);
        }

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
        // Self-gated: entirely inert outside BedRest, regardless of trigger state.
        if (!IsBedRestPhase) return;
        if (!canInteract || isSleeping || player == null) return;

        if (!meterActive && !player.interacting)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit"))
                BeginRecovery();
        }

        if (!meterActive && !player.interacting)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
            {
                if (CalendarManager.instance == null || CalendarManager.instance.CurrentPhase != DayPhase.Night)
                {
                    Debug.Log("Can only sleep at Night.");
                }
                else
                {
                    StartCoroutine(SleepSequence());
                }
            }
        }
    }

    // ── Phase-driven model swap ──────────────────────────────────────────────────

    private void HandlePhaseChanged(StoryPhase old, StoryPhase newPhase)
    {
        ApplyPhaseVisual(newPhase);
    }

    private void ApplyPhaseVisual(StoryPhase phase)
    {
        if (meterActive || isSleeping) return;

        bool isBedRest = phase == StoryPhase.BedRest;

        if (playerMeshChild != null) playerMeshChild.SetActive(!isBedRest);
        if (activityModel != null) activityModel.SetActive(isBedRest);

        if (isBedRest && activityAnimator != null && !string.IsNullOrEmpty(bedIdleAnimation))
            activityAnimator.Play(bedIdleAnimation);
    }

    // ── Recovery activity (slot-driven) ─────────────────────────────────────────

    private void BeginRecovery()
    {
        if (currentSlotIndex >= slots.Count)
        {
            Debug.Log("[BedRest] All bed rest activities are complete.");
            return;
        }

        BedActivitySlot slot = slots[currentSlotIndex];

        if (slot.activity == null)
        {
            Debug.LogWarning($"[BedRest] Slot {currentSlotIndex} ('{slot.label}') has no ActivityDefinition assigned.");
            return;
        }

        if (!CalendarManager.instance.TryConsumeActivity(slot.activity))
        {
            Debug.Log($"{slot.activity.displayName} already done today.");
            return;
        }

        if (!StoryEnvironmentController.instance.CanDoActivity())
        {
            Debug.Log("Too tired to do more today — rest and try again tomorrow.");
            return;
        }

        player.interacting = true;

        string poseAnim = !string.IsNullOrEmpty(slot.activityAnimationState) ? slot.activityAnimationState : recoveryAnimation;
        if (activityAnimator != null && !string.IsNullOrEmpty(poseAnim))
            activityAnimator.Play(poseAnim);

        SceneCameraManager.instance.ActivateCamera(activityCamera);
        meterActive = true;

        StartCoroutine(RunActivity(slot));
    }

    private IEnumerator RunActivity(BedActivitySlot slot)
    {
        // Tutorial popup pauses here, BEFORE the minigame actually starts — otherwise
        // its timer would already be ticking away behind the popup.
        if (slot.onboardingUI != null)
        {
            slot.onboardingUI.SetActive(true);
            yield return new WaitUntil(() => Input.GetButtonDown("Submit"));
            slot.onboardingUI.SetActive(false);
        }

        if (healthMotivationMeterUI != null) healthMotivationMeterUI.SetActive(slot.showHealthMotivationMeter);
        if (slot.activityUI != null) slot.activityUI.SetActive(true);

        bool won = false, lost = false, injured = false;

        if (slot.activityType == BedActivityType.TypeA)
        {
            FillBarMinigame mg = slot.typeAMinigame;
            mg.gameObject.SetActive(true);

            System.Action<float> onFill = null;
            if (!string.IsNullOrEmpty(slot.fillProgressParam) && activityAnimator != null)
            {
                onFill = percent => activityAnimator.SetFloat(slot.fillProgressParam, percent / 100f);
                mg.OnFillChanged += onFill;
            }

            mg.ResetMinigame();
            while (!mg.finished) yield return null;
            won = mg.won; lost = mg.lost; injured = mg.injured;

            if (onFill != null) mg.OnFillChanged -= onFill;
            mg.gameObject.SetActive(false);

            string outcomeAnim = won ? slot.typeAWinAnimation : injured ? slot.typeAInjuredAnimation : slot.typeALoseAnimation;
            yield return PlayOutcomeAnimation(outcomeAnim);
        }
        else if (slot.activityType == BedActivityType.TypeB)
        {
            TrackerRhythmMinigame mg = slot.typeBMinigame;
            mg.gameObject.SetActive(true);

            System.Action onMiss = () => PlayAnimStateIfSet(slot.typeBMissAnimation);
            System.Action onInjury = () => PlayAnimStateIfSet(slot.typeBInjuredAnimation);
            mg.OnMissedHit += onMiss;
            mg.OnInjuryTriggered += onInjury;

            mg.ResetMinigame();
            while (!mg.finished) yield return null;
            won = mg.won; lost = mg.lost; injured = mg.injured;

            mg.OnMissedHit -= onMiss;
            mg.OnInjuryTriggered -= onInjury;
            mg.gameObject.SetActive(false);
        }
        else // TypeC — RhythmSequenceMinigame already drives its own animator hooks internally
        {
            RhythmSequenceMinigame mg = slot.typeCMinigame;
            mg.gameObject.SetActive(true);
            mg.ResetMinigame();

            while (!mg.finished) yield return null;
            won = mg.won; lost = mg.lost; injured = mg.injured;
            mg.gameObject.SetActive(false);
        }

        if (slot.activityUI != null) slot.activityUI.SetActive(false);

        yield return EndRecovery(slot, won, lost, injured);
    }

    /// <summary>
    /// Plays the given outcome animation and waits for its actual clip length before
    /// returning — mirrors RecoveryInteraction's approach. Waits one frame after Play()
    /// before reading the state's length, since the Animator doesn't evaluate the new
    /// state until its next update pass.
    /// </summary>
    private IEnumerator PlayOutcomeAnimation(string stateName)
    {
        if (activityAnimator == null || string.IsNullOrEmpty(stateName))
        {
            yield return new WaitForSeconds(0.01f);
            yield break;
        }

        activityAnimator.Play(stateName);
        yield return null;

        float length = activityAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(length);
    }

    private void PlayAnimStateIfSet(string stateName)
    {
        if (activityAnimator != null && !string.IsNullOrEmpty(stateName))
            activityAnimator.Play(stateName);
    }

    private IEnumerator EndRecovery(BedActivitySlot slot, bool won, bool lost, bool injured)
    {
        if (won)
        {
            Debug.Log("Won");
            HealthAndMotivation.instance.ModifyHealth(slot.winHealth);
            HealthAndMotivation.instance.ModifyMotivation(slot.winMotivation);
        }
        else if (lost)
        {
            Debug.Log("Lost");
            HealthAndMotivation.instance.ModifyHealth(slot.loseHealth);
            HealthAndMotivation.instance.ModifyMotivation(slot.loseMotivation);
        }
        else if (injured)
        {
            Debug.Log("Injured");
            HealthAndMotivation.instance.ModifyHealth(slot.injuryHealth);
            HealthAndMotivation.instance.ModifyMotivation(slot.injuryMotivation);
        }

        if (slot.onboardingUI != null) slot.onboardingUI.SetActive(false);

        yield return new WaitForSeconds(0.01f);
        SceneCameraManager.instance.DeactivateCamera(activityCamera);
        CalendarManager.instance.AdvanceTime(slot.activity.quadrantTicks);

        player.interacting = false;
        meterActive = false;
        currentSlotIndex++;

        ApplyPhaseVisual(StoryManager.instance.CurrentPhase);
    }

    // ── BedRest-specific sleep sequence ─────────────────────────────────────────
    // Unlike the generic BedInteraction's sleep, this keeps activityModel active
    // throughout rather than reverting to playerMeshChild — the phase-locked
    // model swap owns visibility here, not this coroutine.

    public IEnumerator SleepSequence()
    {
        isSleeping = true;
        player.interacting = true;

        if (activityAnimator != null && !string.IsNullOrEmpty(fallAsleepAnimation))
            activityAnimator.Play(fallAsleepAnimation);

        SceneCameraManager.instance.ActivateCamera(activityCamera);

        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(Fade(0f, 1f));

        CalendarManager.instance.Sleep();

        yield return new WaitForSeconds(0.5f);

        bool stillBedRest = IsBedRestPhase;

        if (stillBedRest && activityAnimator != null && !string.IsNullOrEmpty(wakeUpAnimation))
            activityAnimator.Play(wakeUpAnimation);

        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(Fade(1f, 0f));
        yield return new WaitForSeconds(0.5f);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);

        player.interacting = false;
        isSleeping = false;

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