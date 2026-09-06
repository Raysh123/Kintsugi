using UnityEngine;

/// <summary>
/// One entry in BedRestPhaseController's activity sequence — the DATA half only
/// (activity type, ActivityDefinition, rewards, animation state names). Scene
/// object references (minigame instances, onboarding/activity UI) can't live
/// here — ScriptableObject assets can't reference scene objects via the
/// Inspector — so those live in the parallel BedActivitySlotSceneRefs list on
/// BedRestPhaseController instead, matched by list index.
///
/// This is a ScriptableObject asset — not an inline list entry — specifically
/// because a List of a large [System.Serializable] class (20+ fields) can hit
/// a known Unity Inspector bug where editing one array element visibly
/// corrupts/mirrors another. Separate asset files can't cross-contaminate
/// that way.
///
/// Create via: Assets > Create > RPG > Bed Activity Slot
/// </summary>
[CreateAssetMenu(fileName = "NewBedActivitySlot", menuName = "RPG/Bed Activity Slot")]
public class BedActivitySlotDefinition : ScriptableObject
{
    [Tooltip("Inspector-only label, e.g. 'Day 1 - Morning'. Not used at runtime.")]
    public string label;
    public BedActivityType activityType;
    public ActivityDefinition activity;

    [Header("Activity Pose")]
    [Tooltip("Base pose animation for this specific activity — e.g. a deadlift pose for Type A, " +
             "holding kick pads for Type B. Falls back to the shared Recovery Animation on the " +
             "controller if left blank.")]
    public string activityAnimationState;

    [Header("Type A Animation (ignored for other types)")]
    [Tooltip("Animator float parameter driving Motion Time on the pose above — set that state to " +
             "use Motion Time in the Animator Controller and assign it this parameter name.")]
    public string fillProgressParam;
    public string typeAWinAnimation;
    public string typeALoseAnimation;
    public string typeAInjuredAnimation;

    [Header("Type B Animation (ignored for other types)")]
    [Tooltip("Plays on every button press during this activity, regardless of hit or miss — " +
             "purely cosmetic, separate from the reaction animations below.")]
    public string typeBActionAnimation;
    public string typeBMissAnimation;
    public string typeBInjuredAnimation;

    [Header("Rewards / Penalties")]
    public float winHealth;
    public float winMotivation;
    public float loseHealth;
    public float loseMotivation;
    public float injuryHealth;
    public float injuryMotivation;

    [Header("Presentation")]
    [Tooltip("If true, the shared health/motivation meter UI is shown during this slot.")]
    public bool showHealthMotivationMeter;
}

public enum BedActivityType { TypeA, TypeB, TypeC }