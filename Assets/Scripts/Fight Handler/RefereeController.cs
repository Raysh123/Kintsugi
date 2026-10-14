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
        [Header("Animator STATES (optional, played by name - no triggers needed)")]
        [Tooltip("State to play on the player's Animator (e.g. 'FaceMe', 'FaceEachOther', 'Bow'). Blank = none. " +
                 "Missing states are skipped quietly.")]
        public string playerState;
        public string opponentState;
        public string refereeState;
        [Tooltip("Steps that wait for the player's button (the bows) play their animations/triggers when the player " +
                 "PRESSES. Tick this to play them at the start of the step instead.")]
        public bool playBeforeInput;
        [Tooltip("ON = after the step (including Post Delay) the fighters/referee go back to Idle. Set the " +
                 "step's Duration (or Post Delay for a bow) to at least the length of its animation.")]
        public bool idleAfterStep = true;
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
    [Tooltip("Animator state played to return anyone to rest after a step that used Animator states.")]
    [SerializeField] private string idleState = "Idle";
    [Tooltip("After the player presses to bow, wait at least this many seconds (for the bow animation to play) before " +
             "the next step starts and before everyone goes back to Idle. Applies to every step that waits for input.")]
    [SerializeField] private float bowHoldSeconds = 1f;
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
    [Header("Result call")]
    [SerializeField] private string playerWinText = "Player wins!";
    [SerializeField] private string playerLoseText = "Player loses!";
    [Tooltip("Optional referee Animator triggers for the result (blank = skipped).")]
    [SerializeField] private string refereeWinTrigger = "PlayerWins";
    [SerializeField] private string refereeLoseTrigger = "EnemyWins";
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
    [Tooltip("Used instead of the triggers above when 2 or more points are awarded at once (blank = use the 1-point trigger).")]
    [SerializeField] private string refereePlayerPoint2Trigger = "PointPlayer2";
    [SerializeField] private string refereeEnemyPoint2Trigger = "PointEnemy2";
    [SerializeField] private string refereeResumeTrigger = "Begin";
    [Tooltip("Seconds after the reset begins before the referee's point / warning animation (and its SFX) plays - " +
             "set it to the fighters' reset time (GameManager Reset Duration) so it starts once they are back in place. " +
             "The call text still shows straight away.")]
    [SerializeField] private float callAnimDelayAfterReset = 0.8f;
    private bool resetFired;
    [Tooltip("Optional AudioManager SFX ids (inert until sounds exist).")]
    [SerializeField] private string pointSfxId;
    [SerializeField] private string resumeSfxId;
    /// <summary>Fired when the match is decided (true = the player won). Used by FighterReactions.</summary>
    public event System.Action<bool> OnResultAnnounced;
    public UnityEvent onPlayerPoint;
    public UnityEvent onEnemyPoint;

    [Header("When it ends")]
    [Tooltip("Fight music (AudioManager SoundLibrary id). Starts when the step with 'Start Music " +
             "On Step Start' ticked (Hajime) begins. If no step has it ticked, or Skip Intro is on, " +
             "it starts when the fight begins. Empty = no music.")]
    [SerializeField] private string musicId;
    [SerializeField] private float musicFadeIn = 3f;
    [Header("Intro music (optional)")]
    [Tooltip("Plays from the start of the pre-fight ritual. Fades out after the player's Nth bow " +
             "(below). While this is set, the fight music above starts at Hajime with NO fade-in. " +
             "Empty = no intro music, fight music behaves as before.")]
    [SerializeField] private string introMusicId;
    [SerializeField] private float introMusicFadeIn = 1f;
    [Tooltip("How long the intro music takes to fade out once the bow is pressed.")]
    [SerializeField] private float introMusicFadeOut = 1.5f;
    [Tooltip("Fade the intro out after this many bows (steps that wait for the player's press). " +
             "2 = after the second bow.")]
    [SerializeField] private int introEndsAfterBow = 2;
    private int bowsDone;
    private bool introPlaying;
    [Tooltip("Fired right as the fight starts, after the timer is started.")]
    public UnityEvent onFightBegin;

    [Header("Testing")]
    [Tooltip("Skip the whole ritual and start the fight immediately.")]
    [SerializeField] private bool skipIntro;

    public bool IsRunning { get; private set; }
    /// <summary>The referee's transform (what the fighters face / bow to). Used by FighterReactions.</summary>
    public Transform RefereeTransform => refereeTransform;

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
            CallPoint(playerPointText, playerGain, PickPointTrigger(playerGain, refereePlayerPointTrigger, refereePlayerPoint2Trigger));
            onPlayerPoint?.Invoke();
        }
        else if (enemyGain > 0)
        {
            CallPoint(enemyPointText, enemyGain, PickPointTrigger(enemyGain, refereeEnemyPointTrigger, refereeEnemyPoint2Trigger));
            onEnemyPoint?.Invoke();
        }
    }

    private static string PickPointTrigger(int points, string one, string two)
        => (points >= 2 && !string.IsNullOrEmpty(two)) ? two : one;

    /// <summary>A stop that isn't a point (out-of-bounds warning / deduction): same flow as a point -
    /// short wait, Yame, reset, then the call text - but nobody scores. Used by OutOfBounds.</summary>
    public void CallPenalty(string text, string animatorTrigger)
    {
        if (!fightStarted) return;
        Debug.Log($"[Referee] Penalty call: {text}");
        if (pointSequenceRoutine != null) StopCoroutine(pointSequenceRoutine);
        pointSequenceRoutine = StartCoroutine(PostPointSequence(text ?? "", animatorTrigger));
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
        Debug.Log($"[Referee] Point sequence started - waiting {hitAnimDelay}s for hit anims.");
        yield return new WaitForSecondsRealtime(hitAnimDelay);

        resetFired = false;
        Debug.Log("[Referee] Yame!");
        Trigger(refereeAnimator, refereeYameTrigger);
        if (!string.IsNullOrEmpty(yameSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(yameSfxId);
        ShowTimed(yameText, yameDuration);
        yield return new WaitForSecondsRealtime(yameDuration);

        Debug.Log($"[Referee] Firing On Ready To Reset ({onReadyToReset.GetPersistentEventCount()} " +
                  "Inspector listeners) - if that's 0, nothing is bound to it, so no reset will happen.");
        onReadyToReset?.Invoke();

        resetFired = true;
        ShowTimed(pointText, pointTextDuration);

        // The referee's point / warning animation starts only once the fighters are back in place.
        if (callAnimDelayAfterReset > 0f) yield return new WaitForSecondsRealtime(callAnimDelayAfterReset);
        Trigger(refereeAnimator, pointTrigger);
        if (!string.IsNullOrEmpty(pointSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(pointSfxId);
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
            if (!resetFired)
                Debug.LogWarning("[Referee] Play resumed BEFORE the point sequence finished - the reset " +
                                 "was skipped. Shorten Hit Anim Delay / Yame Duration, or raise MatchTimer's " +
                                 "Hit Pause Duration.");
            else
                Debug.LogWarning("[Referee] Play resumed before the referee's point/warning animation started - " +
                                 "lower Call Anim Delay After Reset or raise MatchTimer's Hit Pause Duration.");
            StopCoroutine(pointSequenceRoutine);
            pointSequenceRoutine = null;
        }
        Trigger(refereeAnimator, refereeResumeTrigger);
        if (!string.IsNullOrEmpty(resumeSfxId) && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(resumeSfxId);
        ShowTimed(resumeText, resumeTextDuration);
    }

    private void OnTimeUp() => ShowTimed(timeUpText, 3f);

    /// <summary>Called by MatchScore when the match is decided: shows the result text for
    /// `seconds` and fires the referee's win/lose trigger.</summary>
    public void AnnounceResult(bool playerWon, float seconds)
    {
        if (pointSequenceRoutine != null) { StopCoroutine(pointSequenceRoutine); pointSequenceRoutine = null; }
        OnResultAnnounced?.Invoke(playerWon);   // FighterReactions plays the fighters' win / lose animations
        Trigger(refereeAnimator, playerWon ? refereeWinTrigger : refereeLoseTrigger);
        ShowTimed(playerWon ? playerWinText : playerLoseText, seconds);
    }

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
            StartIntroMusic();

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

        bool usesStates = !string.IsNullOrEmpty(step.playerState) || !string.IsNullOrEmpty(step.opponentState) ||
                          !string.IsNullOrEmpty(step.refereeState);
        // A step that waits for the button (a bow) plays its animations/triggers when the player presses, unless 'Play Before Input' is ticked.
        bool deferToInput = step.waitForPlayerInput && !step.playBeforeInput;
        bool statesPlayed = false;
        if (usesStates && !deferToInput)
        {
            PlayStates(step);
            statesPlayed = true;
        }

        Debug.Log($"[Referee] Step '{step.name}': waitForPlayerInput={step.waitForPlayerInput}, playBeforeInput={step.playBeforeInput} -> " +
                  (deferToInput ? "bow waits for the button." : "anims/triggers fire NOW at step start."));
        if (!deferToInput) FireStepTriggers(step);

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
            if (usesStates && !statesPlayed) { PlayStates(step); statesPlayed = true; }
            if (deferToInput) FireStepTriggers(step);
            bowsDone++;
            if (bowsDone >= introEndsAfterBow) StopIntroMusic();
        }

        float after = step.postDelay;
        if (step.waitForPlayerInput) after = Mathf.Max(after, bowHoldSeconds);   // let the bow play out
        if (after > 0f) yield return new WaitForSecondsRealtime(after);

        if (statesPlayed && step.idleAfterStep)
        {
            if (!string.IsNullOrEmpty(step.playerState)) PlayState(playerAnimator, idleState, "player");
            if (!string.IsNullOrEmpty(step.opponentState)) PlayState(opponentAnimator, idleState, "opponent");
            if (!string.IsNullOrEmpty(step.refereeState)) PlayState(refereeAnimator, idleState, "referee");
        }
    }

    private void FireStepTriggers(Step step)
    {
        if (!string.IsNullOrEmpty(step.playerTrigger) || !string.IsNullOrEmpty(step.opponentTrigger) || !string.IsNullOrEmpty(step.refereeTrigger))
            Debug.Log($"[Referee] Step '{step.name}': firing triggers (player '{step.playerTrigger}', opponent '{step.opponentTrigger}', referee '{step.refereeTrigger}').");
        Trigger(playerAnimator, step.playerTrigger);
        Trigger(opponentAnimator, step.opponentTrigger);
        Trigger(refereeAnimator, step.refereeTrigger);
    }

    private void PlayStates(Step step)
    {
        PlayState(playerAnimator, step.playerState, "player");
        PlayState(opponentAnimator, step.opponentState, "opponent");
        PlayState(refereeAnimator, step.refereeState, "referee");
    }

    private static void PlayState(Animator animator, string state, string who = "")
    {
        if (string.IsNullOrEmpty(state)) return;
        if (animator == null)
        {
            Debug.LogWarning($"[Referee] Step state '{state}' for the {who}: that Animator is NOT assigned on the RefereeController " +
                             "(References > Player/Opponent/Referee Animator).");
            return;
        }
        int hash = Animator.StringToHash(state);
        if (animator.HasState(0, hash))
        {
            animator.Play(hash, 0, 0f);
            Debug.Log($"[Referee] {who} ({animator.gameObject.name}): playing state '{state}'.");
        }
        else
            Debug.LogWarning($"[Referee] {who} ({animator.gameObject.name}): Animator has no state named '{state}' on layer 0 - skipped.");
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

    private void StartIntroMusic()
    {
        if (string.IsNullOrEmpty(introMusicId) || AudioManager.instance == null) return;
        introPlaying = true;
        AudioManager.instance.PlayMusic(introMusicId, introMusicFadeIn);
    }

    private void StopIntroMusic()
    {
        if (!introPlaying) return;
        introPlaying = false;
        if (AudioManager.instance != null) AudioManager.instance.StopMusic(introMusicFadeOut);
    }

    private void StartMusicOnce()
    {
        if (musicStarted || string.IsNullOrEmpty(musicId)) return;
        musicStarted = true;

        StopIntroMusic(); // safety: if the ritual ended before the Nth bow, don't leave it playing
        if (AudioManager.instance != null)
            AudioManager.instance.PlayMusic(musicId, musicFadeIn, string.IsNullOrEmpty(introMusicId) ? -1f : 0f);
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