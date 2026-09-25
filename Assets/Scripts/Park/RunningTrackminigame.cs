using UnityEngine;

/// <summary>
/// Tracks the mash-to-run minigame's speed and elapsed time. Does NOT move anything
/// itself and does NOT decide when the run ends — a RunnerMover reads CurrentSpeed to
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
             "immediately instead of continuing to gradually decay — this is what stops the " +
             "'coasting' feel once the player actually lets go, rather than it slowly bleeding " +
             "off over several seconds.")]
    [SerializeField] private float noInputStopDelay = 0.25f;

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
        }
        else if (pressedRight && lastPress != LastPress.Right)
        {
            lastPress = LastPress.Right;
            timeSinceLastPress = 0f;
            CurrentSpeed = Mathf.Min(CurrentSpeed + speedBoostPerAlternation, maxSpeed);
        }

        if (timeSinceLastPress >= noInputStopDelay)
        {
            // Player has genuinely stopped mashing — kill speed immediately rather than
            // letting it coast down over several seconds.
            CurrentSpeed = 0f;
        }
        else
        {
            // Still within a normal gap between alternating presses — decay gently so
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