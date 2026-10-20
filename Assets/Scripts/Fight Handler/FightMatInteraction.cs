using UnityEngine;

/// <summary>
/// The fight mat. Put this on a trigger collider over the mat: while the player stands on it, the prompt shows,
/// and pressing E / A / Submit opens the FightSelectionMenu (pick an opponent, confirm to fight, Space / B to
/// cancel). The menu locks the player's movement itself while it's open.
///
/// To keep the mat unavailable until the story reaches TournamentPrep, put a StoryPhaseGate (visibleFrom
/// TournamentPrep, visibleUntil TournamentDay) on this trigger object - not on the mat's visible model, or the
/// mat would vanish too.
/// </summary>
public class FightMatInteraction : MonoBehaviour
{
    [SerializeField] private FightSelectionMenu fightSelectionMenu;
    [Tooltip("Shown while the player is on the mat and nothing else is busy - e.g. \"E: Choose a fight\".")]
    [SerializeField] private GameObject interactPrompt;

    private PlayerMovement player;
    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        player = other.GetComponentInParent<PlayerMovement>();
        playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void OnDisable()
    {
        playerInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    // The mat stays off during TournamentPrep until the player has talked to the sensei.
    private static bool MatUnlocked()
    {
        if (StoryManager.instance == null || StoryManager.instance.CurrentPhase != StoryPhase.TournamentPrep) return true;
        return PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(SenseiInteraction.FirstPrepTalkDoneFlag);
    }

    private void Update()
    {
        if (!playerInRange || fightSelectionMenu == null) return;

        if (!MatUnlocked())
        {
            if (interactPrompt != null) interactPrompt.SetActive(false);
            return;
        }

        bool free = !fightSelectionMenu.IsOpen &&
                    !ActiveDialogueTracker.AnyActive &&
                    (player == null || !player.movementLocked);

        if (interactPrompt != null) interactPrompt.SetActive(free);
        if (!free) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            fightSelectionMenu.Open();
    }
}