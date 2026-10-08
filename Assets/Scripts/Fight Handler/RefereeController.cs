using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The referee. Runs the pre-fight ritual (bow to the referee -> bow to each other -> "begin"
/// -> fight and music start), then stays on to call the match: announces every point
/// ("Point - Player!"), says "Hajime!" when play resumes after a point, and "Time!" at the end.
/// Scoreboard numbers are handled separately by ScoreboardUI.
///
/// How it holds the fight back: MatchTimer.matchStopped is what PlayerStateMachine,
/// EnemyStateMachine and PlayerController all already check before doing anything, so while
/// the timer hasn't started nobody can attack. Turn OFF "Auto Start On Scene Load" on the
/// MatchTimer so it doesn't start itself - this script calls matchTimer.StartTimer() at the
/// end instead (which also fires OnMatchStart as normal).
///
/// Everything is a list of Steps, edited in the Inspector, so adding/changing text later is
/// just typing into "Display Text" - no code. Per step you can set:
///   - Display Text: shown in the Text element (leave empty to show nothing)
///   - Wait For Player Input: step ends when the player presses confirm (the bow itself);
///     otherwise it just lasts Duration seconds
///   - Animator triggers for player / opponent / referee (leave blank for none; a missing
///     trigger on an Animator is skipped quietly, so you can add bow animations later)
///   - Sfx Id: optional AudioManager SFX (e.g. a clap or "hajime" shout), inert if not set up
///   - On Step Start: UnityEvent for anything else (camera move, voice line...)
///
/// Setup:
///   1. Empty GameObject in the fight scene, add this component.
///   2. Assign Match Timer, and a TMP_Text (Canvas) for Display Text.
///   3. Optional: assign Player/Opponent/Referee Animators and set trigger names.
///   4. MatchTimer -> untick "Auto Start On Scene Load".
///   5. AudioManager -> clear "Auto Play Music Id" and put the id in "Music Id On Begin" here
///      so music starts at "begin", not at scene load.
/// </summary>
public class RefereeController : MonoBehaviour
{
    public enum FaceTarget { None, Referee, EachOther }

    [System.Serializable]
    public class Step
    {
        public string name = "Step";
        [TextArea] public string displayText = "";
        [Tooltip("ON: step ends when the player presses confirm (Enter / gamepad A) - that press IS the bow.")]
        public bool waitForPlayerInput;
        [Tooltip("Seconds the step lasts (when not waiting for input), or the minimum before input is accepted.")]
        public float duration = 1f;
        [Tooltip("Font size of Display Text for this step. 0 = use the text element's own size.")]
        public float fontSize = 0f;
        [Tooltip("Show the Prompt Image while waiting for the player's input. Only used when " +
                 "Wait For Player Input is on.")]
        public bool showInputPrompt = true;
        [Tooltip("Start the fight music (Music Id below) the moment this step begins, fading in " +
                 "over Music Fade In. Tick it on the Hajime step.")]
        public bool startMusicOnStepStart;
        [Tooltip("Pause after the step finishes, before the next one starts.")]
        public float postDelay = 0.3f;
        [Tooltip("Turns the fighters during this step. Referee = both face the referee, " +
                 "EachOther = they face one another, None = leave them as they are.")]
        public FaceTarget faceTarget = FaceTarget.None;
        [Header("Animator triggers (optional)")]
        public string playerTrigger;
        public string opponentTrigger;
        public string refereeTrigger;
        [Header("Audio / events (optional)")]
        public string sfxId;
        public UnityEvent onStepStart;
    }

    [Header("References")]
    [SerializeField] private MatchTimer matchTimer;
    [Tooltip("On-screen text for each step's Display Text. Optional - nothing shows without it.")]
    [SerializeField] private TMP_Text displayText;
    [Tooltip("An Image (or any UI object, e.g. a parent holding keyboard + gamepad button icons) " +
             "shown while the player needs to press to bow, then hidden. Optional.")]
    [SerializeField] private GameObject promptImage;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Animator opponentAnimator;
    [SerializeField] private Animator refereeAnimator;
    [Header("Facing (root transforms that get turned)")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform opponentTransform;
    [SerializeField] private Transform refereeTransform;
    [Tooltip("Seconds the fighters take to turn during a Face step.")]
    [SerializeField] private float turnDuration = 0.6f;

    [Header("Sequence")]
    [SerializeField] private float startDelay = 0.75f;
    [SerializeField]
    private List<Step> steps = new List<Step>
    {
        new Step { name = "Face referee", displayText = "Face me", duration = 1.5f, fontSize = 40f,
                   faceTarget = FaceTarget.Referee, postDelay = 0.2f },
        new Step { name = "Bow to referee", displayText = "Bow", waitForPlayerInput = true,
                   duration = 0.3f, fontSize = 80f,
                   playerTrigger = "Bow", opponentTrigger = "Bow" },
        new Step { name = "Face each other", displayText = "Face each other", duration = 1.5f, fontSize = 40f,
                   faceTarget = FaceTarget.EachOther, postDelay = 0.2f },
        new Step { name = "Bow to opponent", displayText = "Bow", waitForPlayerInput = true,
                   duration = 0.3f, fontSize = 80f,
                   playerTrigger = "Bow", opponentTrigger = "Bow" },
        new Step { name = "Begin", displayText = "Hajime!", waitForPlayerInput = false,
                   duration = 1.2f, fontSize = 80f, postDelay = 0f, refereeTrigger = "Begin",
                   startMusicOnStepStart = true },
    };

    [Header("Calling points (after the fight starts)")]
    [SerializeField] private MatchScore matchScore;
    [Tooltip("{points} is replaced with how many points were just awarded.")]
    [SerializeField] private string playerPointText = "Point! Player (+{points})";
    [SerializeField] private string enemyPointText = "Point! Opponent (+{points})";
    [Tooltip("Shown when play resumes after a point. Empty = say nothing.")]
    [SerializeField] private string resumeText = "Hajime!";
    [Tooltip("Shown when time runs out. Empty = say nothing.")]
    [SerializeField] private string timeUpText = "Time!";
    [SerializeField] private float pointTextDuration = 2.5f;
    [SerializeField] private float resumeTextDuration = 1f;
    [Header("After a point: let the hit play out, call Yame, then reset")]
    [Tooltip("Seconds after a point lands before the referee calls Yame - lets the attack and " +
             "hurt animations finish playing.")]
    [SerializeField] private float hitAnimDelay = 1.2f;
    [SerializeField] private string yameText = "Yame!";
    [Tooltip("How long Yame is on screen before the reset fires. Keep (Hit Anim Delay + this + " +
             "the reset time) shorter than MatchTimer's Hit Pause Duration.")]
    [SerializeField] private float yameDuration = 1.2f;
    [SerializeField] private string refereeYameTrigger = "Yame";
    [SerializeField] private string yameSfxId;
    [Tooltip("Fires once Yame has been called - bind GameManager.ResetPos here (and REMOVE it " +
             "from MatchTimer's On Match Stop, or the reset will still happen instantly).")]
    public UnityEvent onReadyToReset;

    [Header("Point call")]
    [Tooltip("Referee Animator triggers (optional, skipped if missing).")]
    [SerializeField] private string refereePlayerPointTrigger = "PointPlayer";
    [SerializeField] private string refereeEnemyPointTrigger = "PointEnemy";
    [SerializeField] private string refereeResumeTrigger = "Begin";
    [Tooltip("Optional AudioManager SFX ids (inert until sounds exist).")]
    [SerializeField] private string pointSfxId;
    [SerializeField] private string resumeSfxId;
    public UnityEvent onPlayerPoint;
    public UnityEvent onEnemyPoint;

    [Header("When it ends")]
    [Tooltip("Fight music (AudioManager SoundLibrary id). Starts when the step with 'Start Music " +
             "On Step Start' ticked (Hajime) begins. If no step has it ticked, or Skip Intro is on, " +
             "it starts when the fight begins. Empty = no music.")]
    [SerializeField] private string musicId;
    [SerializeField] private float musicFadeIn = 3f;
    [Tooltip("Fired right as the fight starts, after the timer is started.")]
    public UnityEvent onFightBegin;

    [Header("Testing")]
    [Tooltip("Skip the whole ritual and start the fight immediately.")]
    [SerializeField] private bool skipIntro;

    public bool IsRunning { get; private set; }

    private int lastPlayerScore, lastEnemyScore;
    private bool fightStarted;
    private Coroutine callRoutine;
    private Coroutine pointSequenceRoutine;
    private float baseFontSize;
    private bool musicStarted;

    private void OnEnable()
    {
        if (matchScore != null)
        {
            lastPlayerScore = matchScore.PlayerScore;
            lastEnemyScore = matchScore.EnemyScore;
            matchScore.OnScoreChanged.AddListener(OnScoreChanged);
        }
        if (matchTimer != null)
        {
            matchTimer.OnMatchStart.AddListener(OnMatchResumed);
            matchTimer.OnTimeUp.AddListener(OnTimeUp);
        }
    }

    private void OnDisable()
    {
        if (matchScore != null) matchScore.OnScoreChanged.RemoveListener(OnScoreChanged);
        if (matchTimer != null)
        {
            matchTimer.OnMatchStart.RemoveListener(OnMatchResumed);
            matchTimer.OnTimeUp.RemoveListener(OnTimeUp);
        }
    }

    private void OnScoreChanged(int playerScore, int enemyScore)
    {
        int playerGain = playerScore - lastPlayerScore;
        int enemyGain = enemyScore - lastEnemyScore;
        lastPlayerScore = playerScore;
        lastEnemyScore = enemyScore;

        if (!fightStarted) return; // ignore score resets etc. during the ritual

        if (playerGain > 0)
        {
            CallPoint(playerPointText, playerGain, refereePlayerPointTrigger);
            onPlayerPoint?.Invoke();
        }
        else if (enemyGain > 0)
        {
            CallPoint(enemyPointText, enemyGain, refereeEnemyPointTrigger);
            onEnemyPoint?.Invoke();
        }
    }

    private void CallPoint(string template, int points, string trigger)
    {
        Debug.Log($"[Referee] Point awarded ({points}).");
        if (pointSequenceRoutine != null) StopCoroutine(pointSequenceRoutine);
        pointSequenceRoutine = StartCoroutine(PostPointSequence(
            (template ?? "").Replace("{points}", points.ToString()), trigger));
    }

    // hit anims play -> "Yame!" -> reset fires -> "Point!" call (while fighters walk back).
    private IEnumerator PostPointSequence(string pointText, string pointTrigger)
    {
        yield return new WaitForSecondsRealtime(hitAnimDelay);

        Trigger(refereeAnimator, refereeYameTrigger);
        if (!string.IsNullOrEmpty(yameSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(yameSfxId);
        ShowTimed(yameText, yameDuration);
        yield return new WaitForSecondsRealtime(yameDuration);

        onReadyToReset?.Invoke();

        Trigger(refereeAnimator, pointTrigger);
        if (!string.IsNullOrEmpty(pointSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(pointSfxId);
        ShowTimed(pointText, pointTextDuration);
        pointSequenceRoutine = null;
    }

    // Fires on every StartTimer(): once at "begin" (fightStarted still false then - the
    // sequence handles that itself) and after every post-point pause (a "Hajime!" resume).
    private void OnMatchResumed()
    {
        if (!fightStarted) return;
        // Play resumed - drop any part of the post-point sequence that hasn't happened yet.
        if (pointSequenceRoutine != null)
        {
            StopCoroutine(pointSequenceRoutine);
            pointSequenceRoutine = null;
        }
        Trigger(refereeAnimator, refereeResumeTrigger);
        if (!string.IsNullOrEmpty(resumeSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(resumeSfxId);
        ShowTimed(resumeText, resumeTextDuration);
    }

    private void OnTimeUp() => ShowTimed(timeUpText, 3f);

    private void ShowTimed(string text, float seconds)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (callRoutine != null) StopCoroutine(callRoutine);
        callRoutine = StartCoroutine(ShowTimedRoutine(text, seconds));
    }

    private IEnumerator ShowTimedRoutine(string text, float seconds)
    {
        if (displayText != null && baseFontSize > 0f) displayText.fontSize = baseFontSize;
        SetText(text);
        yield return new WaitForSecondsRealtime(seconds);
        SetText("");
        callRoutine = null;
    }

    private void Start()
    {
        if (matchTimer == null)
        {
            Debug.LogWarning("[Referee] No Match Timer assigned - can't start the fight.");
            return;
        }

        // Make sure nobody can fight until begin, even if Auto Start wasn't unticked.
        matchTimer.matchStopped = true;
        if (displayText != null) baseFontSize = displayText.fontSize;
        if (promptImage != null &&
            (promptImage.GetComponent<Canvas>() != null ||
             promptImage.GetComponentInChildren<ScoreboardUI>(true) != null ||
             (displayText != null && displayText.transform.IsChildOf(promptImage.transform))))
        {
            Debug.LogError("[Referee] 'Prompt Image' is set to '" + promptImage.name + "', which is a " +
                           "Canvas / contains the scoreboard or the referee text. It gets switched off " +
                           "after every bow, which would hide all of that. Ignoring it - assign ONLY " +
                           "the bow button icon object instead.", promptImage);
            promptImage = null;
        }
        SetPrompt(false);
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        IsRunning = true;
        SetText("");

        if (!skipIntro)
        {
            if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

            foreach (Step step in steps)
                yield return RunStep(step);
        }

        SetText("");
        IsRunning = false;

        Debug.Log("[Referee] Begin - starting match.");
        matchTimer.StartTimer();
        fightStarted = true; // after StartTimer, so its OnMatchStart isn't treated as a resume

        StartMusicOnce();

        onFightBegin?.Invoke();
    }

    private IEnumerator RunStep(Step step)
    {
        Debug.Log($"[Referee] Step: {step.name}");
        if (displayText != null)
            displayText.fontSize = step.fontSize > 0f ? step.fontSize : baseFontSize;
        SetText(step.displayText);

        if (step.startMusicOnStepStart) StartMusicOnce();

        if (step.faceTarget != FaceTarget.None)
        {
            Transform playerLook = step.faceTarget == FaceTarget.Referee ? refereeTransform : opponentTransform;
            Transform opponentLook = step.faceTarget == FaceTarget.Referee ? refereeTransform : playerTransform;
            StartCoroutine(TurnToward(playerTransform, playerLook));
            StartCoroutine(TurnToward(opponentTransform, opponentLook));
        }

        Trigger(playerAnimator, step.playerTrigger);
        Trigger(opponentAnimator, step.opponentTrigger);
        Trigger(refereeAnimator, step.refereeTrigger);

        if (!string.IsNullOrEmpty(step.sfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(step.sfxId);

        step.onStepStart?.Invoke();

        float t = 0f;
        while (t < step.duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (step.waitForPlayerInput)
        {
            // Skip this frame's input so a press that finished the previous step can't
            // also instantly satisfy this one.
            yield return null;
            SetPrompt(step.showInputPrompt);
            while (!SkipInput.Pressed()) yield return null;
            SetPrompt(false);
        }

        if (step.postDelay > 0f) yield return new WaitForSecondsRealtime(step.postDelay);
    }

    /// <summary>Smoothly turns `who` around Y only to face `target`.</summary>
    private IEnumerator TurnToward(Transform who, Transform target)
    {
        if (who == null || target == null) yield break;

        Vector3 flat = target.position - who.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) yield break;

        Quaternion from = who.rotation;
        Quaternion to = Quaternion.LookRotation(flat.normalized, Vector3.up);

        float t = 0f;
        while (t < turnDuration)
        {
            t += Time.unscaledDeltaTime;
            who.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / turnDuration)));
            yield return null;
        }
        who.rotation = to;
    }

    private void StartMusicOnce()
    {
        if (musicStarted || string.IsNullOrEmpty(musicId)) return;
        musicStarted = true;

        if (AudioManager.instance != null)
            AudioManager.instance.PlayMusic(musicId, musicFadeIn);
        else
            Debug.LogWarning("[Referee] Music Id set but no AudioManager in the scene.");
    }

    private void SetPrompt(bool visible)
    {
        if (promptImage != null) promptImage.SetActive(visible);
    }

    private void SetText(string text)
    {
        if (displayText == null) return;
        displayText.text = text;
        // Toggle only the text component, never its GameObject - if this object is (or sits on)
        // something shared like the scoreboard canvas, deactivating it would switch that off too.
        displayText.enabled = !string.IsNullOrEmpty(text);
    }

    private static void Trigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;

        // Skip quietly if the Animator Controller has no such trigger yet.
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
            {
                animator.SetTrigger(triggerName);
                return;
            }
        }
    }
}