using System.Collections;
using UnityEngine;

/// <summary>
/// Put this on the sensei NPC. Purely the ONGOING interaction, once the dojo has already
/// been joined - the one-time "join the dojo" dialogue no longer lives here; it now plays
/// automatically on arrival via DojoArrivalHandler.OnArrival() (see TravelMapController's
/// ITravelArrivalHandler hookup), before this NPC's day-to-day behavior is even reachable.
///
/// Walking up to the sensei (within interactRange) offers two actions:
///   - Talk (E / A / Submit): plays talkDialogue, a general chat line.
///   - Ask to Fight (Space / B / Cancel): opens fightSelectionMenu, which lists opponents to
///     choose from and hands off to FightSessionManager.BeginFight on selection.
///
/// Whenever a dialogue or the fight menu is open, the sensei's NPCPatrol is paused (he
/// stops and stands still - doesn't move mid-conversation) and the player's movement is
/// locked, then both are restored once it closes. Same player.movementLocked pattern used
/// everywhere else dialogue plays in this project (ParkActivity, PostFightHandler,
/// TravelMapController, DojoArrivalHandler's own join sequence).
/// </summary>
public class SenseiInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("Paused whenever a dialogue or the fight menu is open, so the sensei stands " +
             "still instead of wandering off mid-conversation.")]
    [SerializeField] private NPCPatrol patrol;

    [Header("Ongoing Interaction")]
    [Tooltip("Distance within which Talk/Ask to Fight become available.")]
    [SerializeField] private float interactRange = 3f;
    [Tooltip("Shown while the player is in range and nothing else is busy - e.g. " +
             "\"A: Talk   B: Ask to Fight\".")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private DialogueRunner talkDialogue;
    [SerializeField] private FightSelectionMenu fightSelectionMenu;

    private bool isBusy; // true during any dialogue or the fight menu - blocks re-entry and input

    private void Update()
    {
        if (isBusy)
        {
            if (interactPrompt != null) interactPrompt.SetActive(false);
            return;
        }

        bool inRange = player != null &&
                        Vector3.Distance(player.transform.position, transform.position) <= interactRange;

        if (interactPrompt != null) interactPrompt.SetActive(inRange);
        if (!inRange) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            StartCoroutine(RunTalkSequence());
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetButtonDown("Cancel"))
            StartCoroutine(RunAskToFightSequence());
    }

    private IEnumerator RunTalkSequence()
    {
        isBusy = true;
        BeginBusy();

        if (talkDialogue != null)
        {
            talkDialogue.Show();
            yield return new WaitUntil(() => !talkDialogue.IsRunning);
        }
        else
        {
            Debug.LogWarning("[SenseiInteraction] Talk Dialogue not assigned.");
        }

        EndBusy();
        isBusy = false;
    }

    private IEnumerator RunAskToFightSequence()
    {
        if (fightSelectionMenu == null)
        {
            Debug.LogWarning("[SenseiInteraction] Fight Selection Menu not assigned.");
            yield break;
        }

        isBusy = true;
        BeginBusy();

        fightSelectionMenu.Open();
        // Covers both ways this ends: the player cancels (menu closes, we're still in this
        // scene) or picks a fight (FightSessionManager loads the fight scene out from under
        // us, which also destroys this coroutine along with everything else - harmless).
        yield return new WaitUntil(() => !fightSelectionMenu.IsOpen);

        EndBusy();
        isBusy = false;
    }

    /// <summary>Pauses patrol and locks player movement - shared by both sequences above.
    /// Note the fight menu locks movement itself too (same as TravelMapController); locking
    /// it here as well is harmless, just belt-and-braces for the moment between Open() being
    /// called and the menu's own Update() taking over.</summary>
    private void BeginBusy()
    {
        if (patrol != null) patrol.SetPaused(true);
        if (player != null) player.movementLocked = true;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void EndBusy()
    {
        if (patrol != null) patrol.SetPaused(false);
        if (player != null) player.movementLocked = false;
    }
}