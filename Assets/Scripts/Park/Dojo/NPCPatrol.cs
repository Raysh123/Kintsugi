using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic patrol for any NPC (the sensei, the cat...): walks between a list of points in order,
/// then loops back to the start. Movement is plain transform interpolation
/// (Vector3.MoveTowards) rather than NavMesh or physics - fine for simple point-to-point
/// walking on open, obstacle-free paths; swap MoveTowards() for a NavMeshAgent call in here
/// if you need obstacle avoidance later.
///
/// Each point independently chooses:
///   - Wait Here: idle there for a randomized time, or just pass straight through as a plain waypoint
///     (useful for a corner point placed only to route around an obstacle).
///   - Jump To Next: after arriving (and idling, if Wait Here is on) the NPC JUMPS to the next point in an arc
///     instead of walking - the jump animation plays for the whole hop. That's the cat's "jump point".
///
/// Animation (optional): give it the NPC's Animator and the names of the Idle / Walk / Jump states. Idle plays
/// while waiting at a point and while paused; Walk plays while moving; Jump plays during a jump.
///
/// Call SetPaused(true) to freeze the NPC exactly where it is (e.g. while a dialogue or menu is open, or the player
/// is playing with the cat) and SetPaused(false) to resume - it picks up the same walk/idle phase it was in,
/// nothing resets. A jump that has already started always finishes first, so the NPC is never frozen mid-air.
/// </summary>
public class NPCPatrol : MonoBehaviour
{
    /// <summary>
    /// One stop on the route. A separate class (rather than a plain List&lt;Transform&gt;) so
    /// each point can independently decide whether the NPC idles there, passes through, or jumps onward.
    /// </summary>
    [System.Serializable]
    public class PatrolPoint
    {
        public Transform point;

        [Tooltip("If true, the NPC idles here for a randomized duration (Min/Max Idle Time " +
                 "below, or this point's own overrides if set) before moving on. If false, " +
                 "this is just a waypoint - the NPC passes through with no pause and " +
                 "immediately continues to the next point.")]
        public bool waitHere = true;

        [Tooltip("Overrides Min Idle Time below for this point specifically. Leave at 0 to use " +
                 "the shared default. Only relevant if Wait Here is checked.")]
        public float minIdleTimeOverride = 0f;

        [Tooltip("Overrides Max Idle Time below for this point specifically. Leave at 0 to use " +
                 "the shared default. Only relevant if Wait Here is checked.")]
        public float maxIdleTimeOverride = 0f;

        [Tooltip("Jump point: instead of walking to the NEXT point, the NPC jumps there in an arc (the jump " +
                 "animation plays for the hop). Happens after any idling at this point.")]
        public bool jumpToNext = false;

        [Tooltip("Overrides the shared Jump Height below for the jump FROM this point. Leave at 0 to use the shared " +
                 "default. Only relevant if Jump To Next is checked. Useful when jumping up or down to a point at " +
                 "a different height - the arc rises this far above the straight line between the two points.")]
        public float jumpHeightOverride = 0f;
    }

    [Tooltip("Points visited in order, then looped back to the start. Needs at least 2 to " +
             "actually patrol - with 0 or 1 the NPC just stands still. " +
             "Each point independently controls whether the NPC waits there and whether it jumps onward.")]
    [SerializeField] private List<PatrolPoint> patrolPoints = new List<PatrolPoint>();

    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("Degrees/second turning to face the direction of travel.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Idling at each point (defaults - see per-point overrides above)")]
    [SerializeField] private float minIdleTime = 2f;
    [SerializeField] private float maxIdleTime = 4f;

    [Tooltip("Distance at which a patrol point counts as 'reached'.")]
    [SerializeField] private float arriveThreshold = 0.1f;

    [Header("Jumping (points with Jump To Next)")]
    [Tooltip("How high above the straight line between the two points the NPC arcs. Each point can override this.")]
    [SerializeField] private float jumpHeight = 0.8f;
    [Tooltip("Horizontal speed of a jump, in units/second. Lower = a longer, floatier hop.")]
    [SerializeField] private float jumpSpeed = 3f;

    [Header("Animation (optional)")]
    [Tooltip("The NPC's Animator. If empty, one on this object or its children is used.")]
    [SerializeField] private Animator animator;
    [Tooltip("Animator state played while idling at a point and while paused. Blank = never switch to it.")]
    [SerializeField] private string idleState = "Idle";
    [Tooltip("Animator state played while walking between points.")]
    [SerializeField] private string walkState = "Walk";
    [Tooltip("Animator state played for the whole length of a jump.")]
    [SerializeField] private string jumpState = "Jump";
    [Tooltip("Seconds to blend between animation states.")]
    [SerializeField] private float crossFadeTime = 0.15f;

    /// <summary>True while paused (e.g. mid-dialogue) - movement and idle countdowns both
    /// freeze, nothing advances until this goes false again.</summary>
    public bool IsPaused { get; private set; }

    private int currentPointIndex;
    private string currentAnim;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (patrolPoints.Count < 2) return;

        StartCoroutine(PatrolLoop());
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (paused) PlayAnim(idleState);
    }

    private IEnumerator PatrolLoop()
    {
        while (true)
        {
            PatrolPoint current = patrolPoints[currentPointIndex];
            Transform target = current.point;

            if (target == null)
            {
                currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
                yield return null;
                continue;
            }

            // --- Walk to this point ---
            while (Vector3.Distance(transform.position, target.position) > arriveThreshold)
            {
                if (IsPaused)
                {
                    PlayAnim(idleState);
                }
                else
                {
                    PlayAnim(walkState);
                    MoveTowards(target.position);
                }
                yield return null;
            }

            // --- Idle here, only if this point wants it ---
            if (current.waitHere)
            {
                PlayAnim(idleState);

                float min = current.minIdleTimeOverride > 0f ? current.minIdleTimeOverride : minIdleTime;
                float max = current.maxIdleTimeOverride > 0f ? current.maxIdleTimeOverride : maxIdleTime;

                float idleDuration = Random.Range(min, max);
                float idledFor = 0f;
                while (idledFor < idleDuration)
                {
                    if (!IsPaused) idledFor += Time.deltaTime;
                    yield return null;
                }
            }
            // else: pure waypoint - fall straight through to the next point, no idle at all.

            // Don't start a jump while paused - wait until the player lets go, so the NPC is never frozen mid-air.
            while (IsPaused) yield return null;

            int nextIndex = (currentPointIndex + 1) % patrolPoints.Count;

            if (current.jumpToNext && patrolPoints[nextIndex].point != null)
            {
                float height = current.jumpHeightOverride > 0f ? current.jumpHeightOverride : jumpHeight;
                yield return JumpTo(patrolPoints[nextIndex].point.position, height);
            }

            currentPointIndex = nextIndex;
        }
    }

    /// <summary>Arcs from the current position to the destination while the jump animation plays. Ignores pausing,
    /// so a jump that has started always lands.</summary>
    private IEnumerator JumpTo(Vector3 destination, float height)
    {
        Vector3 start = transform.position;
        Vector3 flat = destination - start;
        flat.y = 0f;

        float duration = Mathf.Max(0.1f, flat.magnitude / Mathf.Max(0.01f, jumpSpeed));

        PlayAnim(jumpState);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float clamped = Mathf.Clamp01(t);

            Vector3 position = Vector3.Lerp(start, destination, clamped);
            position.y += height * 4f * clamped * (1f - clamped);
            transform.position = position;

            FaceDirection(flat);
            yield return null;
        }

        transform.position = destination;
    }

    private void MoveTowards(Vector3 targetPosition)
    {
        Vector3 current = transform.position;
        transform.position = Vector3.MoveTowards(current, targetPosition, moveSpeed * Time.deltaTime);

        FaceDirection(targetPosition - current);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    /// <summary>Crossfades to the given Animator state, only when it actually changes. Does nothing if there's no
    /// Animator, the name is blank, or the state doesn't exist.</summary>
    private void PlayAnim(string state)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
        if (string.IsNullOrEmpty(state) || state == currentAnim) return;

        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash)) return;

        currentAnim = state;
        animator.CrossFadeInFixedTime(hash, crossFadeTime, 0, 0f);
    }
}