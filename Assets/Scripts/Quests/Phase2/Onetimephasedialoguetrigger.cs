using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds any number of independent "fire this dialogue automatically, exactly
/// once" slots — one component can cover every unprompted story beat across
/// the whole game, rather than needing a separate GameObject or component per
/// trigger. Each slot tracks its own fired state independently.
///
/// Two matching modes per slot:
///   Fire On Phase Entry (recommended default) — fires once the story enters
///     Required Phase AND the player has genuinely finished waking up (waits
///     for BedInteraction.OnAnyWakeUp, since phase transitions happen
///     synchronously mid-sleep, well before the fade-in/wake animation
///     actually finish). Ignores time of day and day number entirely.
///     Completely unaffected by how long any earlier phase actually lasts.
///   Day/Time matching (Fire On Phase Entry off) — fires on a specific
///     StoryPhase + DayPhase (time of day) + day-number-since-phase-started
///     combination, e.g. "Night of Day 2." More precise, but depends on
///     day-count math staying correct as phase durations change.
/// </summary>
public class OneTimePhaseDialogueTrigger : MonoBehaviour
{
    [System.Serializable]
    public class TriggerSlot
    {
        [Tooltip("Inspector-only label, e.g. 'HouseTraining welcome'. Not used at runtime.")]
        public string label;
        public StoryPhase requiredPhase;

        [Tooltip("If true, fires the instant the story enters Required Phase — ignores time of day " +
                 "and day number entirely below. Robust to phase durations changing, since it doesn't " +
                 "depend on any day-count math at all. If false, uses the more specific Time Of Day + " +
                 "Day Number matching instead.")]
        public bool fireOnPhaseEntry = true;

        [Header("Used only if Fire On Phase Entry is OFF")]
        public DayPhase requiredTimeOfDay;
        [Tooltip("Only fires on this specific day count since Required Phase began. Set to 0 to fire " +
                 "on any day, as long as phase/time of day still match.")]
        public int requiredDayNumber = 1;

        public DialogueRunner dialogueRunner;

        [HideInInspector] public bool hasFired;
    }

    [SerializeField] private List<TriggerSlot> slots = new List<TriggerSlot>();
    private readonly List<TriggerSlot> pendingPhaseEntrySlots = new List<TriggerSlot>();

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (CalendarManager.instance != null)
            CalendarManager.instance.OnPhaseChanged -= HandleTimeOfDayChanged;
        if (StoryManager.instance != null)
            StoryManager.instance.OnPhaseChanged -= HandleStoryPhaseChanged;
        BedInteraction.OnAnyWakeUp -= HandleWakeUp;
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (CalendarManager.instance == null || StoryManager.instance == null)
            yield return null;

        CalendarManager.instance.OnPhaseChanged += HandleTimeOfDayChanged;
        StoryManager.instance.OnPhaseChanged += HandleStoryPhaseChanged;
        BedInteraction.OnAnyWakeUp += HandleWakeUp;
    }

    // ── Fire On Phase Entry mode ─────────────────────────────────────────────────

    private void HandleStoryPhaseChanged(StoryPhase old, StoryPhase newPhase)
    {
        foreach (TriggerSlot slot in slots)
        {
            if (slot.hasFired || !slot.fireOnPhaseEntry) continue;
            if (newPhase != slot.requiredPhase) continue;
            if (pendingPhaseEntrySlots.Contains(slot)) continue;

            // Don't fire yet — phase transitions happen synchronously mid-sleep,
            // well before the player has actually visually woken up. Queue it and
            // wait for BedInteraction.OnAnyWakeUp instead.
            pendingPhaseEntrySlots.Add(slot);
        }
    }

    private void HandleWakeUp()
    {
        if (pendingPhaseEntrySlots.Count == 0) return;

        foreach (TriggerSlot slot in pendingPhaseEntrySlots)
        {
            if (slot.hasFired) continue;

            if (slot.dialogueRunner == null)
            {
                Debug.LogWarning($"[DialogueTrigger] '{slot.label}': entered {slot.requiredPhase} but no " +
                                  "DialogueRunner is assigned — nothing to play.");
                continue;
            }

            slot.hasFired = true;
            Debug.Log($"[DialogueTrigger] '{slot.label}': firing after wake-up (phase entry into {slot.requiredPhase}).");
            slot.dialogueRunner.Show();
        }

        pendingPhaseEntrySlots.Clear();
    }

    // ── Day/Time matching mode ───────────────────────────────────────────────────

    private void HandleTimeOfDayChanged(DayPhase newTimeOfDay)
    {
        foreach (TriggerSlot slot in slots)
        {
            if (slot.hasFired || slot.fireOnPhaseEntry) continue;
            if (newTimeOfDay != slot.requiredTimeOfDay) continue;

            if (StoryManager.instance == null || StoryManager.instance.CurrentPhase != slot.requiredPhase)
            {
                Debug.Log($"[DialogueTrigger] '{slot.label}': time of day matched ({newTimeOfDay}) but " +
                          $"story phase didn't (need {slot.requiredPhase}, currently " +
                          $"{(StoryManager.instance != null ? StoryManager.instance.CurrentPhase.ToString() : "N/A")}).");
                continue;
            }

            if (slot.requiredDayNumber > 0 && StoryManager.instance.DaysSincePhaseStart != slot.requiredDayNumber)
            {
                Debug.Log($"[DialogueTrigger] '{slot.label}': phase and time of day matched but day number " +
                          $"didn't (need day {slot.requiredDayNumber}, currently day " +
                          $"{StoryManager.instance.DaysSincePhaseStart}).");
                continue;
            }

            if (slot.dialogueRunner == null)
            {
                Debug.LogWarning($"[DialogueTrigger] '{slot.label}': all conditions matched but no " +
                                  "DialogueRunner is assigned — nothing to play.");
                continue;
            }

            slot.hasFired = true;
            Debug.Log($"[DialogueTrigger] '{slot.label}': all conditions matched — firing now.");
            slot.dialogueRunner.Show();
        }
    }
}