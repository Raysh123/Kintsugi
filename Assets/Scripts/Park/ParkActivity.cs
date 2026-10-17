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

    [Header("Run Intro (first time only)")]
    [Tooltip("Panel explaining that park activities only restore the motivation meter. Shown once, before the run can start. " +
             "Leave empty to skip it.")]
    [SerializeField] private GameObject runIntroPanel;
    [Tooltip("Flag id remembered in PersistentGameFlags so the intro only ever shows the first time.")]
    [SerializeField] private string runIntroFlagId = "ParkRunIntroShown";

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
    private bool runIntroShownThisSession;

    private bool HasVisitedBefore =>
        PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(firstVisitFlagId);

    private bool HasSeenRunIntro =>
        runIntroShownThisSession ||
        (PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(runIntroFlagId));

    private void MarkVisited()
    {
        if (PersistentGameFlags.instance != null)
            PersistentGameFlags.instance.SetFlag(firstVisitFlagId);
    }

    private void MarkRunIntroSeen()
    {
        runIntroShownThisSession = true;
        if (PersistentGameFlags.instance != null)
            PersistentGameFlags.instance.SetFlag(runIntroFlagId);
    }

    public void OnArrival()
    {
        if (isRunning) return;
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        isRunning = true;

        // Lock normal movement, hide the regular player mesh - the running/bench
        // models are separate pre-built objects, same pattern as every other activity.
        if (player != null) player.movementLocked = true;
        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (defaultCamera != null) defaultCamera.SetActive(false);

        // --- Running phase ---
        if (runnerMover != null) runnerMover.ResetPosition(runStartPosition);

        if (runningCamera != null) runningCamera.SetActive(true);
        if (runningActivityModel != null) runningActivityModel.SetActive(true);

        // First time only: explain that park activities only restore motivation. The minigame hasn't been
        // started yet, so mashing can't begin (or the timer run) until the panel is dismissed.
        if (runIntroPanel != null && !HasSeenRunIntro)
        {
            runIntroPanel.SetActive(true);
            yield return null; // let the key press that opened the activity pass
            yield return new WaitUntil(() =>
                Input.GetKeyDown(KeyCode.E) ||
                Input.GetKeyDown(KeyCode.JoystickButton0) ||
                Input.GetButtonDown("Submit"));
            runIntroPanel.SetActive(false);
            MarkRunIntroSeen();
            yield return null; // don't let the dismiss press count as the first mash
        }

        if (minigame != null)
        {
            minigame.ResetMinigame();
            yield return new WaitUntil(() => minigame.finished);
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
        }

        if (benchActivityModel != null) benchActivityModel.SetActive(true);

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