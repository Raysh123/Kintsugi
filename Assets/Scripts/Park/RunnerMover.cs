using UnityEngine;

/// <summary>
/// Moves the runner (the running activity model) forward along the track at
/// RunningTrackMinigame's CurrentSpeed. Replaces the old ScrollingGround approach —
/// now the character actually runs down the track instead of the ground scrolling
/// underneath a stationary character.
/// </summary>
public class RunnerMover : MonoBehaviour
{
    [SerializeField] private RunningTrackMinigame minigame;
    [Tooltip("The transform that actually moves — usually the runningActivityModel, " +
             "or a dedicated root if the model's local transform is used for animation.")]
    [SerializeField] private Transform runner;
    [Tooltip("Local-to-runner direction to move in, e.g. Vector3.forward if the model " +
             "faces down the track.")]
    [SerializeField] private Vector3 moveDirection = Vector3.forward;

    [Header("Animation")]
    [Tooltip("The Animator on the running model. Its Speed parameter is fed minigame.CurrentSpeed " +
             "every frame so a blend tree can match footfall to actual movement speed — without this, " +
             "the model moves at an exact, non-physical speed while the animation clip plays at its own " +
             "fixed rate, which reads visually as sliding/skating rather than genuinely accelerating.")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameterName = "Speed";

    /// <summary>
    /// Places the runner at the given start point — ParkActivity should call this
    /// (with its runStartPosition transform) right before minigame.ResetMinigame()
    /// each time the running phase begins.
    ///
    /// Only POSITION is taken from startPoint. Rotation is left untouched, since the
    /// runner (and the camera, if it's parented to it) already has whatever orientation
    /// you set up by hand in the Editor — copying startPoint's rotation on top of that
    /// would silently override it every time the activity starts.
    /// </summary>
    public void ResetPosition(Transform startPoint)
    {
        if (runner == null || startPoint == null) return;
        runner.position = startPoint.position;
    }

    private bool warnedMissingRefs;
    private bool loggedFirstMove;

    private void Update()
    {
        if (minigame == null || runner == null)
        {
            if (!warnedMissingRefs)
            {
                warnedMissingRefs = true;
                if (minigame == null) Debug.LogWarning("[RunnerMover] Minigame not assigned.");
                if (runner == null) Debug.LogWarning("[RunnerMover] Runner not assigned.");
            }
            return;
        }

        UpdateAnimatorSpeed();

        if (minigame.finished) return;

        float moveAmount = minigame.CurrentSpeed * Time.deltaTime;
        if (moveAmount <= 0f) return;

        if (!loggedFirstMove)
        {
            loggedFirstMove = true;
            Debug.Log($"[RunnerMover] Runner started moving. CurrentSpeed={minigame.CurrentSpeed}");
        }

        Vector3 direction = runner.TransformDirection(moveDirection.normalized);
        runner.position += direction * moveAmount;
    }

    private void UpdateAnimatorSpeed()
    {
        if (animator == null) return;

        // Feeds the RAW speed value (same units as speedBoostPerAlternation/maxSpeed on
        // RunningTrackMinigame) — set your blend tree's motion thresholds to match that
        // same range (e.g. 0 = idle, ~half of Max Speed = jog, Max Speed = sprint) rather
        // than normalizing to 0-1 here.
        animator.SetFloat(speedParameterName, minigame.CurrentSpeed);
    }
}