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

    private bool hasVisitedBefore;
    private bool isRunning;

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

        if (!hasVisitedBefore && friendDialogue != null)
        {
            friendDialogue.Show();
            yield return new WaitUntil(() => !friendDialogue.IsRunning);
        }

        hasVisitedBefore = true;

        if (CalendarManager.instance != null)
            CalendarManager.instance.AdvanceTime(quadrantTicks);

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