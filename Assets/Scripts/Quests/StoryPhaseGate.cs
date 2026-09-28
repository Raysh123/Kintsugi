using UnityEngine;

/// <summary>
/// Attach to any GameObject that should only be active during certain story
/// phases — an NPC, a door, a dojo entrance, a bed interaction trigger, etc.
/// No code needed anywhere else; it just listens to StoryManager.OnPhaseChanged.
///
/// Examples:
///   Friend NPC      — visibleFrom: FriendVisit,  visibleUntil: NewDojoJoin
///   Bed interaction — visibleFrom: BedRest,       visibleUntil: StandingRecovery
///   New dojo door   — visibleFrom: NewDojoJoin,   visibleUntil: TournamentDay
/// </summary>
public class StoryPhaseGate : MonoBehaviour
{
    [Tooltip("This object becomes active when the story reaches this phase.")]
    [SerializeField] private StoryPhase visibleFrom;
    [Tooltip("This object deactivates after this phase. Set to TournamentDay to keep it active until the end.")]
    [SerializeField] private StoryPhase visibleUntil;

    private void Start()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private System.Collections.IEnumerator SubscribeWhenReady()
    {
        while (StoryManager.instance == null) yield return null;
        StoryManager.instance.OnPhaseChanged += OnPhaseChanged;
        Refresh(StoryManager.instance.CurrentPhase);
    }

    private void OnDestroy()
    {
        if (StoryManager.instance != null)
            StoryManager.instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(StoryPhase old, StoryPhase current) => Refresh(current);

    private void Refresh(StoryPhase current)
    {
        bool shouldBeActive = current >= visibleFrom && current <= visibleUntil;
        gameObject.SetActive(shouldBeActive);
    }
}