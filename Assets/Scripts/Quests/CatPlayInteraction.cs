using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// Recovery interaction: playing with the cat. Motivation only - no health reward, no loss, no injury.
///
/// Same pattern as RecoveryInteraction: this script does NOT listen for the interact press itself. PlayerMovement
/// detects the trigger (tag it "Interact") and sets player.interacting = true; this script polls that while the
/// player is inside its own trigger (the player carries the "Player" tag). So the cat object needs ONE trigger
/// collider tagged "Interact".
///
/// The session is a press-at-the-right-time game. A series of prompts appears: after a random gap, the button icon
/// (Prompt Icon) shows for a short window. Pressing the play button (Space / B / Cancel) while it's showing is a
/// HIT - the cat's reaction plays and the icon goes away. Letting the window run out is a MISS (no penalty beyond a
/// lower reward). Pressing when there's no prompt is a mistimed press: it briefly locks the button out, so mashing
/// doesn't work. The share of prompts hit decides the motivation reward between Min and Max.
///
/// Time cost and once-per-day come from the ActivityDefinition, same as every other activity.
/// </summary>
public class CatPlayInteraction : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("The child GameObject holding only the player's visible mesh/rig. Hidden during the activity.")]
    [SerializeField] private GameObject playerMeshChild;

    [Header("Activity Model")]
    [Tooltip("Dedicated rig for this activity (player + cat playing). Disabled by default in the scene.")]
    [SerializeField] private GameObject activityModel;
    [SerializeField] private Animator activityAnimator;
    [Tooltip("Animator state played when the session starts. Blank = don't switch state.")]
    [SerializeField] private string startAnimation;
    [Tooltip("Animator state played when the session ends; the activity finishes once it has played. Blank = none.")]
    [SerializeField] private string endAnimation;
    [Tooltip("Animator TRIGGER fired on every successful press (needs a transition from Any State or your " +
             "loop state to the reaction clip). Blank = not used.")]
    [SerializeField] private string pressTrigger;
    [Tooltip("Animator state restarted from the beginning on every successful press (it plays over " +
             "whatever is playing - give it an exit transition back to your loop). Blank = not used. " +
             "Use this OR the trigger above.")]
    [SerializeField] private string pressAnimation;
    [Tooltip("Animator TRIGGER fired when a prompt is missed (the window runs out). Blank = not used.")]
    [SerializeField] private string missTrigger;

    [Header("Hit Feedback")]
    [Tooltip("Particles played on every successful press (e.g. hearts or sparkles by the cat). Restarted each hit. " +
             "Blank = none.")]
    [SerializeField] private ParticleSystem hitParticles;
    [Tooltip("Sound id (from the SoundLibrary) played on every successful press through the AudioManager, like " +
             "every other sound in the game. Blank = none.")]
    [SerializeField] private string hitSoundId;
    [Range(0f, 1f)]
    [Tooltip("Volume scale applied on top of the sound definition's own volume.")]
    [SerializeField] private float hitSoundVolume = 1f;
    [Tooltip("Sound id of the meow, played at random on some hits (on top of the hit sound). Blank = no meows. " +
             "If the SoundDefinition has several variations, it picks between them as usual.")]
    [SerializeField] private string meowSoundId;
    [Range(0f, 1f)]
    [Tooltip("Chance (0-1) that any given hit also gets a meow. 0.3 = roughly one hit in three.")]
    [SerializeField] private float meowChance = 0.3f;

    [Header("Patrol")]
    [Tooltip("The cat's NPCPatrol (normally on the same object as this script). Paused while the player plays with " +
             "the cat, resumed afterwards.")]
    [SerializeField] private NPCPatrol catPatrol;
    [Tooltip("Optional: hide this while the session runs (the world cat's visual, if the activity model has its own " +
             "cat). Must be a CHILD of this object - this script's own object can't be switched off or its session stops.")]
    [SerializeField] private GameObject hideWhileActive;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Calendar")]
    [Tooltip("Time cost and once-per-day limit, same as every other activity.")]
    [SerializeField] private ActivityDefinition activity;

    [Header("Prompt")]
    [Tooltip("Optional text (child of the prompt) that shows what this activity gives and costs. Filled from this activity's own values.")]
    [SerializeField] private TMP_Text stakesText;

    [Tooltip("A prompt (e.g. \"E / A: Play\") shown while the player is in range and the cat can be played with. " +
             "Hidden during the session and once the once-a-day play has been done. Keep it INACTIVE in the scene. Optional.")]
    [SerializeField] private GameObject interactPrompt;

    [Header("Timing Game")]
    [Tooltip("The button icon shown while a press is needed. Hidden the rest of the time. Disabled in the scene.")]
    [SerializeField] private GameObject promptIcon;
    [Tooltip("Seconds the icon takes to pop in (it overshoots slightly, then settles).")]
    [SerializeField] private float popInTime = 0.12f;
    [Tooltip("The icon slowly shrinks over the hit window down to this fraction of its size, so it shows how much " +
             "time is left. 1 = no shrinking.")]
    [SerializeField] private float shrinkToScale = 0.5f;
    [Tooltip("Seconds the icon takes to pop out (shrink to nothing) after a hit or a miss.")]
    [SerializeField] private float popOutTime = 0.1f;
    [Tooltip("How many prompts the session has.")]
    [SerializeField] private int promptCount = 8;
    [Tooltip("Shortest wait (seconds) before the next prompt appears.")]
    [SerializeField] private float minGap = 0.8f;
    [Tooltip("Longest wait (seconds) before the next prompt appears.")]
    [SerializeField] private float maxGap = 1.8f;
    [Tooltip("How long (seconds) the icon stays up. Longer = easier.")]
    [SerializeField] private float hitWindow = 0.8f;
    [Tooltip("A press with no prompt showing locks the button for this many seconds, so mashing doesn't work.")]
    [SerializeField] private float earlyPressLockout = 0.35f;

    [Header("Reward (motivation only)")]
    [Tooltip("Reward when no prompt is hit.")]
    [SerializeField] private float minMotivation = 5f;
    [Tooltip("Reward when every prompt is hit.")]
    [SerializeField] private float maxMotivation = 15f;

    [Header("First-Time Tutorial")]
    [Tooltip("Shown the first time this is ever used, before the session starts. Dismissed with Space / B. Blank = none.")]
    [SerializeField] private GameObject onboardingUI;
    [SerializeField] private string onboardingFlagId = "CatPlayOnboardingSeen";

    /// <summary>Fired once a session has fully finished (reward given, time advanced, player restored).</summary>
    public event Action OnSessionFinished;

    private bool canInteract;
    private bool sessionActive;
    /// <summary>True while the cat play session is running (the HUD-hiding script reads this).</summary>
    public static bool SessionInProgress { get; private set; }
    private Vector3 promptIconBaseScale = Vector3.one;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        canInteract = true;
        player = other.GetComponent<PlayerMovement>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        canInteract = false;
    }

    private void Start()
    {
        ActivityStakes.FillMotivation(stakesText, minMotivation, maxMotivation, activity != null ? activity.quadrantTicks : 0);
        if (activityModel != null) activityModel.SetActive(false);
        if (promptIcon != null)
        {
            promptIconBaseScale = promptIcon.transform.localScale;
            promptIcon.SetActive(false);
        }
        if (onboardingUI != null) onboardingUI.SetActive(false);
    }

    private void OnDisable()
    {
        if (sessionActive) SessionInProgress = false;
        SetPromptShown(false);
    }

    private bool promptMine;

    // The prompt object / text may be shared with other interactions, so only this script's own show is undone by it.
    private void SetPromptShown(bool on)
    {
        if (on)
        {
            if (!promptMine) { promptMine = true; ActivityStakes.FillMotivation(stakesText, minMotivation, maxMotivation, activity != null ? activity.quadrantTicks : 0); }
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

    private void Update()
    {
        bool available = !sessionActive && !ActiveDialogueTracker.AnyActive &&
                         (activity == null || !activity.onceLimitPerDay || CalendarManager.instance == null ||
                          !CalendarManager.instance.HasDoneToday(activity.activityId));
        SetPromptShown(canInteract && available);

        if (canInteract && player != null && player.interacting && !sessionActive)
            BeginInteraction();
    }

    private void BeginInteraction()
    {
        if (activity != null && CalendarManager.instance != null && !CalendarManager.instance.TryConsumeActivity(activity))
        {
            player.interacting = false;
            return;
        }

        sessionActive = true;
        SessionInProgress = true;
        if (catPatrol != null) catPatrol.SetPaused(true);
        StartCoroutine(RunSession());
    }

    private static bool PlayPressed()
    {
        return Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.JoystickButton1) ||
               Input.GetButtonDown("Cancel");
    }

    /// <summary>Plays the hit reaction - particles, sound and animation - on every hit, restarting what's still going.</summary>
    private void PlayHitReaction()
    {
        if (hitParticles != null)
        {
            hitParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hitParticles.Play(true);
        }

        Sfx.Play(hitSoundId, hitSoundVolume);

        if (!string.IsNullOrEmpty(meowSoundId) && Random.value < meowChance)
            Sfx.Play(meowSoundId, hitSoundVolume);

        if (activityAnimator == null) return;

        if (!string.IsNullOrEmpty(pressTrigger))
        {
            activityAnimator.ResetTrigger(pressTrigger);
            activityAnimator.SetTrigger(pressTrigger);
        }

        if (!string.IsNullOrEmpty(pressAnimation))
            activityAnimator.Play(pressAnimation, 0, 0f);
    }

    private void PlayMissReaction()
    {
        if (activityAnimator == null || string.IsNullOrEmpty(missTrigger)) return;

        activityAnimator.ResetTrigger(missTrigger);
        activityAnimator.SetTrigger(missTrigger);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    /// <summary>Shrinks the prompt icon from its current size to nothing, then hides it.</summary>
    private IEnumerator PopOutPrompt(float fromScale)
    {
        if (promptIcon == null) yield break;

        float t = 0f;
        float duration = Mathf.Max(0.01f, popOutTime);
        while (t < duration)
        {
            t += Time.deltaTime;
            promptIcon.transform.localScale = promptIconBaseScale * (fromScale * (1f - Mathf.Clamp01(t / duration)));
            yield return null;
        }

        promptIcon.SetActive(false);
        promptIcon.transform.localScale = promptIconBaseScale;
    }

    private IEnumerator RunSession()
    {
        if (onboardingUI != null &&
            !(PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(onboardingFlagId)))
        {
            onboardingUI.SetActive(true);
            yield return null;
            yield return new WaitUntil(PlayPressed);
            onboardingUI.SetActive(false);
            if (PersistentGameFlags.instance != null)
                PersistentGameFlags.instance.SetFlag(onboardingFlagId);
            yield return null; // the dismiss press must not count as a play press
        }

        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (hideWhileActive != null) hideWhileActive.SetActive(false);
        if (activityModel != null)
        {
            activityModel.SetActive(true);
            if (activityAnimator != null && !string.IsNullOrEmpty(startAnimation))
                activityAnimator.Play(startAnimation);
        }

        if (SceneCameraManager.instance != null) SceneCameraManager.instance.ActivateCamera(activityCamera);

        int prompts = Mathf.Max(1, promptCount);
        int hits = 0;
        float lockedUntil = 0f;

        for (int i = 0; i < prompts; i++)
        {
            // Wait for the next prompt. A press here is mistimed and briefly locks the button.
            float gap = Random.Range(Mathf.Min(minGap, maxGap), Mathf.Max(minGap, maxGap));
            float waited = 0f;
            while (waited < gap)
            {
                waited += Time.deltaTime;
                if (PlayPressed()) lockedUntil = Time.time + earlyPressLockout;
                yield return null;
            }

            // The prompt: show the icon until it's hit or the window runs out.
            if (promptIcon != null)
            {
                promptIcon.transform.localScale = Vector3.zero;
                promptIcon.SetActive(true);
            }

            bool hit = false;
            float shown = 0f;
            float iconScale = 0f;
            while (shown < hitWindow && !hit)
            {
                shown += Time.deltaTime;

                // Pops in with a little overshoot, then slowly shrinks across the window.
                float pop = EaseOutBack(Mathf.Clamp01(shown / Mathf.Max(0.01f, popInTime)));
                float shrink = Mathf.Lerp(1f, shrinkToScale, Mathf.Clamp01(shown / Mathf.Max(0.01f, hitWindow)));
                iconScale = pop * shrink;
                if (promptIcon != null) promptIcon.transform.localScale = promptIconBaseScale * iconScale;

                if (PlayPressed() && Time.time >= lockedUntil)
                {
                    hit = true;
                    hits++;
                    PlayHitReaction();
                }
                yield return null;
            }

            if (!hit) PlayMissReaction();
            yield return PopOutPrompt(iconScale);
        }

        float reward = Mathf.Lerp(minMotivation, maxMotivation, hits / (float)prompts);
        if (HealthAndMotivation.instance != null)
            HealthAndMotivation.instance.ModifyMotivation(reward);

        yield return PlayEndAnimation();

        if (activityModel != null) activityModel.SetActive(false);
        if (hideWhileActive != null) hideWhileActive.SetActive(true);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);
        if (SceneCameraManager.instance != null) SceneCameraManager.instance.DeactivateCamera(activityCamera);

        if (activity != null && CalendarManager.instance != null)
            CalendarManager.instance.AdvanceTime(activity.quadrantTicks);

        player.interacting = false;
        sessionActive = false;
        SessionInProgress = false;
        if (catPatrol != null) catPatrol.SetPaused(false);

        OnSessionFinished?.Invoke();
    }

    /// <summary>Plays the end animation and waits for its real length. Waits one frame first so the Animator has
    /// actually entered the new state before its length is read.</summary>
    private IEnumerator PlayEndAnimation()
    {
        if (activityAnimator == null || string.IsNullOrEmpty(endAnimation))
        {
            yield return new WaitForSeconds(0.01f);
            yield break;
        }

        activityAnimator.Play(endAnimation);
        yield return null;
        yield return new WaitForSeconds(activityAnimator.GetCurrentAnimatorStateInfo(0).length);
    }
}