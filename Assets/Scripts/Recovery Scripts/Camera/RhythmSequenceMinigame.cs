using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A rhythm-sequence recovery minigame — the player is shown a looping
/// sequence of beats (e.g. Up/Down for a push-up) and must press the
/// matching button within each beat's timing window.
///
/// Rather than a single mistake ending the attempt, this runs on three
/// counters that race toward thresholds:
///
///   successCount   → winThreshold hits      → Won
///   failCount      → failThreshold misses   → Lost      (missed a normal beat)
///   riskyMissCount → injuryThreshold misses → Injured   (missed a beat flagged "risky")
///
/// The beat pattern loops once it reaches the end — the minigame keeps going
/// until one of the thresholds above is hit, not until the pattern runs out.
///
/// Each beat's timing window is scaled by HealthAndMotivation.instance.currentMotivation
/// at the moment that beat starts: lower motivation means a shorter window,
/// i.e. a harder minigame.
///
/// This script only tracks input and outcome — it doesn't touch the calendar,
/// player state, or trigger detection. Recovery_Interaction_Type_C is the
/// wrapper responsible for starting/stopping it, playing the activity
/// animation, activating the camera, and reading won/lost/injured once
/// `finished` is true (same role RecoveryInteraction plays for Pointer, and
/// Recovery_Interaction_Type_B plays for TimedPointer).
/// </summary>
public class RhythmSequenceMinigame : MonoBehaviour
{
    public enum BeatButton { Up, Down }

    [System.Serializable]
    public class RhythmBeat
    {
        public BeatButton expectedButton;

        [Tooltip("Base seconds allowed to hit this beat, before motivation scaling is applied.")]
        public float timingWindow = 0.6f;

        [Tooltip("If true, missing this beat fills the injury meter instead of the fail meter.")]
        public bool isRisky;
    }

    [Header("Sequence")]
    [Tooltip("Manually authored beat pattern. Loops once the end is reached — the minigame keeps " +
             "going until a win/fail/injury threshold is hit, not until the list runs out.")]
    [SerializeField] private List<RhythmBeat> beats = new List<RhythmBeat>();

    [Tooltip("If true, builds a randomized Down/Up pattern at runtime instead of using the list above. " +
             "Button choice per beat is random since animation is no longer tied to which button it is.")]
    [SerializeField] private bool autoGenerateAlternating = true;
    [SerializeField] private int autoPatternLength = 6;
    [SerializeField] private float autoTimingWindow = 0.6f;
    [Tooltip("Every Nth beat in the pattern is risky (0 = never).")]
    [SerializeField] private int autoRiskyEveryNthRep = 3;

    [Header("Thresholds")]
    [Tooltip("Successful hits needed to win.")]
    public int winThreshold = 12;
    [Tooltip("Regular misses that cause a loss.")]
    public int failThreshold = 5;
    [Tooltip("Risky misses that cause an injury.")]
    public int injuryThreshold = 3;

    [Header("Timing")]
    [Tooltip("Delay before the first beat activates, so the start animation has time to play.")]
    [SerializeField] private float startDelay = 1f;

    [Header("Motivation Difficulty Scaling")]
    [Tooltip("Reads HealthAndMotivation.instance.currentMotivation and scales each beat's timing " +
             "window with it — lower motivation means a shorter window, i.e. a harder minigame.")]
    [SerializeField] private bool scaleWithMotivation = true;
    [SerializeField] private float motivationRangeMin = 0f;
    [SerializeField] private float motivationRangeMax = 100f;
    [Tooltip("Timing window multiplier applied at the lowest expected motivation.")]
    [SerializeField] private float timingMultiplierAtMinMotivation = 0.6f;
    [Tooltip("Timing window multiplier applied at the highest expected motivation.")]
    [SerializeField] private float timingMultiplierAtMaxMotivation = 1.4f;
    [Tooltip("Floor so the window never becomes impossible to react to.")]
    [SerializeField] private float minTimingWindowFloor = 0.15f;

    [Header("Camera")]
    [Tooltip("Optional direct reference. If left blank, resolved automatically from Camera.main at Start.")]
    [SerializeField] private TopDownCameraFollow cameraFollow;

    [Header("Optional Animation Hooks")]
    [Tooltip("If assigned, this script plays Up/Down/mistake/injured anim states itself. " +
             "Up/Down only play when the player presses the correct button, never when the prompt appears. " +
             "Leave blank if the wrapper script is driving animation instead.")]
    [SerializeField] private Animator activityAnimator;
    [SerializeField] private string upAnimState;
    [SerializeField] private string downAnimState;
    [SerializeField] private string mistakeAnimState;
    [SerializeField] private string injuredAnimState;
    [Tooltip("If true, Up/Down animation follows each beat's actual required button (so it randomizes " +
             "along with it — good for exercises like kicks where either direction reads fine). If " +
             "false, the animation ignores the button and just alternates Down/Up on its own fixed " +
             "cycle regardless of what's required — good for exercises like squats that need a " +
             "consistent physical cycle even though the input itself is randomized.")]
    [SerializeField] private bool tieAnimationToButton = false;

    [Header("Outcome")]
    [SerializeField] private bool _won;
    [SerializeField] private bool _lost;
    [SerializeField] private bool _injured;
    [Tooltip("True once won/lost/injured has been set.")]
    [SerializeField] private bool _finished;
    public bool won => _won;
    public bool lost => _lost;
    public bool injured => _injured;
    public bool finished => _finished;

    [Header("Progress (read-only, drives the UI meters)")]
    public int successCount;
    public int failCount;
    public int riskyMissCount;

    //[Header("Events (for UI)")]
    [Tooltip("Fired whenever a new beat becomes active — UI should show the prompt for it.")]
    public event System.Action<RhythmBeat> OnBeatStarted;
    [Tooltip("Fired when the active beat resolves (true = correct press, false = miss/wrong).")]
    public event System.Action<bool> OnBeatResolved;
    [Tooltip("Fired whenever a counter changes: (successCount, failCount, riskyMissCount).")]
    public event System.Action<int, int, int> OnProgressChanged;

    // Input is fixed rather than Inspector-configurable: Up = Right Shoulder (RB) / Right Mouse Button,
    // Down = Left Shoulder (LB) / Left Mouse Button. Matches TopDownCameraFollow's orbit buttons 1:1,
    // which is exactly why the camera lock below exists.
    private static readonly List<KeyCode> upKeys = new List<KeyCode> { KeyCode.JoystickButton5, KeyCode.Mouse1 };
    private static readonly List<KeyCode> downKeys = new List<KeyCode> { KeyCode.JoystickButton4, KeyCode.Mouse0 };

    private int currentBeatIndex;
    private float beatTimer;
    private bool sequenceActive;
    private bool waitingForStart = true;
    private bool cameraOrbitWasEnabled;
    private bool cameraRotationLocked;
    [Tooltip("Tracks the Down/Up movement animation cycle — independent of which button each beat actually requires.")]
    private bool nextMoveIsDown = true;

    private void Start()
    {
        LockCameraRotation();

        if (autoGenerateAlternating) GenerateAlternatingSequence();
        StartCoroutine(BeginAfterDelay());
    }

    private void OnDisable()
    {
        RestoreCameraRotation();
    }

    private void OnDestroy()
    {
        RestoreCameraRotation();
    }

    private IEnumerator BeginAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);

        if (beats.Count == 0) yield break;

        waitingForStart = false;
        sequenceActive = true;
        currentBeatIndex = 0;
        StartBeat(currentBeatIndex);
    }

    private void Update()
    {
        if (finished || waitingForStart || !sequenceActive) return;

        beatTimer -= Time.deltaTime;

        bool upPressed = AnyKeyDown(upKeys);
        bool downPressed = AnyKeyDown(downKeys);

        if (upPressed || downPressed)
        {
            HandlePress(upPressed ? BeatButton.Up : BeatButton.Down);
        }
        else if (beatTimer <= 0f)
        {
            HandleMiss(beats[currentBeatIndex]);
        }
    }

    // ── Beat lifecycle ───────────────────────────────────────────────────────

    private void StartBeat(int index)
    {
        RhythmBeat beat = beats[index];
        beatTimer = GetTimingWindowForBeat(beat);
        OnBeatStarted?.Invoke(beat);
    }

    private void HandlePress(BeatButton pressed)
    {
        RhythmBeat beat = beats[currentBeatIndex];

        if (pressed == beat.expectedButton)
        {
            PlayMovementAnim(beat);
            successCount++;
            OnBeatResolved?.Invoke(true);
            OnProgressChanged?.Invoke(successCount, failCount, riskyMissCount);

            if (successCount >= winThreshold)
            {
                _won = true;
                EndSequence();
            }
            else
            {
                AdvanceBeat();
            }
        }
        else
        {
            HandleMiss(beat);
        }
    }

    private void HandleMiss(RhythmBeat beat)
    {
        if (beat.isRisky)
        {
            riskyMissCount++;
            PlayAnimState(injuredAnimState);
        }
        else
        {
            failCount++;
            PlayAnimState(mistakeAnimState);
        }

        OnBeatResolved?.Invoke(false);
        OnProgressChanged?.Invoke(successCount, failCount, riskyMissCount);

        if (riskyMissCount >= injuryThreshold)
        {
            _injured = true;
            EndSequence();
        }
        else if (failCount >= failThreshold)
        {
            _lost = true;
            EndSequence();
        }
        else
        {
            AdvanceBeat();
        }
    }

    private void AdvanceBeat()
    {
        currentBeatIndex = (currentBeatIndex + 1) % beats.Count;
        StartBeat(currentBeatIndex);
    }

    private void EndSequence()
    {
        _finished = true;
        sequenceActive = false;
        RestoreCameraRotation();
    }

    // ── Motivation scaling ───────────────────────────────────────────────────

    private float GetTimingWindowForBeat(RhythmBeat beat)
    {
        if (!scaleWithMotivation || HealthAndMotivation.instance == null)
            return beat.timingWindow;

        float motivation = HealthAndMotivation.instance.currentMotivation;
        float t = Mathf.InverseLerp(motivationRangeMin, motivationRangeMax, motivation);
        float multiplier = Mathf.Lerp(timingMultiplierAtMinMotivation, timingMultiplierAtMaxMotivation, t);

        return Mathf.Max(minTimingWindowFloor, beat.timingWindow * multiplier);
    }

    // ── Sequence generation ──────────────────────────────────────────────────

    private void GenerateAlternatingSequence()
    {
        beats.Clear();
        for (int i = 0; i < autoPatternLength; i++)
        {
            BeatButton button = Random.value < 0.5f ? BeatButton.Down : BeatButton.Up;
            bool risky = autoRiskyEveryNthRep > 0 && (i + 1) % autoRiskyEveryNthRep == 0;

            beats.Add(new RhythmBeat
            {
                expectedButton = button,
                timingWindow = autoTimingWindow,
                isRisky = risky
            });
        }
    }

    // ── Animation helpers ──────────────────────────────────────────────────────

    private void PlayMovementAnim(RhythmBeat beat)
    {
        string state;

        if (tieAnimationToButton)
        {
            // Follows whichever button this specific beat requires — since the button
            // itself is randomized, the animation effectively randomizes with it too.
            // Good fit for exercises where either direction reads fine either way (kicks).
            state = beat.expectedButton == BeatButton.Down ? downAnimState : upAnimState;
        }
        else
        {
            // Ignores the beat's button entirely — just alternates on its own fixed
            // cycle. Good fit for exercises that need a consistent physical cycle
            // regardless of random input (squats always look like a real squat).
            state = nextMoveIsDown ? downAnimState : upAnimState;
            nextMoveIsDown = !nextMoveIsDown;
        }

        PlayAnimState(state);
    }

    private void PlayAnimState(string state)
    {
        if (string.IsNullOrEmpty(state)) return;

        if (activityAnimator == null) return;

        activityAnimator.Play(state);
    }

    // ── Input helpers ────────────────────────────────────────────────────────

    private bool AnyKeyDown(List<KeyCode> keys)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            if (Input.GetKeyDown(keys[i])) return true;
        }
        return false;
    }

    // ── Camera lock ──────────────────────────────────────────────────────────

    private void LockCameraRotation()
    {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<TopDownCameraFollow>();

        if (cameraFollow == null || cameraRotationLocked) return;

        cameraOrbitWasEnabled = cameraFollow.enableOrbitRotation;
        cameraFollow.enableOrbitRotation = false;
        cameraRotationLocked = true;
    }

    private void RestoreCameraRotation()
    {
        if (cameraFollow == null || !cameraRotationLocked) return;

        cameraFollow.enableOrbitRotation = cameraOrbitWasEnabled;
        cameraRotationLocked = false;
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    /// <summary>Call to restart the minigame (e.g. re-attempting the activity).</summary>
    public void ResetMinigame()
    {
        _won = _lost = _injured = _finished = false;
        successCount = failCount = riskyMissCount = 0;
        currentBeatIndex = 0;
        waitingForStart = true;
        sequenceActive = false;
        nextMoveIsDown = true;
        StopAllCoroutines();
        LockCameraRotation();
        StartCoroutine(BeginAfterDelay());
    }
}