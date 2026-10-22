using System;
using UnityEngine;

/// <summary>
/// Persistent singleton - survives scene loads alongside CalendarManager and
/// HealthAndMotivation. Owns the current story phase and all key narrative dates.
///
/// Other systems never set CurrentPhase directly - they call AdvancePhase() or
/// TryAdvanceToPhase() and let StoryManager fire the event so every subscriber
/// updates consistently.
/// </summary>
public class StoryManager : MonoBehaviour
{
    public static StoryManager instance;

    [Header("Current State")]
    [Tooltip("Set to FriendVisit for the EGX build - BedRest is skipped entirely this way. " +
             "BedRestPhaseController/DojoArrivalHandler/HomeArrivalHandler all already handle " +
             "starting past BedRest gracefully (they no-op/self-gate on CurrentPhase), so no " +
             "other script needs to change for this - just this default (or set it directly " +
             "on the StoryManager component in the Inspector instead, same effect, no code " +
             "edit needed).")]
    [SerializeField] private StoryPhase startingPhase = StoryPhase.FriendVisit;
    public StoryPhase CurrentPhase { get; private set; }

    [Header("Key Narrative Dates")]

    public GameDate storyStartDate = new GameDate(1, 1, 1);

    [Header("Phase Durations (days, relative to when each phase itself begins)")]
    [Tooltip("How many days BedRest lasts before advancing to FriendVisit.")]
    public int bedRestDurationDays = 1;
    [Tooltip("How many days NewDojoJoin lasts before advancing to TournamentPrep.")]
    public int newDojoJoinDurationDays = 7;

    [Tooltip("How many days FriendVisit lasts before advancing to NewDojoJoin.")]
    public int daysUntilNewDojo = 1;

    [Tooltip("The tournament is set when the player first talks to the sensei in TournamentPrep: that day + this many days. " +
             "10 means the next morning's banner reads \"9 days till the tournament\".")]
    public int daysUntilTournament = 10;

    // Computed dates - set automatically when the relevant phase is reached
    [Header("Computed Dates (read only - set at runtime)")]
    [SerializeField] private GameDate newDojoDate;
    [SerializeField] private GameDate tournamentDate;
    [SerializeField] private bool tournamentDateSet;
    public bool TournamentDateSet => tournamentDateSet;

    // Tournament prep tracking
    [Header("Tournament Prep")]
    public bool beatenPartnerA;
    public bool beatenPartnerB;
    public bool BothPartnersBeaten => beatenPartnerA && beatenPartnerB;

    [SerializeField] private bool hasHadFirstDojoFight;
    public bool HasHadFirstDojoFight => hasHadFirstDojoFight;

    /// <summary>
    /// Call this when a dojo fight begins. Returns true if this is the FIRST
    /// time - the fight system should then trigger the injury meter tutorial
    /// before the fight starts. Returns false on all subsequent fights.
    /// </summary>
    public bool RegisterDojoFight()
    {
        if (hasHadFirstDojoFight) return false;
        hasHadFirstDojoFight = true;
        return true;
    }

    public event Action<StoryPhase, StoryPhase> OnPhaseChanged; // (oldPhase, newPhase)

    public event Action<StoryPhase> OnScheduledEventTriggered;

    /// <summary>Fired when the tournament deadline passes without both partners beaten.</summary>
    public event Action OnGameOver;

    // Tracks when the CURRENT phase began, generically — set once at Awake for the
    // starting phase, then re-set on every SetPhase() transition. This is what lets
    // DialogueBlock's "day number since phase started" narrowing work for ANY phase,
    // not just BedRest specifically.
    private GameDate currentPhaseStartDate;
    public GameDate CurrentPhaseStartDate => currentPhaseStartDate;

    /// <summary>1-indexed day count since the current phase began (Day 1 = the day it started).</summary>
    public int DaysSincePhaseStart =>
        CalendarManager.instance != null
            ? CalendarManager.instance.CurrentDate.AbsoluteDay - currentPhaseStartDate.AbsoluteDay + 1
            : 1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // DontDestroyOnLoad silently does nothing unless this is a ROOT object - if it sits
        // under another GameObject it gets destroyed with the scene and a fresh one (reset to
        // startingPhase) is created on the next load, which looks exactly like "phase not saved".
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);

        CurrentPhase = startingPhase;
        currentPhaseStartDate = storyStartDate;
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeToCalendar());
    }

    private System.Collections.IEnumerator SubscribeToCalendar()
    {
        while (CalendarManager.instance == null) yield return null;
        CalendarManager.instance.OnDayChanged += OnDayChanged;
    }

    private void OnDisable()
    {
        if (CalendarManager.instance != null)
            CalendarManager.instance.OnDayChanged -= OnDayChanged;
    }

    public void AdvancePhase()
    {
        StoryPhase next = CurrentPhase + 1;
        if ((int)next > (int)StoryPhase.TournamentDay) return;
        SetPhase(next);
    }

    public void TryAdvanceToPhase(StoryPhase target)
    {
        if (target <= CurrentPhase) return;
        SetPhase(target);
    }

    private void SetPhase(StoryPhase newPhase)
    {
        StoryPhase old = CurrentPhase;
        CurrentPhase = newPhase;
        currentPhaseStartDate = CalendarManager.instance != null
            ? CalendarManager.instance.CurrentDate
            : storyStartDate;


        OnPhaseChanged?.Invoke(old, newPhase);
        HandlePhaseStarted(newPhase);
    }

    private void HandlePhaseStarted(StoryPhase phase)
    {
        GameDate today = CalendarManager.instance != null
            ? CalendarManager.instance.CurrentDate
            : storyStartDate;

        switch (phase)
        {
            case StoryPhase.BedRest:
                break;

            case StoryPhase.FriendVisit:
                newDojoDate = today.AddDays(daysUntilNewDojo);
                break;

            case StoryPhase.NewDojoJoin:
                break;

            case StoryPhase.TournamentPrep:
                break;

            case StoryPhase.TournamentDay:
                HandleTournamentDay();
                break;
        }
    }

    /// <summary>
    /// Called every in-game day by CalendarManager.OnDayChanged.
    /// Drives all date-based phase transitions - this is the single place
    /// the story moves forward automatically.
    /// </summary>
    private void OnDayChanged(GameDate newDate)
    {
        switch (CurrentPhase)
        {
            case StoryPhase.BedRest:
                if (newDate >= currentPhaseStartDate.AddDays(bedRestDurationDays))
                {
                    TryAdvanceToPhase(StoryPhase.FriendVisit);
                    OnScheduledEventTriggered?.Invoke(StoryPhase.FriendVisit);
                }
                break;

            case StoryPhase.FriendVisit:
                if (newDate >= newDojoDate)
                {
                    TryAdvanceToPhase(StoryPhase.NewDojoJoin);
                    OnScheduledEventTriggered?.Invoke(StoryPhase.NewDojoJoin);
                }
                break;

            case StoryPhase.NewDojoJoin:
                if (newDate >= currentPhaseStartDate.AddDays(newDojoJoinDurationDays))
                    TryAdvanceToPhase(StoryPhase.TournamentPrep);
                break;

            case StoryPhase.TournamentPrep:
                if (tournamentDateSet && newDate >= tournamentDate)
                {
                    if (BothPartnersBeaten)
                        TryAdvanceToPhase(StoryPhase.TournamentDay);
                    else
                        HandleGameOver();
                }
                break;
        }
    }

    /// <summary>
    /// Call this when the player defeats a sparring partner in the new dojo.
    /// If both are now beaten, offer the option to skip to tournament day.
    /// </summary>
    public void RegisterPartnerDefeated(bool isPartnerA)
    {
        if (isPartnerA) beatenPartnerA = true;
        else beatenPartnerB = true;


        if (BothPartnersBeaten)
        {
            TryAdvanceToPhase(StoryPhase.TournamentDay);
        }
    }

    /// <summary>
    /// Called by sensei interaction after both partners are beaten.
    /// Skips to tournament date.
    /// </summary>
    public void SkipToTournament()
    {
        if (!BothPartnersBeaten) return;
        TryAdvanceToPhase(StoryPhase.TournamentDay);
    }

    private void HandleTournamentDay()
    {
        // Hook your ending cutscene/scene load here
    }

    private void HandleGameOver()
    {
        OnGameOver?.Invoke();
        // For the demo: hard reset to start of game
        // UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    /// <summary>Called when the player first talks to the sensei in TournamentPrep. Fixes the tournament date from today; only the first call counts.</summary>
    public void SetTournamentDateFromToday()
    {
        if (tournamentDateSet) return;
        GameDate today = CalendarManager.instance != null ? CalendarManager.instance.CurrentDate : storyStartDate;
        tournamentDate = today.AddDays(daysUntilTournament);
        tournamentDateSet = true;
    }

    public GameDate GetTournamentDate() => tournamentDate;
    public GameDate GetNewDojoDate() => newDojoDate;
}