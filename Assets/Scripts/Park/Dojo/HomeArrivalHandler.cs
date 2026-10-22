using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the home location (called by TravelMapController through ITravelArrivalHandler whenever the player
/// arrives here). Every arrival reverts the player to the "Normal" outfit. On top of that it runs two one-off
/// story beats, each remembered through PersistentGameFlags so it never repeats:
///
/// 1) THE FIRST DAY (the cat is hidden all morning, so the only thing to do is go to the park):
///    on the first arrival home, right after the park (Afternoon, story in FriendVisit):
///      - the cat appears and the "Locked During First Day" objects hide;
///      - the player's "I need to talk to her... she's not here" monologue plays;
///      - the player can now only play with the cat (CatPlayInteraction);
///      - once that session finishes, time moves on to Evening: the map is locked, the doors close, the cat is
///        locked, and mom appears where she stands in the scene - the only thing left is to walk up to her and talk;
///      - her conversation plays and passes time up to Night: the map, doors, cat and hidden objects are all freed
///        again for good, and the only thing left to do is sleep.
///
/// 2) THE DOJO DAY ENDS: the player joins the dojo and does the day's activities, comes home at Night and sleeps.
///    The first new morning after joining, the story moves to TournamentPrep and the normal daily loop begins.
///
/// Home must have Night in its Allowed Times Of Day in TravelMapController, or the player can't come home at night.
/// </summary>
public class HomeArrivalHandler : MonoBehaviour, ITravelArrivalHandler
{
    private const string FirstVisitBegunFlag = "HomeFirstVisitBegun";
    private const string CatDoneFlag = "HomeCatDone";
    private const string MomTalkDoneFlag = "HomeMomTalkDone";
    private const string PrepStartedFlag = "TournamentPrepStarted";
    private const string DojoJoinedFlag = "DojoJoined";
    private const string PrepMorningFlag = "TournamentPrepMorningDone";

    [Header("Appearance")]
    [SerializeField] private PlayerAppearanceController appearance;
    [SerializeField] private string normalModelId = "Normal";

    [Header("Tournament Prep - First Morning")]
    [Tooltip("Plays the first morning after waking up in TournamentPrep, then the 'ask the sensei' task appears. " +
             "Needs a block for TournamentPrep / Morning in its dialogue asset. Optional.")]
    [SerializeField] private DialogueRunner prepMorningDialogue;

    [Header("Player & Map")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("Locked from the evening of the first day until Night, so the player can only talk to mom. Free for good after.")]
    [SerializeField] private TravelMapController travelMap;
    [Tooltip("Doors closed over the same stretch as the map (RoomDoorLock swaps its closed/open door objects).")]
    [SerializeField] private List<RoomDoorLock> doorsClosedDuringFirstDay = new List<RoomDoorLock>();

    [Header("First Day - Monologue")]
    [Tooltip("The player's 'I need to talk to her... she's not here' line. A single-speaker dialogue.")]
    [SerializeField] private DialogueRunner needToTalkDialogue;

    [Header("First Day - Cat")]
    [Tooltip("The cat's play interaction. The sequence continues once a session finishes; it is then locked until Night.")]
    [SerializeField] private CatPlayInteraction catPlay;
    [Tooltip("The cat's root object. Leave it INACTIVE in the scene: it stays hidden all morning and is switched on " +
             "when the player first gets home. If empty, the object CatPlayInteraction is on is used.")]
    [SerializeField] private GameObject catObject;
    [Tooltip("Shown whenever the cat is hidden (the morning of the first day, and the whole dojo join day), e.g. the cat " +
             "asleep somewhere out of reach, so she doesn't just vanish. Switched off while the cat is out. Optional.")]
    [SerializeField] private GameObject catSleepingObject;
    [Tooltip("Everything else at home that should be unavailable on the first day (other interactions...). " +
             "Hidden when the player first gets home, shown again at Night.")]
    [SerializeField] private List<GameObject> lockedDuringFirstDay = new List<GameObject>();

    [Header("First Day - Evening With Mom")]
    [Tooltip("Mom's object. Inactive in the scene; switched on in the evening exactly where she is placed.")]
    [SerializeField] private GameObject momObject;
    [Tooltip("Shown while the player is close enough to talk to mom (e.g. 'E: Talk'). Optional.")]
    [SerializeField] private GameObject momTalkPrompt;
    [Tooltip("How close the player has to be to mom to talk to her (E / A / Submit).")]
    [SerializeField] private float momInteractRange = 3f;
    [Tooltip("Needs a block for FriendVisit / Evening (or Any time of day) in its ConversationDialogue asset.")]
    [SerializeField] private ConversationRunner momConversation;
    [Tooltip("Switch mom's object off again once the conversation is over.")]
    [SerializeField] private bool hideMomAfter = true;

    private bool sequenceRunning;
    private bool catFinished;

    private static bool HasFlag(string id) => PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(id);
    private static void SetFlag(string id) { if (PersistentGameFlags.instance != null) PersistentGameFlags.instance.SetFlag(id); }

    private void OnEnable()
    {
        StartCoroutine(SubscribeToCalendar());
        BedInteraction.OnAnyWakeUp += HandleWakeUp;
    }

    private void OnDisable()
    {
        BedInteraction.OnAnyWakeUp -= HandleWakeUp;
        if (CalendarManager.instance != null)
            CalendarManager.instance.OnDayChanged -= HandleDayChanged;
    }

    private IEnumerator SubscribeToCalendar()
    {
        while (CalendarManager.instance == null) yield return null;
        CalendarManager.instance.OnDayChanged += HandleDayChanged;
    }

    /// <summary>The first new day after the dojo has been joined starts TournamentPrep.</summary>
    private void HandleDayChanged(GameDate newDate)
    {
        if (!HasFlag(DojoJoinedFlag) || HasFlag(PrepStartedFlag)) return;
        if (StoryManager.instance == null) return;

        SetFlag(PrepStartedFlag);
        StoryManager.instance.TryAdvanceToPhase(StoryPhase.TournamentPrep);
        RefreshCat();
    }

    private void HandleWakeUp()
    {
        if (!HasFlag(PrepStartedFlag) || HasFlag(PrepMorningFlag) || sequenceRunning) return;
        StartCoroutine(PrepMorningSequence());
    }

    private IEnumerator PrepMorningSequence()
    {
        SetFlag(PrepMorningFlag);

        if (prepMorningDialogue != null)
        {
            if (player != null) player.movementLocked = true;
            prepMorningDialogue.Show();
            yield return new WaitUntil(() => !prepMorningDialogue.IsRunning);
            if (player != null) player.movementLocked = false;
        }

        QuestLog.Show("AskSensei");
    }

    private void Start()
    {
        // The cat is only out between the first afternoon and the dojo join day.
        RefreshCat();

        // The scene may also have reloaded part-way through the first day - put the locks back.
        if (HasFlag(FirstVisitBegunFlag) && !HasFlag(MomTalkDoneFlag))
        {
            SetObjectsLocked(true);
            if (HasFlag(CatDoneFlag))
            {
                SetCatLocked(true);
                SetMapAndDoorsLocked(true);
            }
        }
    }

    public void OnArrival()
    {
        if (appearance != null) appearance.SetModel(normalModelId);
        if (sequenceRunning) return;

        RefreshCat();

        StoryManager story = StoryManager.instance;
        if (story == null) return;

        if (!HasFlag(FirstVisitBegunFlag) && story.CurrentPhase == StoryPhase.FriendVisit)
            StartCoroutine(FirstDaySequence());
    }

    private IEnumerator FirstDaySequence()
    {
        sequenceRunning = true;
        SetFlag(FirstVisitBegunFlag);
        SetObjectsLocked(true);
        SetCatVisible(true);

        if (player != null) player.movementLocked = true;

        if (needToTalkDialogue != null)
        {
            needToTalkDialogue.Show();
            yield return new WaitUntil(() => !needToTalkDialogue.IsRunning);
        }

        // Only the cat is available now.
        if (player != null) player.movementLocked = false;
        QuestLog.Show("PlayWithHime");

        catFinished = false;
        if (catPlay != null)
        {
            catPlay.OnSessionFinished += HandleCatFinished;
            yield return new WaitUntil(() => catFinished);
            catPlay.OnSessionFinished -= HandleCatFinished;
        }

        SetFlag(CatDoneFlag);
        QuestLog.Complete("PlayWithHime");

        // Let the cat activity finish handing control back, then move on to the evening.
        while (player != null && player.interacting) yield return null;
        RefreshCat();
        AdvanceToPhase(DayPhase.Evening);
        while (ActiveDialogueTracker.AnyActive) yield return null;

        // Evening: the map is off, the doors are closed and the cat is locked. Only mom is left.
        SetCatLocked(true);
        SetMapAndDoorsLocked(true);

        // Mom appears where she stands; the only thing left to do is walk up to her and talk.
        if (momObject != null)
        {
            momObject.SetActive(true);
            yield return WaitForMomTalk();
        }

        if (player != null) player.movementLocked = true;

        if (momConversation != null)
        {
            momConversation.Show();
            yield return new WaitUntil(() => !momConversation.IsRunning);
        }

        QuestLog.Complete("TalkToMom");

        // The conversation passes the rest of the day. Night: everything is freed for good, and the bed is usable.
        AdvanceToPhase(DayPhase.Night);

        if (hideMomAfter && momObject != null) momObject.SetActive(false);
        SetFlag(MomTalkDoneFlag);

        SetMapAndDoorsLocked(false);
        SetObjectsLocked(false);
        SetCatLocked(false);

        // Anything the time jump triggers (night lines etc.) finishes first, then control is handed back for good.
        yield return null;
        while (ActiveDialogueTracker.AnyActive) yield return null;
        if (player != null) { player.movementLocked = false; player.interacting = false; }
        sequenceRunning = false;
    }

    /// <summary>Waits until the player is within range of mom and presses the interact button.</summary>
    private bool momPromptMine;

    private IEnumerator WaitForMomTalk()
    {
        while (true)
        {
            bool inRange = player != null &&
                           Vector3.Distance(player.transform.position, momObject.transform.position) <= momInteractRange;
            bool free = !ActiveDialogueTracker.AnyActive && !player.movementLocked;

            SharedPrompt.Set(ref momPromptMine, momTalkPrompt, inRange && free);

            if (inRange && free &&
                (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit")))
                break;

            yield return null;
        }

        SharedPrompt.Set(ref momPromptMine, momTalkPrompt, false);
    }

    private void HandleCatFinished()
    {
        catFinished = true;
    }

    private void SetMapAndDoorsLocked(bool locked)
    {
        if (travelMap != null) travelMap.SetLocked(locked);

        foreach (RoomDoorLock door in doorsClosedDuringFirstDay)
        {
            if (door == null) continue;
            if (locked) door.Lock();
            else door.Unlock();
        }
    }

    private void SetObjectsLocked(bool locked)
    {
        foreach (GameObject obj in lockedDuringFirstDay)
            if (obj != null) obj.SetActive(!locked);
    }

    /// <summary>The cat is hidden before the first day begins, from the end of her first play session until the player has talked to the sensei in TournamentPrep, and throughout the dojo join day; otherwise she is out.</summary>
    private void RefreshCat()
    {
        StoryPhase phase = StoryManager.instance != null ? StoryManager.instance.CurrentPhase : StoryPhase.FriendVisit;
        bool hidden = !HasFlag(FirstVisitBegunFlag) ||
                      phase == StoryPhase.NewDojoJoin ||
                      (HasFlag(CatDoneFlag) && !HasFlag(SenseiInteraction.FirstPrepTalkDoneFlag)) ||
                      (phase == StoryPhase.TournamentPrep && !HasFlag(SenseiInteraction.FirstPrepTalkDoneFlag));
        SetCatVisible(!hidden);
    }

    private void SetCatVisible(bool visible)
    {
        GameObject cat = catObject != null ? catObject : (catPlay != null ? catPlay.gameObject : null);
        if (cat != null) cat.SetActive(visible);
        if (catSleepingObject != null) catSleepingObject.SetActive(!visible);
    }

    /// <summary>Locks the cat interaction without hiding the cat (the script is switched off, so no prompt or play).</summary>
    private void SetCatLocked(bool locked)
    {
        if (catPlay != null) catPlay.enabled = !locked;
    }

    /// <summary>Advances the calendar forward to the start of the given time of day. Does nothing if it's already
    /// that time of day or later.</summary>
    private static void AdvanceToPhase(DayPhase target)
    {
        CalendarManager calendar = CalendarManager.instance;
        if (calendar == null) return;

        int targetTick = (int)target * CalendarManager.TicksPerQuadrant;
        int diff = targetTick - calendar.CurrentTick;
        if (diff > 0) calendar.AdvanceTime(diff);
    }
}