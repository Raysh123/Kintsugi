using UnityEngine;

/// <summary>
/// Put this on a trigger collider placed at the end of the running track.
/// Ends the minigame the moment the runner reaches it, instead of the old
/// fixed-lap-distance check.
/// </summary>
public class RunningFinishLine : MonoBehaviour
{
    [SerializeField] private RunningTrackMinigame minigame;
    [Tooltip("The RunnerMover moving the runner � disabled immediately on contact so it can't " +
             "keep advancing for even a frame after the finish line is touched.")]
    [SerializeField] private RunnerMover runnerMover;
    [Tooltip("Tag on the runner (e.g. the runningActivityModel) that triggers the finish.")]
    [SerializeField] private string runnerTag = "Runner";

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[RunningFinishLine] Something entered the trigger: '{other.gameObject.name}' (tag: '{other.tag}'). Expecting tag '{runnerTag}'.");

        if (minigame == null)
        {
            Debug.LogWarning("[RunningFinishLine] Minigame not assigned.");
            return;
        }

        if (!other.CompareTag(runnerTag))
        {
            Debug.LogWarning($"[RunningFinishLine] Tag mismatch � ignoring '{other.gameObject.name}'.");
            return;
        }

        Debug.Log("[RunningFinishLine] Runner reached the finish line. Stopping movement and calling CompleteRun().");

        // Stop the runner dead immediately � don't wait for RunnerMover's own
        // next-frame finished check, and kill any physics momentum it might have.
        if (runnerMover != null) runnerMover.enabled = false;

        Rigidbody runnerRb = other.attachedRigidbody;
        if (runnerRb != null)
        {
            // NOTE: if you're on Unity 6+, Rigidbody.velocity is obsolete in favor of
            // .linearVelocity � swap this line if your editor warns/errors about it.
            runnerRb.linearVelocity = Vector3.zero;
            runnerRb.angularVelocity = Vector3.zero;
        }

        minigame.CompleteRun();
    }
}