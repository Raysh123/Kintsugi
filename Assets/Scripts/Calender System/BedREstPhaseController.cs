using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Everything specific to the BedRest story phase: the phase-locked model
/// swap (player's normal model hidden for the WHOLE phase, replaced by this
/// script's activityModel), the entry/exit transition poses, the 6-slot
/// recovery activity system, and BedRest's own sleep sequence â€” which keeps
/// activityModel active throughout rather than reverting to the normal mesh
/// after every night, unlike the generic BedInteraction.
///
/// This script is always resident and self-gates on the current story phase
/// internally â€” it's never meant to be enabled/disabled externally. Outside
/// BedRest, its Update() simply no-ops; BedInteraction (a separate, generic
/// script) handles ordinary sleep everywhere else in the game instead.
///
/// The recovery activity itself runs off an ordered list of slots
/// (BedActivitySlotDefinition) â€” Type A morning, Type B afternoon, Type C evening,
/// repeated across however many days BedRest lasts. Completing one slot
/// advances to the next automatically. Each slot has its own dedicated
/// minigame instance (via the parallel BedActivitySlotSceneRefs list), so
/// Day 1 and Day 2 versions of the same type can have independently tuned
/// base difficulty, not just different rewards.
/// </summary>
public class BedRestPhaseController : MonoBehaviour
{
    /// <summary>
    /// The scene-object half of a slot â€” kept separate from BedActivitySlotDefinition
    /// (an asset) because ScriptableObjects can't reference scene objects via the
    /// Inspector. Index N here corresponds to index N in the slots list below.
    /// </summary>
    [System.Serializable]
    public class BedActivitySlotSceneRefs
    {
        public FillBarMinigame typeAMinigame;
        public TrackerRhythmMinigame typeBMinigame;
        public RhythmSequenceMinigame typeCMinigame;
        [Tooltip("Shown while this slot's minigame runs â€” e.g. a first-time tutorial/onboarding hint.")]
        public GameObject onboardingUI;
        [Tooltip("This activity's own gameplay UI â€” e.g. its FillBarMeterUI/TrackerRhythmMeterUI/" +
                 "BoardPromptUI panel.")]
        public GameObject activityUI;

        [Header("Optional: before this activity")]
        [Tooltip("Plays before this slot's activity starts â€” e.g. an NPC reminding the player to do " +
                 "their physio. Gates E until dismissed, same as onboardingUI.")]
        public DialogueRunner beforeDialogue;
        [Tooltip("If assigned, this NPC appears at Before Npc Position for the dialogue above, then " +
                 "returns to its own default position once THIS specific slot's activity finishes â€” " +
                 "not tied to the whole BedRest phase, just this one activity.")]
        public NPCAppearance beforeNpc;
        public Transform beforeNpcPosition;

        [Header("Optional: after this activity (unprompted)")]
        [Tooltip("Plays automatically the instant this slot's activity finishes â€” win, lose, or " +
                 "injured, doesn't matter which. No button press needed to trigger it.")]
        public DialogueRunner afterDialogue;
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
             "activity or sleeping â€” the default state for the whole phase.")]
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
    [Tooltip("Runs in order â€” completing one automatically advances to the next. Each entry is a " +
             "BedActivitySlotDefinition asset (Assets > Create > RPG > Bed Activity Slot), not an " +
             "inline list entry, to avoid a Unity Inspector bug where editing one large serializable " +
             "list element can visibly corrupt another.")]
    [SerializeField] private List<BedActivitySlotDefinition> slots = new List<BedActivitySlotDefinition>();
    [Tooltip("Scene object references for each slot above, MATCHED BY INDEX â€” element 0 here goes " +
             "with element 0 in Slots, etc. Split out from the asset list because ScriptableObjects " +
             "can't hold scene object references.")]
    [SerializeField] private List<BedActivitySlotSceneRefs> sceneRefs = new List<BedActivitySlotSceneRefs>();
    private int currentSlotIndex;

    [Header("Shared UI")]
    [SerializeField] private GameObject healthMotivationMeterUI;

    [Header("Phase Start (unprompted, plays once automatically)")]
    [Tooltip("Plays the moment BedRest begins, before the player can press E for anything â€” " +
             "e.g. Mom's opening line. Not gated behind any button press.")]
    [SerializeField] private DialogueRunner phaseStartDialogue;
    [Tooltip("Optional â€” if assigned along with Phase Start Npc Position, this NPC appears there " +
             "right before the dialogue above plays.")]
    [SerializeField] private NPCAppearance phaseStartNpc;
    [SerializeField] private Transform phaseStartNpcPosition;
    private bool phaseStartDialogueComplete;

    [Header("Phase End Cutscene")]
    [Tooltip("Plays during the final sleep of BedRest, right after fade-to-black and before the " +
             "wake-up/fade-in â€” bridges into FriendVisit while the screen is still dark.")]
    [SerializeField] private ComicCutscene bedRestEndCutscene;

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
        foreach (BedActivitySlotSceneRefs refs in sceneRefs)
        {
            if (refs.onboardingUI != null) refs.onboardingUI.SetActive(false);
            if (refs.activityUI != null) refs.activityUI.SetActive(false);
            if (refs.typeAMinigame != null) refs.typeAMinigame.gameObject.SetActive(false);
            if (refs.typeBMinigame != null) refs.typeBMinigame.gameObject.SetActive(false);
            if (refs.typeCMinigame != null) refs.typeCMinigame.gameObject.SetActive(false);
        }

        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (StoryManager.instance == null) yield return null;

        if (StoryManager.instance.CurrentPhase != StoryPhase.BedRest)
        {
            // Started past BedRest already - nothing for this controller to ever do.
            // Deliberately NOT calling ApplyPhaseVisual here (an earlier version did) -
            // this fires in Start() on EVERY scene load once the player is past BedRest,
            // including a post-fight return, and ApplyPhaseVisual's
            // playerMeshChild.SetActive(!isBedRest) would force playerMeshChild back on
            // directly, undoing whatever PlayerAppearanceController had just set (e.g. the
            // Dojo model) completely independently of it.
            phaseStartDialogueComplete = true;
            yield break;
        }

        StoryManager.instance.OnPhaseChanged += HandlePhaseChanged;
        ApplyPhaseVisual(StoryManager.instance.CurrentPhase);

        yield return PlayPhaseStartDialogue();
    }

    private IEnumerator PlayPhaseStartDialogue()
    {
        if (phaseStartNpc != null && phaseStartNpcPosition != null)
            phaseStartNpc.AppearAt(phaseStartNpcPosition);

        if (phaseStartDialogue != null)
        {
            phaseStartDialogue.Show();
            yield return new WaitUntil(() => !phaseStartDialogue.IsRunning);
        }

        phaseStartDialogueComplete = true;
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

        if (!meterActive && !player.interacting && phaseStartDialogueComplete)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit"))
            {
                if (CalendarManager.instance != null && CalendarManager.instance.CurrentPhase == DayPhase.Night)
                {
                    Debug.Log("Too late for activities â€” time to sleep.");
                }
                else
                {
                    BeginRecovery();
                }
            }
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

    // â”€â”€ Phase-driven model swap â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void HandlePhaseChanged(StoryPhase old, StoryPhase newPhase)
    {
        ApplyPhaseVisual(newPhase);

        if (newPhase != StoryPhase.BedRest)
        {
            // BedRest has ended for good â€” this controller's whole job was that
            // phase specifically. Unsubscribing here stops it from reacting to (and
            // racing with) any later phase transition, which was fighting
            // BedInteraction over playerMeshChild's visibility during later sleeps.
            StoryManager.instance.OnPhaseChanged -= HandlePhaseChanged;
        }
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

    // â”€â”€ Recovery activity (slot-driven) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void BeginRecovery()
    {
        if (currentSlotIndex >= slots.Count)
        {
            Debug.Log("[BedRest] All bed rest activities are complete.");
            return;
        }

        BedActivitySlotDefinition slot = slots[currentSlotIndex];

        if (currentSlotIndex >= sceneRefs.Count)
        {
            Debug.LogError($"[BedRest] Slots and Scene Refs lists are out of sync â€” Slots has " +
                            $"{slots.Count} entries but Scene Refs only has {sceneRefs.Count}. They " +
                            $"must be kept the same length, matched by index.");
            return;
        }

        BedActivitySlotSceneRefs refs = sceneRefs[currentSlotIndex];

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

        player.interacting = true;

        string poseAnim = !string.IsNullOrEmpty(slot.activityAnimationState) ? slot.activityAnimationState : recoveryAnimation;
        if (activityAnimator != null && !string.IsNullOrEmpty(poseAnim))
            activityAnimator.Play(poseAnim);

        SceneCameraManager.instance.ActivateCamera(activityCamera);
        meterActive = true;

        StartCoroutine(RunActivity(slot, refs));
    }

    private IEnumerator RunActivity(BedActivitySlotDefinition slot, BedActivitySlotSceneRefs refs)
    {
        // NPC appears and delivers a line before this specific activity, if configured.
        if (refs.beforeNpc != null && refs.beforeNpcPosition != null)
            refs.beforeNpc.AppearAt(refs.beforeNpcPosition);

        if (refs.beforeDialogue != null)
        {
            refs.beforeDialogue.Show();
            yield return new WaitUntil(() => !refs.beforeDialogue.IsRunning);
        }

        // Tutorial popup pauses here, BEFORE the minigame actually starts â€” otherwise
        // its timer would already be ticking away behind the popup.
        if (refs.onboardingUI != null)
        {
            refs.onboardingUI.SetActive(true);
            yield return new WaitUntil(() =>
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.JoystickButton1) ||
                Input.GetButtonDown("Cancel"));
            refs.onboardingUI.SetActive(false);
        }

        if (healthMotivationMeterUI != null) healthMotivationMeterUI.SetActive(slot.showHealthMotivationMeter);
        if (refs.activityUI != null) refs.activityUI.SetActive(true);

        bool won = false, lost = false, injured = false;

        if (slot.activityType == BedActivityType.TypeA)
        {
            FillBarMinigame mg = refs.typeAMinigame;
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
            TrackerRhythmMinigame mg = refs.typeBMinigame;
            mg.gameObject.SetActive(true);

            System.Action onMiss = () => PlayAnimStateIfSet(slot.typeBMissAnimation);
            System.Action onInjury = () => PlayAnimStateIfSet(slot.typeBInjuredAnimation);
            mg.OnMissedHit += onMiss;
            mg.OnInjuryTriggered += onInjury;

            mg.ResetMinigame();
            while (!mg.finished)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
                    PlayAnimStateIfSet(slot.typeBActionAnimation);
                yield return null;
            }
            won = mg.won; lost = mg.lost; injured = mg.injured;

            mg.OnMissedHit -= onMiss;
            mg.OnInjuryTriggered -= onInjury;
            mg.gameObject.SetActive(false);
        }
        else // TypeC â€” RhythmSequenceMinigame already drives its own animator hooks internally
        {
            RhythmSequenceMinigame mg = refs.typeCMinigame;
            mg.gameObject.SetActive(true);
            mg.ResetMinigame();

            while (!mg.finished) yield return null;
            won = mg.won; lost = mg.lost; injured = mg.injured;
            mg.gameObject.SetActive(false);
        }

        if (refs.activityUI != null) refs.activityUI.SetActive(false);
        if (refs.beforeNpc != null) refs.beforeNpc.ReturnToDefault();

        yield return EndRecovery(slot, refs, won, lost, injured);
    }

    /// <summary>
    /// Plays the given outcome animation and waits for its actual clip length before
    /// returning â€” mirrors RecoveryInteraction's approach. Waits one frame after Play()
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

    private IEnumerator EndRecovery(BedActivitySlotDefinition slot, BedActivitySlotSceneRefs refs, bool won, bool lost, bool injured)
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

        if (refs.afterDialogue != null)
        {
            refs.afterDialogue.Show();
            yield return new WaitUntil(() => !refs.afterDialogue.IsRunning);
        }

        yield return new WaitForSeconds(0.01f);
        SceneCameraManager.instance.DeactivateCamera(activityCamera);
        CalendarManager.instance.AdvanceTime(slot.activity.quadrantTicks);

        player.interacting = false;
        meterActive = false;
        currentSlotIndex++;

        ApplyPhaseVisual(StoryManager.instance.CurrentPhase);
    }

    // â”€â”€ BedRest-specific sleep sequence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Unlike the generic BedInteraction's sleep, this keeps activityModel active
    // throughout rather than reverting to playerMeshChild â€” the phase-locked
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

        // If this specific sleep transitioned OUT of BedRest, play the bridging
        // cutscene now, while the screen is still black â€” before any wake-up
        // animation or fade-in, so the player sees it resolve directly into
        // FriendVisit rather than briefly seeing BedRest's world again first.
        if (!stillBedRest && bedRestEndCutscene != null)
        {
            bool cutsceneDone = false;
            void OnDone() => cutsceneDone = true;
            bedRestEndCutscene.OnCutsceneComplete += OnDone;

            bedRestEndCutscene.Play();
            yield return new WaitUntil(() => cutsceneDone);

            bedRestEndCutscene.OnCutsceneComplete -= OnDone;
        }

        if (activityAnimator != null && !string.IsNullOrEmpty(wakeUpAnimation))
            activityAnimator.Play(wakeUpAnimation);

        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(Fade(1f, 0f));
        yield return new WaitForSeconds(0.5f);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);

        player.interacting = false;
        isSleeping = false;

        ApplyPhaseVisual(StoryManager.instance.CurrentPhase);
        BedInteraction.RaiseWakeUp();
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