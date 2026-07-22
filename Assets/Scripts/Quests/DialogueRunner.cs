using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays spoken dialogue and shows subtitles at the bottom of the screen.
/// No dialogue box — just subtitle text that appears while audio plays and
/// clears when the conversation ends.
///
/// Attach to any NPC or story trigger. Assign a CharacterDialogue asset.
/// Call Show() from whatever interaction or story event owns this character.
///
/// UI setup: one TMP_Text element anchored to the bottom of the screen
/// (subtitleText). No panel needed — the text itself shows/clears.
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    [Header("Dialogue Data")]
    [SerializeField] private CharacterDialogue dialogue;

    [Header("UI — Subtitles")]
    [Tooltip("TMP text anchored to the bottom of the screen. Always present in " +
             "the scene — this script just sets/clears its text.")]
    [SerializeField] private TMP_Text subtitleText;

    [Tooltip("Optional: name card that shows the speaking character's name.")]
    [SerializeField] private TMP_Text speakerNameText;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Settings")]
    [SerializeField] private KeyCode advanceKey = KeyCode.E;

    public bool IsRunning { get; private set; }

    /// Fired when all lines in the block have played.
    public event System.Action OnDialogueComplete;

    public void Show()
    {
        if (IsRunning) return;

        DialogueBlock block = dialogue?.GetBlock(StoryManager.instance.CurrentPhase);
        if (block == null || block.lines.Length == 0)
        {
            Debug.Log($"[Dialogue] No lines for {dialogue?.characterName} " +
                      $"in phase {StoryManager.instance.CurrentPhase}");
            return;
        }

        StartCoroutine(RunDialogue(block));
    }

    /// <summary>Play a specific block by phase directly — for tutorial triggers
    /// that need to fire outside of the normal story phase flow.</summary>
    public void ShowBlock(StoryPhase phase)
    {
        if (IsRunning) return;

        DialogueBlock block = dialogue?.GetBlock(phase);
        if (block == null || block.lines.Length == 0) return;

        StartCoroutine(RunDialogue(block));
    }

    private IEnumerator RunDialogue(DialogueBlock block)
    {
        IsRunning = true;

        if (speakerNameText != null)
            speakerNameText.text = dialogue.characterName;

        foreach (DialogueLine line in block.lines)
        {
            // Show subtitle
            if (subtitleText != null)
                subtitleText.text = line.subtitle;

            // Play audio clip if assigned
            if (line.audioClip != null && audioSource != null)
            {
                audioSource.clip = line.audioClip;
                audioSource.Play();

                if (line.waitForInput)
                {
                    // Wait for clip to finish OR player to advance, whichever is first
                    yield return new WaitUntil(() =>
                        !audioSource.isPlaying ||
                        Input.GetKeyDown(advanceKey) ||
                        Input.GetButtonDown("Submit"));
                    audioSource.Stop();
                }
                else
                {
                    // Auto-advance when clip finishes
                    yield return new WaitUntil(() => !audioSource.isPlaying);
                }
            }
            else
            {
                // No clip — wait for player input to advance
                yield return new WaitUntil(() =>
                    Input.GetKeyDown(advanceKey) ||
                    Input.GetButtonDown("Submit"));
            }

            yield return new WaitForSeconds(0.1f); // prevent double-advance
        }

        // Clear subtitles
        if (subtitleText != null) subtitleText.text = string.Empty;
        if (speakerNameText != null) speakerNameText.text = string.Empty;

        IsRunning = false;
        OnDialogueComplete?.Invoke();
    }
}