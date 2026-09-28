using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns groups of objects on/off depending on the current DayPhase - built for lights
/// (arrange spotlights/point lights under an empty parent, then list that parent here with
/// the phases it should be on), but works for anything: NPCs, particle effects, props.
///
/// Each entry says "this object is ACTIVE during these phases, INACTIVE otherwise", so one
/// group can cover several phases (street lamps: Evening + Night) and one phase can have
/// several groups. Applies immediately on start (so it's correct the moment the scene
/// loads) and again every time CalendarManager.OnPhaseChanged fires - same hookup and same
/// hard-swap reasoning as TimeOfDaySkybox (phase only changes while traveling/asleep).
///
/// Can sit on the same object as TimeOfDaySkybox or on its own - they don't depend on each
/// other.
///
/// Note for lights: only Realtime/Mixed lights actually respond to being toggled. A light
/// set to Baked has its contribution frozen into the lightmap, so switching its object
/// on/off at runtime changes nothing visible.
/// </summary>
public class TimeOfDayObjectToggler : MonoBehaviour
{
    [System.Serializable]
    public class ToggleEntry
    {
        [Tooltip("Just for your own reference in the Inspector list (e.g. \"Street lamps\").")]
        public string label;

        [Tooltip("The object switched on/off - typically an empty parent holding the lights.")]
        public GameObject target;

        [Tooltip("Target is ACTIVE during these phases and INACTIVE during every other one.")]
        public List<DayPhase> activeDuring = new List<DayPhase>();
    }

    [SerializeField] private List<ToggleEntry> entries = new List<ToggleEntry>();

    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribeAndApply();
    }

    private void Start()
    {
        // OnEnable can run before CalendarManager.Awake sets its instance - Start() always
        // runs after every Awake, so retry here if OnEnable couldn't connect.
        if (!subscribed) TrySubscribeAndApply();

        if (!subscribed)
        {
            Debug.LogWarning("[TimeOfDayObjectToggler] CalendarManager.instance still null after " +
                              "Start() - nothing will ever be toggled. Is there a CalendarManager in this scene?");
        }
    }

    private void TrySubscribeAndApply()
    {
        if (CalendarManager.instance == null) return;

        Apply(CalendarManager.instance.CurrentPhase);
        CalendarManager.instance.OnPhaseChanged += Apply;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && CalendarManager.instance != null)
            CalendarManager.instance.OnPhaseChanged -= Apply;
        subscribed = false;
    }

    private void Apply(DayPhase phase)
    {
        foreach (ToggleEntry entry in entries)
        {
            if (entry.target == null)
            {
                Debug.LogWarning($"[TimeOfDayObjectToggler] Entry '{entry.label}' has no Target assigned - skipping.");
                continue;
            }

            if (entry.activeDuring.Count == 0)
            {
                Debug.LogWarning($"[TimeOfDayObjectToggler] Entry '{entry.label}' has an empty " +
                                  "Active During list - it will be switched OFF in every phase.");
            }

            entry.target.SetActive(entry.activeDuring.Contains(phase));
        }

        Debug.Log($"[TimeOfDayObjectToggler] Applied {phase} to {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}.");
    }
}