using UnityEngine;

/// <summary>
/// Moves the runner (the running activity model) forward along the track at RunningTrackMinigame's CurrentSpeed, and animates it so
/// the running animation is exactly as fast as the player is mashing.
///
/// Animation (pick a mode):
///   ClipSpeed      (default) A single run loop. While the player is mashing the Run state plays and its playback speed follows
///                  CurrentSpeed, so mashing faster = a faster stride; when the player stops mashing it goes to Idle. No blend tree
///                  or parameters are needed - just two Animator state names.
///   SpeedParameter The old way: CurrentSpeed is sent to a float parameter on the Animator for a blend tree to use.
/// </summary>
public class RunnerMover : MonoBehaviour
{
    public enum AnimationMode { ClipSpeed, SpeedParameter }

    [SerializeField] private RunningTrackMinigame minigame;
    [Tooltip("The transform that actually moves - usually the runningActivityModel, " +
             "or a dedicated root if the model's local transform is used for animation.")]
    [SerializeField] private Transform runner;
    [Tooltip("Local-to-runner direction to move in, e.g. Vector3.forward if the model " +
             "faces down the track.")]
    [SerializeField] private Vector3 moveDirection = Vector3.forward;

    [Header("Animation")]
    [Tooltip("The Animator on the running model.")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationMode mode = AnimationMode.ClipSpeed;

    [Header("Clip Speed mode (run loop follows the mashing)")]
    [Tooltip("Animator state played while the player is mashing (the run loop). Blank = don't switch states, only change the speed.")]
    [SerializeField] private string runState = "Run";
    [Tooltip("Animator state played when the player isn't mashing (and at the finish). Blank = no switch.")]
    [SerializeField] private string idleState = "Idle";
    [Tooltip("Run speed (CurrentSpeed) at which the animation plays at Max Anim Speed. Set it to the minigame's Max Speed (20 by default).")]
    [SerializeField] private float fullAnimAtSpeed = 20f;
    [Tooltip("Animation playback speed when barely moving (a first slow press).")]
    [SerializeField] private float minAnimSpeed = 0.5f;
    [Tooltip("Animation playback speed at full speed. 1 = the clip's own speed, 2 = twice as fast.")]
    [SerializeField] private float maxAnimSpeed = 2f;
    [Tooltip("Seconds the animation speed takes to catch up with a change in running speed. Lower = snappier, higher = smoother.")]
    [SerializeField] private float animSpeedSmoothing = 0.08f;
    [Tooltip("Seconds to blend between Idle and Run.")]
    [SerializeField] private float crossFadeTime = 0.1f;
    [Tooltip("Below this running speed the player counts as standing still.")]
    [SerializeField] private float stopSpeed = 0.05f;

    [Header("Speed Parameter mode (blend tree)")]
    [Tooltip("The Animator float that receives CurrentSpeed every frame. Set the blend tree's thresholds in the same units as the minigame's speed.")]
    [SerializeField] private string speedParameterName = "Speed";

    private bool wasMoving;
    private float animSpeedVel;

    /// <summary>
    /// Places the runner at the given start point - ParkActivity calls this (with its runStartPosition transform) right before
    /// minigame.ResetMinigame() each time the running phase begins.
    ///
    /// Only POSITION is taken from startPoint. Rotation is left untouched, since the runner (and the camera, if it's parented to it)
    /// already has whatever orientation you set up by hand in the Editor.
    /// </summary>
    public void ResetPosition(Transform startPoint)
    {
        if (runner != null && startPoint != null) runner.position = startPoint.position;
        wasMoving = false;
        animSpeedVel = 0f;
        if (animator != null)
        {
            animator.speed = 1f;
            if (mode == AnimationMode.ClipSpeed) PlayState(idleState, 0f);
        }
    }

    private void Update()
    {
        if (minigame == null || runner == null) return;

        UpdateAnimation();

        if (minigame.finished) return;

        float moveAmount = minigame.CurrentSpeed * Time.deltaTime;
        if (moveAmount <= 0f) return;

        Vector3 direction = runner.TransformDirection(moveDirection.normalized);
        runner.position += direction * moveAmount;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        if (mode == AnimationMode.SpeedParameter)
        {
            animator.SetFloat(speedParameterName, minigame.CurrentSpeed);
            return;
        }

        float speed = minigame.CurrentSpeed;
        bool moving = !minigame.finished && speed > stopSpeed;

        if (moving != wasMoving)
        {
            wasMoving = moving;
            PlayState(moving ? runState : idleState, crossFadeTime);
        }

        float target = 1f;
        if (moving)
            target = Mathf.Lerp(minAnimSpeed, maxAnimSpeed, Mathf.Clamp01(speed / Mathf.Max(0.01f, fullAnimAtSpeed)));
        animator.speed = Mathf.SmoothDamp(animator.speed, target, ref animSpeedVel, Mathf.Max(0.001f, animSpeedSmoothing));
    }

    private void PlayState(string state, float fade)
    {
        if (animator == null || string.IsNullOrEmpty(state)) return;
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash)) return;
        if (fade > 0f) animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
        else animator.Play(hash, 0, 0f);
    }

    private void OnDisable()
    {
        if (animator != null) animator.speed = 1f;
    }
}