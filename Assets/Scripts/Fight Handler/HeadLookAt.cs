using UnityEngine;

/// <summary>
/// Turns a fighter's HEAD toward the other fighter, but only while the fight is actually running
/// (not during the intro, after a point/warning, or at the end of the match). The head eases back to
/// the animation when the fight stops.
///
/// Setup: add this to each fighter (the player model and the enemy model) and drag the HEAD bone into
/// "Head". Everything else is found automatically. It works in LateUpdate, so it is applied on top of
/// the Animator's pose and doesn't touch your animations.
///
/// It is axis-independent: it measures where the opponent is relative to the BODY's facing and tilts the
/// head by that amount, so you don't need to know which way the head bone's axes point.
/// </summary>
public class HeadLookAt : MonoBehaviour
{
    [Tooltip("The head bone to rotate.")]
    [SerializeField] private Transform head;
    [Tooltip("Who to look at. Blank = found automatically (the player looks at the enemy and the enemy at the player).")]
    [SerializeField] private Transform target;
    [Tooltip("The part of the body that defines 'straight ahead' (the model root). Blank = the Animator's object.")]
    [SerializeField] private Transform body;
    [Tooltip("How high above the target's pivot to look (roughly its head height).")]
    [SerializeField] private float targetHeight = 1.5f;

    [Header("Limits")]
    [Tooltip("The head won't turn more than this far left/right of the body's facing (degrees).")]
    [SerializeField] private float maxYaw = 60f;
    [Tooltip("The head won't tilt more than this far up/down (degrees).")]
    [SerializeField] private float maxPitch = 20f;

    [Header("Blending")]
    [Range(0f, 1f)] [SerializeField] private float strength = 1f;
    [Tooltip("Seconds to ease in / out when the fight starts or stops.")]
    [SerializeField] private float blendTime = 0.25f;
    [Tooltip("Seconds the head takes to follow the opponent as they move.")]
    [SerializeField] private float followSmoothTime = 0.08f;
    [Tooltip("Turn the head off during the swap move.")]
    [SerializeField] private bool pauseDuringSwap = true;

    [Header("References (auto-found)")]
    [SerializeField] private MatchTimer matchTimer;

    private float weight;
    private float weightVel;
    private Vector3 smoothVel;
    private Vector3 lookPoint;
    private bool havePoint;

    private void Start()
    {
        if (matchTimer == null) matchTimer = FindFirstObjectByType<MatchTimer>();
        if (body == null)
        {
            Animator a = GetComponentInParent<Animator>();
            body = a != null ? a.transform : transform;
        }
        if (head == null)
            Debug.LogWarning("[HeadLookAt] No Head bone assigned on " + name + ".");
        if (target == null) FindTarget();
    }

    private void FindTarget()
    {
        bool isPlayer = GetComponentInParent<PlayerController>() != null;
        if (isPlayer)
        {
            var es = FindFirstObjectByType<EnemyStateMachine>();
            if (es != null) target = es.transform;
        }
        else
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
    }

    private bool FightRunning()
    {
        if (matchTimer == null) return true;
        if (matchTimer.matchStopped || matchTimer.matchOver) return false;
        if (pauseDuringSwap && PlayerSwap.Swapping) return false;
        return true;
    }

    private void LateUpdate()
    {
        if (head == null) return;
        if (target == null) { FindTarget(); if (target == null) return; }

        float goal = FightRunning() ? strength : 0f;
        weight = Mathf.SmoothDamp(weight, goal, ref weightVel, Mathf.Max(0.01f, blendTime), Mathf.Infinity, Time.unscaledDeltaTime);
        if (weight < 0.001f) { havePoint = false; return; }

        Vector3 want = target.position + Vector3.up * targetHeight;
        if (!havePoint) { lookPoint = want; havePoint = true; }
        else lookPoint = Vector3.SmoothDamp(lookPoint, want, ref smoothVel, Mathf.Max(0.001f, followSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);

        Vector3 dir = lookPoint - head.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        // Direction to the target in the body's own space -> yaw / pitch, clamped.
        Vector3 local = body.InverseTransformDirection(dir.normalized);
        float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        // Rotate the head by that offset (done in the body's space so the head's own axes don't matter).
        Quaternion localOffset = Quaternion.Euler(-pitch, yaw, 0f);
        Quaternion worldOffset = body.rotation * localOffset * Quaternion.Inverse(body.rotation);
        head.rotation = Quaternion.Slerp(head.rotation, worldOffset * head.rotation, weight);
    }
}
