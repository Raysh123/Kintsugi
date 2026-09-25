using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic back-and-forth/loop patrol for any NPC: walks between a list of points in order,
/// idling at each for a randomized duration before moving to the next, then loops back to
/// the start. Movement is plain transform interpolation (Vector3.MoveTowards) rather than
/// NavMesh or physics — fine for simple point-to-point walking on open, obstacle-free paths;
/// swap MoveTowards() for a NavMeshAgent call in here if you need obstacle avoidance later.
///
/// Call SetPaused(true) to freeze the NPC exactly where it is (e.g. while a dialogue or menu
/// is open) and SetPaused(false) to resume — it picks up the same walk/idle phase it was in,
/// nothing resets. This is what SenseiInteraction calls so the sensei doesn't wander off
/// mid-conversation.
/// </summary>
public class NPCPatrol : MonoBehaviour
{
    [Tooltip("Points visited in order, then looped back to the start. Needs at least 2 to " +
             "actually patrol — with 0 or 1 the NPC just stands still (a warning is logged).")]
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("Degrees/second turning to face the direction of travel.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Idling at each point")]
    [SerializeField] private float minIdleTime = 2f;
    [SerializeField] private float maxIdleTime = 4f;

    [Tooltip("Distance at which a patrol point counts as 'reached'.")]
    [SerializeField] private float arriveThreshold = 0.1f;

    /// <summary>True while paused (e.g. mid-dialogue) — movement and idle countdowns both
    /// freeze, nothing advances until this goes false again.</summary>
    public bool IsPaused { get; private set; }

    private int currentPointIndex;

    private void Start()
    {
        if (patrolPoints.Count < 2)
        {
            Debug.LogWarning("[NPCPatrol] Fewer than 2 patrol points assigned — this NPC will " +
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
            Transform target = patrolPoints[currentPointIndex];
            if (target == null)
            {
                Debug.LogWarning($"[NPCPatrol] Patrol point index {currentPointIndex} is empty — skipping it.");
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

            // --- Idle here for a bit ---
            float idleDuration = Random.Range(minIdleTime, maxIdleTime);
            float idledFor = 0f;
            while (idledFor < idleDuration)
            {
                if (!IsPaused) idledFor += Time.deltaTime;
                yield return null;
            }

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
