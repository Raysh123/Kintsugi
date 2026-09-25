using System.Collections;
using UnityEngine;

/// <summary>
/// Put this on the sensei NPC. Replaces the old DojoJoinInteraction — the sensei is now a
/// persistent NPC (via NPCPatrol) rather than a one-shot trigger object, so the "join the
/// dojo" beat is just the first thing this component does, not its whole lifetime:
///
///   1. First arrival (automatic, fires once): the moment the player enters this object's
///      trigger collider, the sensei's join dialogue plays and DojoArrivalHandler.
///      CompleteFirstArrival() is called (advances StoryPhase AND activates the day-to-day
///      unlock list — e.g. FightTrigger — in one go). Position that collider over wherever
///      MorningDojoTransition drops the player, same as before.
///   2. From then on, walking up to the sensei (within interactRange) offers two actions:
///        - Talk (E / A / Submit): plays talkDialogue, a general chat line.
///        - Ask to Fight (F / B): opens fightSelectionMenu, which lists opponents to
///          choose from and hands off to FightSessionManager.BeginFight on selection.
///
/// Whenever a dialogue or the fight menu is open (including the one-time join dialogue),
/// the sensei's NPCPatrol is paused (he stops and stands still — doesn't move mid-
/// conversation) and the player's movement is locked, then both are restored once it
/// closes. This is the same player.movementLocked pattern used everywhere else dialogue
/// plays in this project (MorningDojoTransition, ParkActivity, PostFightHandler,
/// TravelMapController) — the old DojoJoinInteraction was the one place that had been
/// missing it, which is fixed here.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SenseiInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("Paused whenever a dialogue or the fight menu is open, so the sensei stands " +
             "still instead of wandering off mid-conversation.")]
    [SerializeField] private NPCPatrol patrol;

    [Header("First Arrival (automatic, fires once)")]
    [SerializeField] private DialogueRunner joinDialogue;
    [Tooltip("Advances StoryPhase and activates the day-to-day loop (FightTrigger etc.) " +
             "once the join dialogue finishes.")]
    [SerializeField] private DojoArrivalHandler dojoArrivalHandler;

    [Header("Ongoing Interaction (once joined)")]
    [Tooltip("Distance within which Talk/Ask to Fight become available.")]
    [SerializeField] private float interactRange = 3f;
    [Tooltip("Shown while the player is in range and nothing else is busy — e.g. " +
             "\"A: Talk   B: Ask to Fight\".")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private DialogueRunner talkDialogue;
    [SerializeField] private FightSelectionMenu fightSelectionMenu;

    private bool hasJoined;
    private bool isBusy; // true during any dialogue or the fight menu — blocks re-entry and input

    private void OnTriggerEnter(Collider other)
    {
        if (hasJoined) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(RunJoinSequence());
    }

    private void Update()
    {
        if (!hasJoined || isBusy)
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
        else if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton1))
            StartCoroutine(RunAskToFightSequence());
    }

    private IEnumerator RunJoinSequence()
    {
        hasJoined = true; // set immediately so this can't double-fire from an overlapping re-entry
        isBusy = true;
        BeginBusy();

        if (joinDialogue != null)
        {
            joinDialogue.Show();
            yield return new WaitUntil(() => !joinDialogue.IsRunning);
        }
        else
        {
            Debug.LogWarning("[SenseiInteraction] Join Dialogue not assigned.");
        }

        if (dojoArrivalHandler != null)
        {
            Debug.Log("[SenseiInteraction] Join dialogue finished — completing dojo arrival.");
            dojoArrivalHandler.CompleteFirstArrival();
        }
        else
        {
            Debug.LogWarning("[SenseiInteraction] Dojo Arrival Handler not assigned — phase not " +
                              "advanced and the day-to-day loop (FightTrigger etc.) won't unlock.");
        }

        EndBusy();
        isBusy = false;
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
        // us, which also destroys this coroutine along with everything else — harmless).
        yield return new WaitUntil(() => !fightSelectionMenu.IsOpen);

        EndBusy();
        isBusy = false;
    }

    /// <summary>Pauses patrol and locks player movement — shared by all three sequences
    /// above. Note the fight menu locks movement itself too (same as TravelMapController);
    /// locking it here as well is harmless, just belt-and-braces for the moment between
    /// Open() being called and the menu's own Update() taking over.</summary>
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
