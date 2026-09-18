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
    [SerializeField] private float speedDecayPerSecond = 4f;
    [SerializeField] private float maxSpeed = 20f;

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
    private LastPress lastPress = LastPress.None;
    private bool isRunning;

    public void ResetMinigame()
    {
        finished = false;
        finishTime = 0f;
        motivationEarned = 0f;
        CurrentSpeed = 0f;
        elapsedTime = 0f;
        lastPress = LastPress.None;
        isRunning = true;
    }

    private void Update()
    {
        if (!isRunning) return;

        elapsedTime += Time.deltaTime;

        bool pressedLeft = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.JoystickButton4);
        bool pressedRight = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.JoystickButton5);

        if (pressedLeft && lastPress != LastPress.Left)
        {
            lastPress = LastPress.Left;
            CurrentSpeed = Mathf.Min(CurrentSpeed + speedBoostPerAlternation, maxSpeed);
        }
        else if (pressedRight && lastPress != LastPress.Right)
        {
            lastPress = LastPress.Right;
            CurrentSpeed = Mathf.Min(CurrentSpeed + speedBoostPerAlternation, maxSpeed);
        }

        CurrentSpeed = Mathf.Max(0f, CurrentSpeed - speedDecayPerSecond * Time.deltaTime);
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