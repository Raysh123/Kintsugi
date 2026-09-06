using System;
using UnityEngine;

/// <summary>
/// One asset per character. Holds all their spoken dialogue lines organised
/// by StoryPhase, and optionally further narrowed by time of day (DayPhase)
/// and day number since the current story phase began — so the SAME asset
/// can serve different lines for "BedRest, Day 1, Morning" vs "BedRest,
/// Day 2, Evening" etc, without needing a separate asset per variant.
///
/// GetBlock picks the MOST SPECIFIC matching block: a block that matches on
/// phase + time of day + day number beats one that only matches phase alone.
/// This lets you have one generic fallback block (any time, any day) plus
/// specific overrides for particular moments, without needing to author
/// every single combination.
///
/// Create via: Assets > Create > RPG > Character Dialogue
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterDialogue", menuName = "RPG/Character Dialogue")]
public class CharacterDialogue : ScriptableObject
{
    public string characterName;
    public DialogueBlock[] dialogueBlocks;

    /// <summary>
    /// Finds the most specific matching block for the given phase, time of day,
    /// and day number since that phase began. A block only "counts" as matching
    /// time of day / day number if it doesn't have Any Time Of Day / Any Day
    /// checked — those checked (the default) match regardless of the actual value.
    /// </summary>
    public DialogueBlock GetBlock(StoryPhase phase, DayPhase timeOfDay, int dayNumber)
    {
        DialogueBlock best = null;
        int bestSpecificity = -1;

        foreach (var block in dialogueBlocks)
        {
            if (block.phase != phase) continue;
            if (!block.anyTimeOfDay && block.timeOfDay != timeOfDay) continue;
            if (!block.anyDay && block.dayNumber != dayNumber) continue;

            int specificity = (block.anyTimeOfDay ? 0 : 1) + (block.anyDay ? 0 : 1);
            if (specificity > bestSpecificity)
            {
                bestSpecificity = specificity;
                best = block;
            }
        }

        return best;
    }

    /// <summary>Backward-compatible overload — matches on phase only, ignoring time of day/day number.</summary>
    public DialogueBlock GetBlock(StoryPhase phase)
    {
        foreach (var block in dialogueBlocks)
            if (block.phase == phase && block.anyTimeOfDay && block.anyDay) return block;
        return null;
    }
}

[Serializable]
public class DialogueBlock
{
    public StoryPhase phase;

    [Header("Time of Day (optional narrowing)")]
    [Tooltip("If true, this block matches regardless of time of day. Uncheck to restrict it to one specific quadrant.")]
    public bool anyTimeOfDay = true;
    public DayPhase timeOfDay;

    [Header("Day Number Since Phase Started (optional narrowing)")]
    [Tooltip("If true, this block matches on any day. Uncheck to restrict it to a specific day count " +
             "since the current story phase began — Day 1, Day 2, etc.")]
    public bool anyDay = true;
    public int dayNumber = 1;

    public DialogueLine[] lines;
}

[Serializable]
public class DialogueLine
{
    [Tooltip("The spoken audio clip for this line.")]
    public AudioClip audioClip;

    [Tooltip("Subtitle text shown on screen while the clip plays. " +
             "If no clip is assigned, subtitle stays until player advances manually.")]
    [TextArea(2, 4)]
    public string subtitle;

    [Tooltip("If true, player must press the advance key to continue - even if a " +
             "clip is assigned. If false, the next line plays automatically when " +
             "the clip finishes.")]
    public bool waitForInput = false;
}