using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the dojo location end-to-end: on the player's very first arrival here (via the
/// travel map, through ITravelArrivalHandler.OnArrival - see TravelMapController), it locks
/// player movement, plays the one-time join dialogue, then unlocks whatever represents the
/// day-to-day training loop (e.g. FightTrigger). On every arrival after that, it's a no-op
/// (OnArrival just logs the return visit) - the sensei's own SenseiInteraction component
/// handles Talk/Ask-to-Fight from then on.
///
/// This is fully automatic and requires no player collider: TravelMapController calls
/// OnArrival() itself right after teleporting the player in, the same way it already does
/// for ParkActivity. Assign this location's Arrival Handler field (in TravelMapController)
/// to whatever object carries this component.
///
/// IMPORTANT: "have I joined" is tracked via PersistentGameFlags, NOT via StoryPhase.
/// StoryManager.OnDayChanged advances StoryPhase to NewDojoJoin automatically once a
/// calculated calendar date is reached, regardless of whether the player has actually been
/// to the dojo - so CurrentPhase >= NewDojoJoin can become true well before a real arrival.
/// PersistentGameFlags survives scene reloads (e.g. going into the fight scene and back via
/// FightSessionManager) the same way StoryPhase does, so it's a safe substitute here.
/// </summary>
public class DojoArrivalHandler : MonoBehaviour, ITravelArrivalHandler
{
    private const string JoinedFlag = "DojoJoined";

    [Header("First Arrival (automatic, fires once)")]
    [Tooltip("Advanced to (if not already reached) once the join dialogue finishes. Purely " +
             "for keeping StoryPhase's own bookkeeping consistent - actual join tracking uses " +
             "PersistentGameFlags, see class comment above.")]
    [SerializeField] private StoryPhase targetPhase = StoryPhase.NewDojoJoin;
    [Tooltip("Plays automatically on first arrival, before anything unlocks. Player movement " +
             "is locked for its duration.")]
    [SerializeField] private ConversationRunner joinDialogue;
    [Tooltip("Locked for the duration of the join dialogue, then restored. Not needed for " +
             "return visits.")]
    [SerializeField] private PlayerMovement player;

    [Header("Day-To-Day Loop Unlock")]
    [Tooltip("Objects activated once the player has joined - e.g. FightTrigger for " +
             "sparring, any dojo NPCs that should only appear once you've joined. " +
             "Re-activated automatically on every scene load once already joined, " +
             "since this object (and everything in this list) gets recreated fresh " +
             "each time the RPG scene reloads.")]
    [SerializeField] private List<GameObject> unlockOnFirstArrival = new List<GameObject>();

    [Header("Appearance")]
    [Tooltip("Switched to \"Dojo\" on every arrival here (join or return visit), and back to " +
             "\"Normal\" by ParkActivity/a Home arrival handler elsewhere.")]
    [SerializeField] private PlayerAppearanceController appearance;
    [SerializeField] private string dojoModelId = "Dojo";

    private bool joinSequenceRunning;

    private void Start()
    {
        // If already joined, this Start() is happening because the RPG scene just reloaded
        // (e.g. returning from a fight), not because this is a genuine first-ever arrival.
        // Re-activate the unlock list immediately so it doesn't fall back to its default
        // (inactive) state, and restore the dojo appearance in case the player object itself
        // persisted through the reload with whatever model it last had active.
        if (AlreadyJoined())
        {
            Debug.Log("[DojoArrivalHandler] Scene (re)loaded with dojo already joined - re-activating day-to-day loop.");
            ActivateUnlockList();
            if (appearance != null) appearance.SetModel(dojoModelId);
        }
    }

    private bool AlreadyJoined()
    {
        return PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(JoinedFlag);
    }

    /// <summary>
    /// Called automatically by TravelMapController right after the player is teleported here
    /// (see ITravelArrivalHandler). On a genuine first arrival, runs the join dialogue and
    /// completes the join. On any later arrival, does nothing beyond logging.
    /// </summary>
    public void OnArrival()
    {
        // Model swap happens regardless of joined state - a return visit still wears the
        // dojo outfit, it just skips the one-time join dialogue below.
        if (appearance != null) appearance.SetModel(dojoModelId);

        if (AlreadyJoined())
        {
            Debug.Log("[DojoArrivalHandler] Returning visit - day-to-day loop already unlocked.");
            return;
        }

        if (joinSequenceRunning)
        {
            Debug.LogWarning("[DojoArrivalHandler] OnArrival fired again while the join sequence " +
                              "was already running - ignoring the duplicate call.");
            return;
        }

        StartCoroutine(RunJoinSequence());
    }

    private IEnumerator RunJoinSequence()
    {
        joinSequenceRunning = true;
        if (player != null) player.movementLocked = true;

        if (joinDialogue != null)
        {
            joinDialogue.Show();
            yield return new WaitUntil(() => !joinDialogue.IsRunning);
        }
        else
        {
            Debug.LogWarning("[DojoArrivalHandler] Join Dialogue not assigned - skipping straight to completing the join.");
        }

        Debug.Log("[DojoArrivalHandler] Join dialogue finished - marking dojo as joined.");
        if (PersistentGameFlags.instance != null)
        {
            PersistentGameFlags.instance.SetFlag(JoinedFlag);
        }
        else
        {
            Debug.LogWarning("[DojoArrivalHandler] PersistentGameFlags.instance is null - join state won't persist across a scene reload!");
        }

        // Keeps StoryPhase in sync in case anything else still reads it directly - harmless
        // no-op via TryAdvanceToPhase's own guard if the calendar already advanced it.
        if (StoryManager.instance != null)
            StoryManager.instance.TryAdvanceToPhase(targetPhase);

        ActivateUnlockList();

        if (player != null) player.movementLocked = false;
        joinSequenceRunning = false;
    }

    private void ActivateUnlockList()
    {
        foreach (GameObject obj in unlockOnFirstArrival)
        {
            if (obj != null) obj.SetActive(true);
        }

        Debug.Log($"[DojoArrivalHandler] Day-to-day loop active ({unlockOnFirstArrival.Count} object(s)).");
    }
}