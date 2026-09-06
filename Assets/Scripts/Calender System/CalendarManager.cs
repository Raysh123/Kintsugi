using System;
using System.Collections.Generic;
using UnityEngine;

public enum DayPhase { Morning, Afternoon, Evening, Night }

/// <summary>
/// Persona-style calendar, now driven entirely by quadrant-ticks rather than
/// continuous minutes. A day is 4 quadrants (Morning/Afternoon/Evening/Night),
/// each made of TicksPerQuadrant ticks — default 4, giving quarter-quadrant
/// granularity, so an activity can cost as little as 1 tick (a quarter) or as
/// much as several quadrants at once. There's no clock-time concept anymore;
/// CurrentPhase (and CurrentTick within it, if you need finer UI feedback) is
/// the only notion of "what time it is."
/// </summary>
public class CalendarManager : MonoBehaviour
{
    public static CalendarManager instance;

    /// <summary>Ticks in one quadrant. 4 = quarter-quadrant granularity (1 tick = 25% of a quadrant).</summary>
    public const int TicksPerQuadrant = 4;
    private const int QuadrantsPerDay = 4;
    /// <summary>Total ticks in a full day (all 4 quadrants).</summary>
    public const int TicksPerDay = TicksPerQuadrant * QuadrantsPerDay;

    [Header("Starting Date")]
    [SerializeField] private int startYear = 1;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;
    [SerializeField] private DayPhase startPhase = DayPhase.Morning;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private bool showDebugOverlay = true;

    public GameDate CurrentDate { get; private set; }
    public DayPhase CurrentPhase { get; private set; }

    private int _currentTick;
    /// <summary>Ticks elapsed since the start of the current day (0 to TicksPerDay-1).</summary>
    public int CurrentTick => _currentTick;
    /// <summary>How many ticks into the CURRENT quadrant specifically (0 to TicksPerQuadrant-1).</summary>
    public int TickWithinQuadrant => _currentTick % TicksPerQuadrant;

    private readonly HashSet<string> _activitiesDoneToday = new HashSet<string>();

    public event Action<GameDate> OnDayChanged;
    public event Action<DayPhase> OnPhaseChanged;
    /// <summary>Fired whenever time advances, with the new CurrentTick value.</summary>
    public event Action<int> OnTimeAdvanced;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentDate = new GameDate(startYear, startMonth, startDay);
        _currentTick = (int)startPhase * TicksPerQuadrant;
        CurrentPhase = startPhase;
    }

    /// <summary>Advances time by the given number of ticks — see ActivityDefinition.quadrantTicks.</summary>
    public void AdvanceTime(int ticks)
    {
        if (ticks <= 0) return;

        _currentTick += ticks;

        while (_currentTick >= TicksPerDay)
        {
            _currentTick -= TicksPerDay;
            AdvanceDay();
        }

        UpdatePhase();
        OnTimeAdvanced?.Invoke(_currentTick);

        if (debugLog)
            Debug.Log($"[Calendar] +{ticks} ticks -> {CurrentDate}, {CurrentPhase}");
    }

    public bool TryConsumeActivity(ActivityDefinition activity)
    {
        if (activity.onceLimitPerDay && _activitiesDoneToday.Contains(activity.activityId))
        {
            if (debugLog) Debug.Log($"[Calendar] Blocked '{activity.activityId}' — already done today.");
            return false;
        }

        _activitiesDoneToday.Add(activity.activityId);
        if (debugLog) Debug.Log($"[Calendar] Started '{activity.activityId}' during {CurrentPhase}");
        return true;
    }

    public bool HasDoneToday(string activityId) => _activitiesDoneToday.Contains(activityId);

    /// <summary>
    /// Rolls the clock forward to the next morning and advances the day.
    /// Called by BedInteraction when the player chooses to sleep.
    /// </summary>
    public void Sleep()
    {
        int ticksUntilNextMorning = TicksPerDay - _currentTick;
        if (ticksUntilNextMorning <= 0) ticksUntilNextMorning = TicksPerDay;

        AdvanceTime(ticksUntilNextMorning);
        if (debugLog) Debug.Log($"[Calendar] Slept -> {CurrentDate}, {CurrentPhase}");
    }

    private void AdvanceDay()
    {
        CurrentDate = CurrentDate.AddDays(1);
        _activitiesDoneToday.Clear();
        if (debugLog) Debug.Log($"[Calendar] === New day: {CurrentDate} ===");
        OnDayChanged?.Invoke(CurrentDate);
    }

    private void UpdatePhase()
    {
        DayPhase newPhase = (DayPhase)(_currentTick / TicksPerQuadrant);
        if (newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            if (debugLog) Debug.Log($"[Calendar] Phase -> {CurrentPhase}");
            OnPhaseChanged?.Invoke(CurrentPhase);
        }
    }

    public void LogCurrentState() =>
        Debug.Log($"[Calendar] {CurrentDate}, {CurrentPhase} (tick {_currentTick}/{TicksPerDay})");

    private void OnGUI()
    {
        if (!showDebugOverlay) return;
        string storyPhase = StoryManager.instance != null ? StoryManager.instance.CurrentPhase.ToString() : "N/A";
        string text = $"{CurrentDate}\nStory: {storyPhase}\nTime of Day: {CurrentPhase}\ntick {_currentTick}/{TicksPerDay}";
        GUI.Box(new Rect(10, 10, 220, 90), text);
    }
}