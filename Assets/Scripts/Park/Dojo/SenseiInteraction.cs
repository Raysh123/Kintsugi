using System.Collections;
using UnityEngine;

/// <summary>
/// Put this on the sensei NPC. He is just another NPC to talk to - fights are chosen at the mat
/// (FightMatInteraction), not here. The one-time "join the dojo" conversation doesn't live here
/// either; it plays automatically on arrival via DojoArrivalHandler.OnArrival().
///
/// Walking up to the sensei (within interactRange) offers one action: Talk (E / A / Submit),
/// which plays talkDialogue. The first talk once TournamentPrep has begun plays prepConversation instead.
///
/// While the dialogue is open, the sensei's NPCPatrol is paused (he stops and stands still -
/// doesn't move mid-conversation) and the player's movement is locked, then both are restored
/// once it closes.
/// </summary>
public class SenseiInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("Paused while the dialogue is open, so the sensei stands still instead of wandering " +
             "off mid-conversation.")]
    [SerializeField] private NPCPatrol patrol;

    [Header("Interaction")]
    [Tooltip("Distance within which Talk becomes available.")]
    [SerializeField] private float interactRange = 3f;
    [Tooltip("Shown while the player is in range and nothing else is busy - e.g. \"E: Talk\".")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private DialogueRunner talkDialogue;

    [Header("Tournament Prep - First Talk")]
    [Tooltip("Plays INSTEAD of the talk dialogue the first time the player talks to him once TournamentPrep has " +
             "begun (before they can fight). Needs a block for TournamentPrep in its ConversationDialogue asset. " +
             "Leave empty to just use the normal dialogue.")]
    [SerializeField] private ConversationRunner prepConversation;

    [Header("Tournament Prep - Injury Meter Panel")]
    [Tooltip("An image panel (keep it INACTIVE in the scene) explaining the injury meter. Shown once, right after the " +
             "first talk with the sensei in TournamentPrep ends. Closed with A (or E / Submit). Leave empty for none.")]
    [SerializeField] private GameObject injuryPanel;
    [Tooltip("Seconds before the panel can be closed, so the press that ended the dialogue doesn't close it.")]
    [SerializeField] private float injuryPanelMinShow = 0.5f;

    [Header("Join Day")]
    [Tooltip("Optional. Unlocked once the player has talked to the sensei at Night during the NewDojoJoin phase " +
             "(the lock is set by DojoArrivalHandler).")]
    [SerializeField] private TravelMapController travelMap;

    private const string PrepConversationFlag = "SenseiPrepConversationDone";

    /// <summary>Set once the player has talked to the sensei in TournamentPrep. The fight mat and the cat stay off until then.</summary>
    public const string FirstPrepTalkDoneFlag = "SenseiFirstPrepTalkDone";

    private bool isBusy; // true during the dialogue - blocks re-entry and input

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
    }

    private IEnumerator RunTalkSequence()
    {
        isBusy = true;
        bool firstPrepTalk = StoryManager.instance != null &&
                             StoryManager.instance.CurrentPhase == StoryPhase.TournamentPrep &&
                             PersistentGameFlags.instance != null &&
                             !PersistentGameFlags.instance.HasFlag(FirstPrepTalkDoneFlag);
        if (patrol != null) patrol.SetPaused(true);
        if (player != null) player.movementLocked = true;
        if (interactPrompt != null) interactPrompt.SetActive(false);

        if (ShouldPlayPrepConversation())
        {
            PersistentGameFlags.instance.SetFlag(PrepConversationFlag);
            prepConversation.Show();
            yield return new WaitUntil(() => !prepConversation.IsRunning);
        }
        else if (talkDialogue != null)
        {
            talkDialogue.Show();
            yield return new WaitUntil(() => !talkDialogue.IsRunning);
        }

        if (firstPrepTalk)
        {
            PersistentGameFlags.instance.SetFlag(FirstPrepTalkDoneFlag);

            if (injuryPanel != null)
            {
                injuryPanel.SetActive(true);
                float shown = 0f;
                while (true)
                {
                    shown += Time.unscaledDeltaTime;
                    if (shown >= injuryPanelMinShow &&
                        (Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit")))
                        break;
                    yield return null;
                }
                injuryPanel.SetActive(false);
                yield return null;
            }
        }

        TryFinishJoinDay();
        QuestLog.Complete("AskSensei");

        if (patrol != null) patrol.SetPaused(false);
        if (player != null) player.movementLocked = false;
        isBusy = false;
    }

    private bool ShouldPlayPrepConversation()
    {
        return prepConversation != null &&
               PersistentGameFlags.instance != null &&
               !PersistentGameFlags.instance.HasFlag(PrepConversationFlag) &&
               StoryManager.instance != null &&
               StoryManager.instance.CurrentPhase == StoryPhase.TournamentPrep;
    }

    private void TryFinishJoinDay()
    {
        if (StoryManager.instance == null || CalendarManager.instance == null) return;
        if (StoryManager.instance.CurrentPhase != StoryPhase.NewDojoJoin) return;
        if (CalendarManager.instance.CurrentPhase != DayPhase.Night) return;
        if (PersistentGameFlags.instance == null) return;

        PersistentGameFlags.instance.SetFlag(DojoArrivalHandler.SenseiNightTalkFlag);
        if (travelMap != null) travelMap.SetLocked(false);
        QuestLog.Complete("TalkToSensei");
    }
}