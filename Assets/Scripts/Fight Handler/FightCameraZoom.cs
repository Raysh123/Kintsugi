using UnityEngine;

/// <summary>
/// Fight camera that sits at the centre of the mat and zooms (and always follows the player sideways): closer when the two fighters are
/// near each other, further out when they are far apart.
///
/// Setup: add to the Main Camera (fight scene). Player and Opponent are found automatically.
/// Position the camera where you want it (centred on the mat) BEFORE pressing play - that spot is kept.
/// Works with a perspective camera (Zoom Mode: Fov or Dolly) or an orthographic one (size).
/// Not for use with Cinemachine driving the same camera - disable the brain/vcam if you use this.
/// </summary>
[RequireComponent(typeof(Camera))]
public class FightCameraZoom : MonoBehaviour
{
    public enum ZoomMode { Fov, Dolly }

    [Header("Targets (auto-found if empty)")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform enemy;

    [Header("Centre")]
    [Tooltip("Optional. If set, the camera is placed so it looks at this point's X (the mat centre) and " +
             "stays there. Empty = camera stays exactly where you put it in the scene.")]
    [SerializeField] private Transform matCentre;

    [Header("Zoom")]
    [Tooltip("Fov = change field of view (camera doesn't move). Dolly = slide the camera back/forward along " +
             "its own view direction. Ignored for orthographic cameras (size is used).")]
    [SerializeField] private ZoomMode zoomMode = ZoomMode.Fov;
    [Tooltip("Fighter distance (world units) at which the camera is fully zoomed IN.")]
    [SerializeField] private float closeDistance = 1.5f;
    [Tooltip("Fighter distance at which the camera is fully zoomed OUT.")]
    [SerializeField] private float farDistance = 9f;
    [Tooltip("Smoothing time. Higher = lazier.")]
    [SerializeField] private float smoothTime = 0.25f;

    [Header("Follow (always)")]
    [Tooltip("Camera slides sideways to follow the player at every zoom level.")]
    [SerializeField] private bool followPlayer = true;
    [Tooltip("1 = follow the PLAYER, 0 = follow the midpoint between both fighters.")]
    [Range(0f, 1f)][SerializeField] private float followPlayerWeight = 1f;
    [Tooltip("How much of the way to the follow point the camera goes (1 = centred on it).")]
    [Range(0f, 1f)][SerializeField] private float followStrength = 1f;
    [Tooltip("The camera never slides further than this from the mat centre (world units). Make it big enough " +
             "to see the mat edges; 0 = no limit.")]
    [SerializeField] private float followLimit = 15f;
    [Tooltip("Camera jumps INSTANTLY back to the mat centre when the referee's reset happens (and stays there " +
             "until the next round starts). Until then it keeps following the player, even while the match is stopped. " +
             "Hooked to the referee's On Ready To Reset automatically.")]
    [SerializeField] private bool snapToCentreOnReset = true;
    [SerializeField] private MatchTimer matchTimer;
    [SerializeField] private RefereeController referee;
    [SerializeField] private float followSmoothTime = 0.2f;

    [Header("Values: zoomed IN")]
    [SerializeField] private float closeFov = 35f;
    [SerializeField] private float closeDolly = 0f;     // metres pulled back from start (Dolly mode)
    [SerializeField] private float closeOrthoSize = 3f;

    [Header("Values: zoomed OUT")]
    [SerializeField] private float farFov = 60f;
    [SerializeField] private float farDolly = 4f;
    [SerializeField] private float farOrthoSize = 6f;

    private Camera cam;
    private Vector3 basePos;
    private float current01;   // smoothed 0 (in) .. 1 (out)
    private float vel;
    private bool sawStopped;
    private bool holdCentre = true;   // centred at the intro, and after each reset until play resumes
    private float offset, offsetVel;   // sideways slide along the camera's right axis

    private void Awake()
    {
        cam = GetComponent<Camera>();
        basePos = transform.position;
    }

    private void Start()
    {
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (enemy == null)
        {
            var es = FindFirstObjectByType<EnemyStateMachine>();
            if (es != null) enemy = es.transform;
        }

        if (matchTimer == null) matchTimer = FindFirstObjectByType<MatchTimer>();
        if (referee == null) referee = FindFirstObjectByType<RefereeController>();
        if (referee != null && snapToCentreOnReset) referee.onReadyToReset.AddListener(SnapToCentre);

        // Keep the current camera height/depth, but centre it on the mat if a centre is given.
        if (matCentre != null)
        {
            Vector3 p = transform.position;
            // Shift sideways along the camera's right axis so the centre sits in the middle of the view.
            Vector3 toCentre = matCentre.position - p;
            float side = Vector3.Dot(toCentre, transform.right);
            basePos = p + transform.right * side;
            transform.position = basePos;
        }
        current01 = Target01();
        offset = TargetOffset();
        Apply();
    }

    private void OnDestroy()
    {
        if (referee != null) referee.onReadyToReset.RemoveListener(SnapToCentre);
    }

    /// <summary>Instantly centre the camera on the mat and hold it there until the match restarts.
    /// Called automatically on the referee's reset; can also be bound to any event by hand.</summary>
    public void SnapToCentre()
    {
        holdCentre = true;
        sawStopped = matchTimer == null || matchTimer.matchStopped;
        offset = 0f;
        offsetVel = 0f;
        Apply();
    }

    private float TargetOffset()
    {
        if (!followPlayer || player == null) return 0f;
        if (holdCentre) return 0f;
        Vector3 point = player.position;
        if (enemy != null) point = Vector3.Lerp((player.position + enemy.position) * 0.5f, player.position, followPlayerWeight);
        float side = Vector3.Dot(point - basePos, transform.right);
        float o = side * followStrength;
        return followLimit > 0f ? Mathf.Clamp(o, -followLimit, followLimit) : o;
    }

    private float Target01()
    {
        if (player == null || enemy == null) return 0.5f;
        Vector3 d = player.position - enemy.position;
        d.y = 0f;
        float dist = d.magnitude;
        return Mathf.Clamp01(Mathf.InverseLerp(closeDistance, Mathf.Max(farDistance, closeDistance + 0.01f), dist));
    }

    private void LateUpdate()
    {
        current01 = Mathf.SmoothDamp(current01, Target01(), ref vel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        // Play resumed: let go of the centre hold and follow the player again.
        if (holdCentre && matchTimer != null)
        {
            if (matchTimer.matchStopped) sawStopped = true;
            else if (sawStopped) holdCentre = false;   // only after it has been stopped since the snap
        }

        if (holdCentre) { offset = 0f; offsetVel = 0f; }
        else offset = Mathf.SmoothDamp(offset, TargetOffset(), ref offsetVel, followSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        Apply();
    }

    private void Apply()
    {
        float t = current01;
        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.Lerp(closeOrthoSize, farOrthoSize, t);
            transform.position = basePos + transform.right * offset;
        }
        else if (zoomMode == ZoomMode.Fov)
        {
            cam.fieldOfView = Mathf.Lerp(closeFov, farFov, t);
            transform.position = basePos + transform.right * offset;
        }
        else
        {
            float back = Mathf.Lerp(closeDolly, farDolly, t);
            transform.position = basePos + transform.right * offset - transform.forward * back;
        }
    }
}