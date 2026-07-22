using System;
using System.Collections.Generic;
using UnityEngine;

public enum DayPhase { Morning, Afternoon, Evening, Night }

/// <summary>
/// Persona-style calendar: time only advances when an activity is completed.
/// </summary>
public class CalendarManager : MonoBehaviour
{
    public static CalendarManager instance;

    [Header("Starting Date")]
    [SerializeField] private int startYear = 1;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;
    [SerializeField] private int startHour = 6;

    [Header("Day Length")]
    [SerializeField] private int minutesPerDay = 1440;

    [Header("Phase Boundaries (whole hours)")]
    [SerializeField] private int morningStartHour = 6;
    [SerializeField] private int afternoonStartHour = 12;
    [SerializeField] private int eveningStartHour = 18;
    [SerializeField] private int nightStartHour = 22;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private bool showDebugOverlay = true;

    public GameDate CurrentDate { get; private set; }
    public DayPhase CurrentPhase { get; private set; }
    public int MinutesPerDay => minutesPerDay;

    private float _preciseMinutes;
    public float PreciseMinutesSinceDayStart => _preciseMinutes;
    public int MinutesSinceDayStart => Mathf.FloorToInt(_preciseMinutes);

    private readonly HashSet<string> _activitiesDoneToday = new HashSet<string>();

    public event Action<GameDate> OnDayChanged;
    public event Action<DayPhase> OnPhaseChanged;
    public event Action<float> OnTimeAdvanced;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentDate = new GameDate(startYear, startMonth, startDay);
        _preciseMinutes = startHour * 60f;
        CurrentPhase = ComputePhase(_preciseMinutes);
    }

    public void AdvanceTime(float minutes)
    {
        if (minutes <= 0f) return;

        _preciseMinutes += minutes;

        while (_preciseMinutes >= minutesPerDay)
        {
            _preciseMinutes -= minutesPerDay;
            AdvanceDay();
        }

        UpdatePhase();
        OnTimeAdvanced?.Invoke(_preciseMinutes);

        if (debugLog)
            Debug.Log($"[Calendar] +{minutes}m -> {CurrentDate}, {GetFormattedTime()}, {CurrentPhase}");
    }

    public bool TryConsumeActivity(ActivityDefinition activity)
    {
        if (activity.onceLimitPerDay && _activitiesDoneToday.Contains(activity.activityId))
        {
            if (debugLog) Debug.Log($"[Calendar] Blocked '{activity.activityId}' — already done today.");
            return false;
        }

        _activitiesDoneToday.Add(activity.activityId);
        if (debugLog) Debug.Log($"[Calendar] Started '{activity.activityId}' at {GetFormattedTime()}");
        return true;
    }

    public bool HasDoneToday(string activityId) => _activitiesDoneToday.Contains(activityId);

    /// <summary>
    /// Rolls the clock forward to the next morning and advances the day.
    /// Called by BedInteraction when the player chooses to sleep.
    /// </summary>
    public void Sleep()
    {
        float minutesUntilMorning = (minutesPerDay - _preciseMinutes) + (morningStartHour * 60f);
        AdvanceTime(minutesUntilMorning);
        if (debugLog) Debug.Log($"[Calendar] Slept -> {CurrentDate}, {GetFormattedTime()}");
    }

    public string GetFormattedTime()
    {
        int whole = Mathf.FloorToInt(_preciseMinutes);
        int hours = (whole / 60) % 24;
        int mins = whole % 60;
        return $"{hours:00}:{mins:00}";
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
        DayPhase newPhase = ComputePhase(_preciseMinutes);
        if (newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            if (debugLog) Debug.Log($"[Calendar] Phase -> {CurrentPhase}");
            OnPhaseChanged?.Invoke(CurrentPhase);
        }
    }

    private DayPhase ComputePhase(float minutes)
    {
        int hour = Mathf.FloorToInt(minutes / 60f) % 24;
        if (hour >= nightStartHour || hour < morningStartHour) return DayPhase.Night;
        if (hour >= eveningStartHour) return DayPhase.Evening;
        if (hour >= afternoonStartHour) return DayPhase.Afternoon;
        return DayPhase.Morning;
    }

    public void LogCurrentState() =>
        Debug.Log($"[Calendar] {CurrentDate}, {GetFormattedTime()}, {CurrentPhase}");

    private void OnGUI()
    {
        if (!showDebugOverlay) return;
        string text = $"{CurrentDate}\n{GetFormattedTime()}\n{CurrentPhase}";
        GUI.Box(new Rect(10, 10, 180, 70), text);
    }
}