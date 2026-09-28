using System.Collections;
using UnityEngine;

public class ParkActivity : MonoBehaviour, ITravelArrivalHandler
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private GameObject playerMeshChild;
    [Tooltip("The normal gameplay camera (e.g. the top-down follow camera). This gets " +
             "disabled whenever a park activity camera is active, and re-enabled once the " +
             "park sequence ends — otherwise both cameras render at once and it looks like " +
             "nothing switched.")]
    [SerializeField] private GameObject defaultCamera;

    [Header("Running Phase")]
    [SerializeField] private RunningTrackMinigame minigame;
    [SerializeField] private RunnerMover runnerMover;
    [SerializeField] private GameObject runningCamera;
    [SerializeField] private GameObject runningActivityModel;
    [SerializeField] private Transform runStartPosition;

    [Header("Bench / Serene Area Phase")]
    [SerializeField] private Transform benchDestination;
    [SerializeField] private GameObject benchCamera;
    [SerializeField] private GameObject benchActivityModel;

    [Header("First Visit Only")]
    [SerializeField] private DialogueRunner friendDialogue;

    [Header("Leaving the Park")]
    [SerializeField] private GameObject openMapPrompt;
    [SerializeField] private TravelMapController travelMapController;

    [Header("Time Cost")]
    [SerializeField] private int quadrantTicks = 4;

    [Tooltip("Flag id used with PersistentGameFlags to remember 'has the friend dialogue " +
             "played' across scene reloads (e.g. going into the fight scene and back). " +
             "ParkActivity itself gets destroyed/recreated on a scene reload, so this can't " +
             "just be a plain local bool — it would reset to false every time.")]
    [SerializeField] private string firstVisitFlagId = "ParkFriendDialogueShown";

    private bool isRunning;

    private bool HasVisitedBefore =>
        PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(firstVisitFlagId);

    private void MarkVisited()
    {
        if (PersistentGameFlags.instance != null)
            PersistentGameFlags.instance.SetFlag(firstVisitFlagId);
        else
            Debug.LogWarning("[ParkActivity] PersistentGameFlags.instance is null — first-visit " +
                              "state won't survive a scene reload. Make sure a PersistentGameFlags " +
                              "object exists in your startup scene.");
    }

    public void OnArrival()
    {
        Debug.Log("[ParkActivity] OnArrival called.");
        if (isRunning)
        {
            Debug.LogWarning("[ParkActivity] OnArrival called again while already running — ignored.");
            return;
        }
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        Debug.Log("[ParkActivity] RunSequence started.");
        isRunning = true;

        // Lock normal movement, hide the regular player mesh — the running/bench
        // models are separate pre-built objects, same pattern as every other activity.
        if (player != null) player.movementLocked = true;
        else Debug.LogWarning("[ParkActivity] Player reference not assigned.");

        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        else Debug.LogWarning("[ParkActivity] Player Mesh Child not assigned.");

        if (defaultCamera != null)
        {
            defaultCamera.SetActive(false);
            Debug.Log("[ParkActivity] Default Camera deactivated.");
        }
        else Debug.LogWarning("[ParkActivity] Default Camera not assigned — the normal gameplay " +
                               "camera will keep rendering alongside the park cameras.");

        // --- Running phase ---
        if (runnerMover != null) runnerMover.ResetPosition(runStartPosition);
        else Debug.LogWarning("[ParkActivity] Runner Mover not assigned.");

        if (minigame != null) minigame.ResetMinigame();
        else Debug.LogWarning("[ParkActivity] Minigame not assigned.");

        if (runningCamera != null)
        {
            runningCamera.SetActive(true);
            Debug.Log("[ParkActivity] Running Camera activated.");
        }
        else Debug.LogWarning("[ParkActivity] Running Camera not assigned.");

        if (runningActivityModel != null)
        {
            runningActivityModel.SetActive(true);
            Debug.Log("[ParkActivity] Running Activity Model activated.");
        }
        else Debug.LogWarning("[ParkActivity] Running Activity Model not assigned.");

        if (minigame != null)
        {
            Debug.Log("[ParkActivity] Waiting for minigame.finished...");
            yield return new WaitUntil(() => minigame.finished);
            Debug.Log("[ParkActivity] minigame.finished is true — running phase complete.");
        }

        if (minigame != null && HealthAndMotivation.instance != null)
            HealthAndMotivation.instance.ModifyMotivation(minigame.motivationEarned);

        if (runningCamera != null) runningCamera.SetActive(false);
        if (runningActivityModel != null) runningActivityModel.SetActive(false);

        // --- Bench phase ---
        TeleportPlayer(benchDestination);

        if (benchCamera != null)
        {
            benchCamera.SetActive(true);
            Debug.Log("[ParkActivity] Bench Camera activated.");
        }
        else Debug.LogWarning("[ParkActivity] Bench Camera not assigned.");

        if (benchActivityModel != null) benchActivityModel.SetActive(true);
        else Debug.LogWarning("[ParkActivity] Bench Activity Model not assigned.");

        if (!HasVisitedBefore && friendDialogue != null)
        {
            friendDialogue.Show();
            yield return new WaitUntil(() => !friendDialogue.IsRunning);
        }

        MarkVisited();

        // --- Leave ---
        if (openMapPrompt != null) openMapPrompt.SetActive(true);

        yield return new WaitUntil(() =>
            Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.JoystickButton0) ||
            Input.GetButtonDown("Submit"));

        if (openMapPrompt != null) openMapPrompt.SetActive(false);

        if (benchCamera != null) benchCamera.SetActive(false);
        if (benchActivityModel != null) benchActivityModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);
        if (player != null) player.movementLocked = false;

        if (defaultCamera != null)
        {
            defaultCamera.SetActive(true);
            Debug.Log("[ParkActivity] Default Camera reactivated.");
        }

        // Time only advances NOW, after the player has actually chosen to leave - not while
        // they're still at the bench. If this crosses a phase boundary, CalendarManager's
        // OnPhaseChanged fires and TimeOfDaySkybox hard-swaps the sky; doing that mid-bench
        // would pop the sky in front of the player, which the whole no-crossfade design
        // assumes never happens. Also has to come BEFORE OpenMap() below, since the map's
        // time-of-day availability (Park: Morning/Evening only, etc.) is computed from the
        // current phase at the moment it opens - opening first would show stale availability.
        if (CalendarManager.instance != null)
            CalendarManager.instance.AdvanceTime(quadrantTicks);

        if (travelMapController != null) travelMapController.OpenMap();

        isRunning = false;
    }

    private void TeleportPlayer(Transform destination)
    {
        if (player == null || destination == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        if (controller != null) controller.enabled = true;
    }
}