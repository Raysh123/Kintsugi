using UnityEngine;

/// <summary>
/// Fixed-angle camera follow for top-down 3D games (Tunic / Minish Cap style).
///
/// The camera keeps a constant offset and rotation relative to the world at all
/// times. It only ever moves (never rotates) to keep the target roughly centered.
/// The player can rotate freely without affecting the camera's angle at all.
///
/// Setup:
/// 1. Attach this script to your Camera (or an empty parent holding the camera).
/// 2. Set "Target" to the player's Transform.
/// 3. Rotate the camera in the Scene view to the angle you want
///    (e.g. looking down at ~45-60 degrees), then press "Use Current Rotation"
///    in the inspector context menu, or just leave fixedRotation at its default
///    and adjust pitch/yaw with the exposed fields below.
/// </summary>
[DisallowMultipleComponent]
public class TopDownCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The transform the camera should follow (usually the player root, not a rotating model).")]
    [SerializeField] private Transform target;

    [Header("Framing")]
    [Tooltip("Offset from the target, expressed in WORLD space (not local/rotated space). " +
             "E.g. (0, 10, -8) sits the camera above and behind the origin, looking down at the target.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 10f, -8f);

    [Tooltip("Camera's fixed look angle. Pitch = degrees looking down (0 = horizontal, 90 = straight down). " +
             "Yaw = rotation around the vertical axis. This never changes at runtime.")]
    [SerializeField] private float pitch = 50f;
    [SerializeField] private float yaw = 0f;

    [Header("Follow Smoothing")]
    [Tooltip("How quickly the camera catches up to the target. Higher = snappier, lower = floatier.")]
    [SerializeField] private float followSmoothTime = 0.15f;

    [Tooltip("Optional: clamp max camera speed. Set to a large number (e.g. 1000) to effectively disable.")]
    [SerializeField] private float maxFollowSpeed = 40f;

    [Header("Look-Ahead (optional)")]
    [Tooltip("If > 0, camera leans slightly in the direction the target is MOVING (not facing) for a bit of anticipation.")]
    [SerializeField] private float lookAheadDistance = 0f;
    [SerializeField] private float lookAheadSmoothTime = 0.2f;

    [Header("Dead Zone (optional)")]
    [Tooltip("If true, the camera only moves once the target strays outside a small centered box, " +
             "rather than tracking every tiny movement. Good for reducing jitter on small idle movements.")]
    [SerializeField] private bool useDeadZone = false;
    [SerializeField] private Vector2 deadZoneSize = new Vector2(1.5f, 1.5f);

    private Vector3 _followVelocity;
    private Vector3 _lookAheadCurrent;
    private Vector3 _lookAheadVelocity;
    private Vector3 _lastTargetPos;
    private Vector3 _fixedRotationEuler;

    private void Awake()
    {
        _fixedRotationEuler = new Vector3(pitch, yaw, 0f);
        transform.rotation = Quaternion.Euler(_fixedRotationEuler);

        if (target != null)
            _lastTargetPos = target.position;
    }

    private void OnValidate()
    {
        // Keep rotation locked in the editor too, so you can preview framing live.
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = ComputeDesiredPosition();

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref _followVelocity,
            followSmoothTime,
            maxFollowSpeed
        );

        // Rotation is NEVER touched here. It stays exactly at (pitch, yaw, 0)
        // regardless of target rotation or movement.
        transform.rotation = Quaternion.Euler(_fixedRotationEuler);

        _lastTargetPos = target.position;
    }

    private Vector3 ComputeDesiredPosition()
    {
        Vector3 targetPos = target.position;

        // Optional look-ahead based on actual movement direction (not facing direction),
        // since the target can rotate independently of where it's actually moving.
        if (lookAheadDistance > 0f)
        {
            Vector3 velocity = (target.position - _lastTargetPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 desiredLookAhead = flatVelocity.sqrMagnitude > 0.01f
                ? flatVelocity.normalized * lookAheadDistance
                : Vector3.zero;

            _lookAheadCurrent = Vector3.SmoothDamp(
                _lookAheadCurrent, desiredLookAhead, ref _lookAheadVelocity, lookAheadSmoothTime);

            targetPos += _lookAheadCurrent;
        }

        // Optional dead zone: only reframe if target has drifted outside a small box
        // centered on where the camera is currently looking.
        if (useDeadZone)
        {
            Vector3 currentLookAtPoint = transform.position - worldOffset;
            Vector3 delta = targetPos - currentLookAtPoint;

            float clampedX = Mathf.Clamp(delta.x, -deadZoneSize.x * 0.5f, deadZoneSize.x * 0.5f);
            float clampedZ = Mathf.Clamp(delta.z, -deadZoneSize.y * 0.5f, deadZoneSize.y * 0.5f);

            Vector3 excess = delta - new Vector3(clampedX, delta.y, clampedZ);
            targetPos = currentLookAtPoint + new Vector3(clampedX, delta.y, clampedZ) + excess;
        }

        return targetPos + worldOffset;
    }

    /// <summary>
    /// Call this if you ever need to instantly snap the camera to the target
    /// (e.g. on level load, teleport, or respawn) without a smoothed transition.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        _followVelocity = Vector3.zero;
        _lookAheadCurrent = Vector3.zero;
        transform.position = target.position + worldOffset;
        transform.rotation = Quaternion.Euler(_fixedRotationEuler);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
            _lastTargetPos = target.position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
        Gizmos.DrawWireSphere(target.position, 0.3f);
    }
#endif
}