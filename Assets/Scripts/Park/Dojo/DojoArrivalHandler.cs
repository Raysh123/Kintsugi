using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles "you've now joined the dojo" bookkeeping: advances StoryPhase and unlocks
/// whatever represents the day-to-day training loop (e.g. FightTrigger).
///
/// This does NOT play any dialogue itself — the "go to the dojo" beat lives in your
/// existing player-dialogue system (the same one used for other story-day lines).
/// Call CompleteFirstArrival() directly once that dialogue finishes and the player
/// has been teleported to the dojo.
///
/// Uses StoryPhase (via the already-persistent StoryManager) as the ONLY source of
/// truth for "have I joined yet" — no separate local flag. This matters because this
/// GameObject lives in the RPG scene and gets destroyed/recreated every time that
/// scene reloads (e.g. going into the fight scene and back via FightSessionManager),
/// so any state stored in a plain field here would be lost on every such reload.
/// Instead, on Start() it re-checks StoryPhase and re-activates the unlock list if
/// the player had already joined before this reload happened.
/// </summary>
public class DojoArrivalHandler : MonoBehaviour, ITravelArrivalHandler
{
    [Tooltip("Phase to advance to on first arrival. Also used to detect whether the " +
             "player has already joined, via StoryManager.instance.CurrentPhase.")]
    [SerializeField] private StoryPhase targetPhase = StoryPhase.NewDojoJoin;

    [Header("Day-To-Day Loop Unlock")]
    [Tooltip("Objects activated once the player has joined — e.g. FightTrigger for " +
             "sparring, any dojo NPCs that should only appear once you've joined. " +
             "Re-activated automatically on every scene load once already joined, " +
             "since this object (and everything in this list) gets recreated fresh " +
             "each time the RPG scene reloads.")]
    [SerializeField] private List<GameObject> unlockOnFirstArrival = new List<GameObject>();

    private void Start()
    {
        // If the story is already past the join point, this Start() is happening
        // because the RPG scene just reloaded (e.g. returning from a fight), not
        // because this is a genuine first-ever arrival. Re-activate the unlock list
        // immediately so it doesn't fall back to its default (inactive) state.
        if (AlreadyJoined())
        {
            Debug.Log("[DojoArrivalHandler] Scene (re)loaded with dojo already joined — re-activating day-to-day loop.");
            ActivateUnlockList();
        }
    }

    private bool AlreadyJoined()
    {
        return StoryManager.instance != null && StoryManager.instance.CurrentPhase >= targetPhase;
    }

    /// <summary>
    /// Call this once the player is at the dojo and the "go to the dojo" story beat
    /// has played out (via your existing player-dialogue system). Advances StoryPhase
    /// and unlocks the day-to-day loop. Safe to call more than once, and safe to call
    /// on a reloaded scene where the phase is already advanced — does nothing further
    /// in that case beyond making sure the unlock list is active.
    /// </summary>
    public void CompleteFirstArrival()
    {
        if (AlreadyJoined())
        {
            Debug.LogWarning("[DojoArrivalHandler] CompleteFirstArrival called but the dojo is " +
                              "already joined — not re-advancing the phase.");
            ActivateUnlockList();
            return;
        }

        if (StoryManager.instance != null)
        {
            Debug.Log($"[DojoArrivalHandler] Advancing story to {targetPhase}.");
            StoryManager.instance.TryAdvanceToPhase(targetPhase);
        }
        else
        {
            Debug.LogWarning("[DojoArrivalHandler] StoryManager.instance is null — phase not advanced.");
        }

        ActivateUnlockList();
    }

    private void ActivateUnlockList()
    {
        foreach (GameObject obj in unlockOnFirstArrival)
        {
            if (obj != null) obj.SetActive(true);
        }

        Debug.Log($"[DojoArrivalHandler] Day-to-day loop active ({unlockOnFirstArrival.Count} object(s)).");
    }

    /// <summary>
    /// Fired when the player selects the dojo from the travel map. Only meaningful
    /// on return visits, since the first visit is expected to be a scripted teleport
    /// via CompleteFirstArrival() rather than a map selection.
    /// </summary>
    public void OnArrival()
    {
        if (!AlreadyJoined())
        {
            Debug.LogWarning("[DojoArrivalHandler] OnArrival fired before joining — the dojo " +
                              "shouldn't be selectable on the map yet at this point.");
            return;
        }

        Debug.Log("[DojoArrivalHandler] Returning visit via map — day-to-day loop already unlocked.");
    }
}