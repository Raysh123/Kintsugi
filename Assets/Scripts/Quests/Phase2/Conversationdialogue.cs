using System;
using UnityEngine;

/// <summary>
/// A multi-character conversation asset — sits alongside CharacterDialogue,
/// used specifically for scenes with real back-and-forth between two or more
/// speakers, each with their own AudioSource (e.g. positioned at their own
/// model in 3D space).
///
/// Lines reference a speaker by INDEX rather than by name — the actual
/// speaker list (display name + AudioSource) lives on ConversationRunner in
/// the scene, since ScriptableObjects can't hold scene object references.
/// Index N on a line here must match index N in that runner's Speakers list.
///
/// Blocks use the same phase/time-of-day/day-number "most specific match
/// wins" system as CharacterDialogue.
///
/// Create via: Assets > Create > RPG > Conversation Dialogue
/// </summary>
[CreateAssetMenu(fileName = "NewConversation", menuName = "RPG/Conversation Dialogue")]
public class ConversationDialogue : ScriptableObject
{
    public ConversationBlock[] blocks;

    public ConversationBlock GetBlock(StoryPhase phase, DayPhase timeOfDay, int dayNumber)
    {
        ConversationBlock best = null;
        int bestSpecificity = -1;

        foreach (var block in blocks)
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
}

[Serializable]
public class ConversationBlock
{
    public StoryPhase phase;

    [Header("Time of Day (optional narrowing)")]
    public bool anyTimeOfDay = true;
    public DayPhase timeOfDay;

    [Header("Day Number Since Phase Started (optional narrowing)")]
    public bool anyDay = true;
    public int dayNumber = 1;

    public ConversationLine[] lines;
}

[Serializable]
public class ConversationLine
{
    [Tooltip("Which speaker slot says this line — must match an index in the ConversationRunner's " +
             "Speakers list in the scene (0 = first speaker, 1 = second, etc).")]
    public int speakerIndex;

    [TextArea(2, 4)]
    public string subtitle;

    [Tooltip("Plays on this line's speaker's own AudioSource, not a shared one.")]
    public AudioClip audioClip;

    [Tooltip("If true, player must press the advance key to continue - even if a " +
             "clip is assigned. If false, the next line plays automatically when " +
             "the clip finishes.")]
    public bool waitForInput = false;
}