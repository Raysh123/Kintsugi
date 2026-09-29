using UnityEngine;

/// <summary>
/// Data-driven definition for anything the player can "do" that consumes calendar
/// time: sparring, a recovery minigame, resting, training with sensei, etc.
/// Time cost is expressed in quadrant-ticks (see CalendarManager) rather than
/// clock minutes — activities move the day forward in Morning/Afternoon/
/// Evening/Night chunks, not by the hour.
/// Create one asset per activity (Assets > Create > RPG > Activity Definition)
/// and reference it from whatever script triggers that activity, instead of
/// hardcoding time costs and once-per-day logic in every interaction script.
/// </summary>
[CreateAssetMenu(fileName = "NewActivity", menuName = "RPG/Activity Definition")]
public class ActivityDefinition : ScriptableObject
{
    [Tooltip("Unique key used for once-per-day tracking and event lookups. " +
             "Keep this stable once other systems (like scheduled events) reference it.")]
    public string activityId;

    public string displayName;

    [Tooltip("How much of the day this activity consumes, in quarter-quadrant units " +
             "(CalendarManager.TicksPerQuadrant = 4 ticks per quadrant). 1 = a quarter of a " +
             "quadrant, 2 = half, 4 = one full quadrant, 8 = two full quadrants, etc.")]
    public int quadrantTicks = CalendarManager.TicksPerQuadrant;

    [Tooltip("If true, this activity can only be performed once per calendar day.")]
    public bool onceLimitPerDay = true;

    [Tooltip("Optional: minimum motivation required to attempt this (harder recovery " +
             "minigames can require higher motivation to even start).")]
    public float minMotivationRequired = 0f;

    [Tooltip("If true, this activity happens in a different scene (e.g. an instanced " +
             "fight). The scene loader is responsible for calling AdvanceTime itself " +
             "on return, using this activity's quadrantTicks.")]
    public bool isCrossScene = false;
}