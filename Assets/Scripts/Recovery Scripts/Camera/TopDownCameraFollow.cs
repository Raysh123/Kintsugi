using UnityEngine;

[DisallowMultipleComponent]
public class TopDownCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Framing")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 10f, -8f);
    [SerializeField] private float pitch = 50f;
    [SerializeField] private float yaw = 0f;

    [Header("Follow Smoothing")]
    [SerializeField] private float followSmoothTime = 0.15f;
    [SerializeField] private float maxFollowSpeed = 40f;

    [Header("Look-Ahead (optional)")]
    [SerializeField] private float lookAheadDistance = 0f;
    [SerializeField] private float lookAheadSmoothTime = 0.2f;

    [Header("Dead Zone (optional)")]
    [SerializeField] private bool useDeadZone = false;
    [SerializeField] private Vector2 deadZoneSize = new Vector2(1.5f, 1.5f);

    [Header("Orbit Rotation (optional — toggle off to disable entirely)")]
    public bool enableOrbitRotation = true;

    //[Tooltip("How fast the camera smoothly rotates to the new 90 degree position after a tap. Higher = snappier.")]
    //[SerializeField] private float orbitRotationSpeed = 90f;
    [Tooltip("How smoothly the orbit rotation catches up. Higher = snappier.")]
    [SerializeField] private float orbitSmoothSpeed = 6f;

    [Tooltip("When true, camera stops following the target and stays put. " +
             "Set by Outsideview to lock the camera in place.")]
    public bool overrideFollow = false;

    private Vector3 _followVelocity;
    private Vector3 _lookAheadCurrent;
    private Vector3 _lookAheadVelocity;
    private Vector3 _lastTargetPos;

    // Orbit state — yaw is the source of truth, pitch stays fixed
    private float _currentYaw;
    private float _targetYaw;

    private void Awake()
    {
        _currentYaw = yaw;
        _targetYaw = yaw;
        transform.rotation = Quaternion.Euler(pitch, _currentYaw, 0f);

        if (target != null)
            _lastTargetPos = target.position;
    }

    private void OnValidate()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (overrideFollow) return;

        if (enableOrbitRotation)
            HandleOrbitInput();

        Vector3 desiredPos = ComputeDesiredPosition();

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref _followVelocity,
            followSmoothTime,
            maxFollowSpeed
        );

        // Smoothly interpolate to target yaw, pitch stays fixed
        _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, orbitSmoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(pitch, _currentYaw, 0f);

        _lastTargetPos = target.position;
    }

    private void HandleOrbitInput()
    {
        // Q or Left Bumper (LB) — snap 90 degrees left
        if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.JoystickButton4))
            _targetYaw -= 90f;

        // E or Right Bumper (RB) — snap 90 degrees right
        if (Input.GetKeyDown(KeyCode.Mouse1) || Input.GetKeyDown(KeyCode.JoystickButton5))
            _targetYaw += 90f;
    }

    private Vector3 ComputeDesiredPosition()
    {
        Vector3 targetPos = target.position;

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

        if (useDeadZone)
        {
            Vector3 currentLookAtPoint = transform.position - GetOrbitedOffset();
            Vector3 delta = targetPos - currentLookAtPoint;

            float clampedX = Mathf.Clamp(delta.x, -deadZoneSize.x * 0.5f, deadZoneSize.x * 0.5f);
            float clampedZ = Mathf.Clamp(delta.z, -deadZoneSize.y * 0.5f, deadZoneSize.y * 0.5f);

            Vector3 excess = delta - new Vector3(clampedX, delta.y, clampedZ);
            targetPos = currentLookAtPoint + new Vector3(clampedX, delta.y, clampedZ) + excess;
        }

        return targetPos + GetOrbitedOffset();
    }

    /// <summary>
    /// Rotates the world offset around the Y axis by the current yaw so the
    /// offset always sits correctly behind/above the player regardless of
    /// how far the camera has been orbited.
    /// </summary>
    private Vector3 GetOrbitedOffset()
    {
        return Quaternion.Euler(0f, _currentYaw, 0f) * worldOffset;
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        _followVelocity = Vector3.zero;
        _lookAheadCurrent = Vector3.zero;
        _currentYaw = _targetYaw;
        transform.position = target.position + GetOrbitedOffset();
        transform.rotation = Quaternion.Euler(pitch, _currentYaw, 0f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
            _lastTargetPos = target.position;
    }

    /// <summary>Resets orbit back to the default yaw smoothly.</summary>
    public void ResetOrbit()
    {
        _targetYaw = yaw;
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