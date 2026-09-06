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
    [SerializeField] private StoryPhase startingPhase = StoryPhase.BedRest;
    public StoryPhase CurrentPhase { get; private set; }

    [Header("Key Narrative Dates")]

    public GameDate storyStartDate = new GameDate(1, 1, 1);

    [Header("Phase Durations (days, relative to when each phase itself begins)")]
    [Tooltip("How many days BedRest lasts before advancing to DojoRejection.")]
    public int bedRestDurationDays = 2;
    [Tooltip("How many days DojoRejection lasts before advancing to HouseTraining.")]
    public int dojoRejectionDurationDays = 2;
    [Tooltip("How many days NewDojoJoin lasts before advancing to TournamentPrep.")]
    public int newDojoJoinDurationDays = 7;

    public int daysUntilFriendVisit = 3;

    public int daysUntilNewDojo = 1;

    public int daysUntilTournament = 10;

    // Computed dates - set automatically when the relevant phase is reached
    [Header("Computed Dates (read only - set at runtime)")]
    [SerializeField] private GameDate dojoRejectionDate;
    [SerializeField] private GameDate friendVisitDate;
    [SerializeField] private GameDate newDojoDate;
    [SerializeField] private GameDate tournamentDate;

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
        Debug.Log("[Story] First dojo fight - injury meter tutorial should trigger.");
        return true;
    }

    public event Action<StoryPhase, StoryPhase> OnPhaseChanged; // (oldPhase, newPhase)

    public event Action<StoryPhase> OnScheduledEventTriggered;

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
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
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

        Debug.Log($"[Story] =============================");
        Debug.Log($"[Story] Phase changed: {old} -> {newPhase}");
        Debug.Log($"[Story] Date: {(CalendarManager.instance != null ? CalendarManager.instance.CurrentDate.ToString() : "N/A")}");
        Debug.Log($"[Story] Phase: {(CalendarManager.instance != null ? CalendarManager.instance.CurrentPhase.ToString() : "N/A")}");
        Debug.Log($"[Story] =============================");

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
                Debug.Log("[Story] BedRest begun - player confined to bed, physio minigames available.");
                break;

            case StoryPhase.DojoRejection:
                dojoRejectionDate = today;
                friendVisitDate = today.AddDays(daysUntilFriendVisit);
                Debug.Log($"[Story] DojoRejection - removed from old dojo. Friend visits on: {friendVisitDate}");
                break;

            case StoryPhase.HouseTraining:
                Debug.Log("[Story] HouseTraining - player training alone, depression beat.");
                break;

            case StoryPhase.FriendVisit:
                newDojoDate = friendVisitDate.AddDays(daysUntilNewDojo);
                Debug.Log($"[Story] FriendVisit - friend has arrived. New dojo unlocks on: {newDojoDate}");
                break;

            case StoryPhase.NewDojoJoin:
                tournamentDate = today.AddDays(daysUntilTournament);
                Debug.Log($"[Story] NewDojoJoin - joined new dojo. Tournament date set to: {tournamentDate}");
                break;

            case StoryPhase.TournamentPrep:
                Debug.Log($"[Story] TournamentPrep - beat both partners before {tournamentDate} to enter tournament.");
                Debug.Log($"[Story] Partner A beaten: {beatenPartnerA} | Partner B beaten: {beatenPartnerB}");
                break;

            case StoryPhase.TournamentDay:
                Debug.Log("[Story] TournamentDay - demo ending reached.");
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
                    TryAdvanceToPhase(StoryPhase.DojoRejection);
                break;

            case StoryPhase.DojoRejection:
                if (newDate >= currentPhaseStartDate.AddDays(dojoRejectionDurationDays))
                    TryAdvanceToPhase(StoryPhase.HouseTraining);
                break;

            case StoryPhase.HouseTraining:
                if (newDate >= friendVisitDate)
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
                if (newDate >= tournamentDate)
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

        Debug.Log($"[Story] Partner A: {beatenPartnerA}, Partner B: {beatenPartnerB}");

        if (BothPartnersBeaten)
            Debug.Log("[Story] Both partners beaten - player can now talk to Sensei to advance to tournament.");
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
        Debug.Log("[Story] Tournament day - demo ending reached.");
        // Hook your ending cutscene/scene load here
    }

    private void HandleGameOver()
    {
        Debug.Log("[Story] Game over - tournament deadline passed without beating both partners.");
        // For the demo: hard reset to start of game
        // UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public GameDate GetTournamentDate() => tournamentDate;
    public GameDate GetFriendVisitDate() => friendVisitDate;
    public GameDate GetNewDojoDate() => newDojoDate;
}