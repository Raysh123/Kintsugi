using System;
using UnityEngine;

/// <summary>
/// One asset per character. Holds all their spoken dialogue lines organised
/// by StoryPhase. Each line pairs an audio clip (the spoken line) with a
/// subtitle string shown on screen while it plays.
///
/// Create via: Assets > Create > RPG > Character Dialogue
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterDialogue", menuName = "RPG/Character Dialogue")]
public class CharacterDialogue : ScriptableObject
{
    public string characterName;
    public DialogueBlock[] dialogueBlocks;

    public DialogueBlock GetBlock(StoryPhase phase)
    {
        foreach (var block in dialogueBlocks)
            if (block.phase == phase) return block;
        return null;
    }
}

[Serializable]
public class DialogueBlock
{
    public StoryPhase phase;
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

    [Tooltip("If true, player must press the advance key to continue — even if a " +
             "clip is assigned. If false, the next line plays automatically when " +
             "the clip finishes.")]
    public bool waitForInput = false;
}