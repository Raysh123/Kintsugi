using UnityEngine;

/// <summary>
/// Data-driven definition for anything the player can "do" that consumes calendar
/// time: sparring, a recovery minigame, resting, training with sensei, etc.
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

    [Tooltip("In-game minutes this activity consumes when completed.")]
    public int timeCostMinutes = 60;

    [Tooltip("If true, this activity can only be performed once per calendar day.")]
    public bool onceLimitPerDay = true;

    [Tooltip("Optional: minimum motivation required to attempt this (harder recovery " +
             "minigames can require higher motivation to even start).")]
    public float minMotivationRequired = 0f;

    [Tooltip("If true, this activity happens in a different scene (e.g. an instanced " +
             "fight). The scene loader is responsible for calling AdvanceTime itself " +
             "on return, using this activity's timeCostMinutes.")]
    public bool isCrossScene = false;
}