using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic back-and-forth/loop patrol for any NPC: walks between a list of points in order,
/// then loops back to the start. Movement is plain transform interpolation
/// (Vector3.MoveTowards) rather than NavMesh or physics - fine for simple point-to-point
/// walking on open, obstacle-free paths; swap MoveTowards() for a NavMeshAgent call in here
/// if you need obstacle avoidance later.
///
/// Each point independently chooses whether the NPC idles there (Wait Here) or just passes
/// straight through it as a plain waypoint - useful for a corner point placed only to route
/// around an obstacle, where you don't want the NPC stopping to idle mid-turn.
///
/// Call SetPaused(true) to freeze the NPC exactly where it is (e.g. while a dialogue or menu
/// is open) and SetPaused(false) to resume - it picks up the same walk/idle phase it was in,
/// nothing resets. This is what SenseiInteraction calls so the sensei doesn't wander off
/// mid-conversation.
/// </summary>
public class NPCPatrol : MonoBehaviour
{
    /// <summary>
    /// One stop on the route. A separate class (rather than a plain List&lt;Transform&gt;) so
    /// each point can independently decide whether the NPC idles there or just walks through.
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
    }

    [Tooltip("Points visited in order, then looped back to the start. Needs at least 2 to " +
             "actually patrol - with 0 or 1 the NPC just stands still (a warning is logged). " +
             "Each point independently controls whether the NPC waits there via its own " +
             "Wait Here checkbox.")]
    [SerializeField] private List<PatrolPoint> patrolPoints = new List<PatrolPoint>();

    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("Degrees/second turning to face the direction of travel.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Idling at each point (defaults - see per-point overrides above)")]
    [SerializeField] private float minIdleTime = 2f;
    [SerializeField] private float maxIdleTime = 4f;

    [Tooltip("Distance at which a patrol point counts as 'reached'.")]
    [SerializeField] private float arriveThreshold = 0.1f;

    /// <summary>True while paused (e.g. mid-dialogue) - movement and idle countdowns both
    /// freeze, nothing advances until this goes false again.</summary>
    public bool IsPaused { get; private set; }

    private int currentPointIndex;

    private void Start()
    {
        if (patrolPoints.Count < 2)
        {
            Debug.LogWarning("[NPCPatrol] Fewer than 2 patrol points assigned - this NPC will " +
                              "stand still instead of patrolling.");
            return;
        }

        StartCoroutine(PatrolLoop());
    }

    public void SetPaused(bool paused) => IsPaused = paused;

    private IEnumerator PatrolLoop()
    {
        while (true)
        {
            PatrolPoint current = patrolPoints[currentPointIndex];
            Transform target = current.point;

            if (target == null)
            {
                Debug.LogWarning($"[NPCPatrol] Patrol point index {currentPointIndex} is empty - skipping it.");
                currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
                yield return null;
                continue;
            }

            // --- Walk to this point ---
            while (Vector3.Distance(transform.position, target.position) > arriveThreshold)
            {
                if (!IsPaused) MoveTowards(target.position);
                yield return null;
            }

            // --- Idle here, only if this point wants it ---
            if (current.waitHere)
            {
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

            currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
        }
    }

    private void MoveTowards(Vector3 targetPosition)
    {
        Vector3 current = transform.position;
        transform.position = Vector3.MoveTowards(current, targetPosition, moveSpeed * Time.deltaTime);

        Vector3 direction = targetPosition - current;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}