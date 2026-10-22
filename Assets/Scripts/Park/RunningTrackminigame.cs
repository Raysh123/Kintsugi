using UnityEngine;

/// <summary>
/// Tracks the mash-to-run minigame's speed and elapsed time. Does NOT move anything
/// itself and does NOT decide when the run ends � a RunnerMover reads CurrentSpeed to
/// actually move the runner, and a RunningFinishLine trigger calls CompleteRun() when
/// the runner physically reaches the end of the track.
/// </summary>
public class RunningTrackMinigame : MonoBehaviour
{
    private enum LastPress { None, Left, Right }

    [Header("Speed")]
    [SerializeField] private float speedBoostPerAlternation = 8f;
    [Tooltip("How fast speed bleeds off WHILE still within the No Input Stop Delay window " +
             "(i.e. the natural gaps between alternating key presses while actively mashing).")]
    [SerializeField] private float speedDecayPerSecond = 4f;
    [SerializeField] private float maxSpeed = 20f;
    [Tooltip("If no alternating key press happens for this many seconds, speed snaps to zero " +
             "immediately instead of continuing to gradually decay � this is what stops the " +
             "'coasting' feel once the player actually lets go, rather than it slowly bleeding " +
             "off over several seconds.")]
    [SerializeField] private float noInputStopDelay = 0.25f;

    [Header("Footsteps")]
    [Tooltip("SoundLibrary ids, used in turn for each step (e.g. left foot, right foot). One id is fine. Blank list = no footsteps.")]
    [SerializeField] private string[] stepSoundIds = new string[0];
    [Range(0f, 2f)][SerializeField] private float stepVolume = 1f;
    [Tooltip("When the average gap between your last strides is shorter than this (seconds), the steps are averaged into a steady run.")]
    [SerializeField] private float mashGap = 0.2f;
    [Tooltip("Seconds between steps in that steady run - a normal running pace.")]
    [SerializeField] private float steadyInterval = 0.26f;
    [Tooltip("How many recent gaps are averaged.")]
    [SerializeField] private int averageOver = 4;

    [Header("Reward Curve")]
    [Tooltip("Finish time (seconds) that earns the max reward.")]
    [SerializeField] private float bestTime = 8f;
    [Tooltip("Finish time (seconds) that earns the min reward.")]
    [SerializeField] private float worstTime = 20f;
    [SerializeField] private float maxMotivationReward = 20f;
    [SerializeField] private float minMotivationReward = 5f;

    public bool finished { get; private set; }
    public float finishTime { get; private set; }
    public float motivationEarned { get; private set; }
    public float CurrentSpeed { get; private set; }

    private float elapsedTime;
    private float timeSinceLastPress;
    private LastPress lastPress = LastPress.None;
    private bool isRunning;

    public void ResetMinigame()
    {
        finished = false;
        finishTime = 0f;
        motivationEarned = 0f;
        CurrentSpeed = 0f;
        elapsedTime = 0f;
        timeSinceLastPress = 0f;
        lastPress = LastPress.None;
        isRunning = true;
        lastStrideTime = -10f;
        gapCount = gapIndex = 0;
        steady = false;
    }

    // ---- footsteps: one per accepted stride; very fast mashing is averaged into a steady running pace
    private float lastStrideTime = -10f;
    private float[] gaps;
    private int gapCount, gapIndex, stepIndex;
    private float nextSteady;
    private bool steady;

    private void OnStride()
    {
        float now = Time.time;
        float gap = now - lastStrideTime;
        lastStrideTime = now;
        if (gaps == null || gaps.Length != Mathf.Max(2, averageOver)) gaps = new float[Mathf.Max(2, averageOver)];

        if (gap > 1f) { gapCount = gapIndex = 0; steady = false; PlayStep(); return; }   // first stride after a pause

        gaps[gapIndex] = gap;
        gapIndex = (gapIndex + 1) % gaps.Length;
        gapCount = Mathf.Min(gapCount + 1, gaps.Length);
        float sum = 0f;
        for (int i = 0; i < gapCount; i++) sum += gaps[i];

        if (gapCount >= 3 && sum / gapCount < mashGap)
        {
            if (!steady) { steady = true; nextSteady = now + steadyInterval; }   // the timer in Update plays the steps now
        }
        else
        {
            steady = false;
            PlayStep();
        }
    }

    private void PlayStep()
    {
        if (stepSoundIds == null || stepSoundIds.Length == 0) return;
        Sfx.Play(stepSoundIds[stepIndex % stepSoundIds.Length], stepVolume);
        stepIndex++;
    }

    private void Update()
    {
        if (!isRunning) return;

        elapsedTime += Time.deltaTime;
        timeSinceLastPress += Time.deltaTime;

        bool pressedLeft = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.JoystickButton4);
        bool pressedRight = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.JoystickButton5);

        if (pressedLeft && lastPress != LastPress.Left)
        {
            lastPress = LastPress.Left;
            timeSinceLastPress = 0f;
            CurrentSpeed = Mathf.Min(CurrentSpeed + speedBoostPerAlternation, maxSpeed);
            OnStride();
        }
        else if (pressedRight && lastPress != LastPress.Right)
        {
            lastPress = LastPress.Right;
            timeSinceLastPress = 0f;
            CurrentSpeed = Mathf.Min(CurrentSpeed + speedBoostPerAlternation, maxSpeed);
            OnStride();
        }

        if (steady)
        {
            if (timeSinceLastPress >= noInputStopDelay) { steady = false; gapCount = gapIndex = 0; }
            else if (Time.time >= nextSteady) { PlayStep(); nextSteady = Time.time + steadyInterval; }
        }

        if (timeSinceLastPress >= noInputStopDelay)
        {
            // Player has genuinely stopped mashing � kill speed immediately rather than
            // letting it coast down over several seconds.
            CurrentSpeed = 0f;
        }
        else
        {
            // Still within a normal gap between alternating presses � decay gently so
            // mashing at a steady rhythm doesn't stutter between each key press.
            CurrentSpeed = Mathf.Max(0f, CurrentSpeed - speedDecayPerSecond * Time.deltaTime);
        }
    }

    /// <summary>
    /// Called by RunningFinishLine when the runner reaches the end of the track.
    /// </summary>
    public void CompleteRun()
    {
        if (finished || !isRunning) return;

        isRunning = false;
        finished = true;
        finishTime = elapsedTime;

        float t = Mathf.Clamp01(Mathf.InverseLerp(worstTime, bestTime, finishTime));
        motivationEarned = Mathf.Lerp(minMotivationReward, maxMotivationReward, t);
    }
}