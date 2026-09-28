using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays spoken dialogue and shows subtitles at the bottom of the screen.
/// No dialogue box � just subtitle text that appears while audio plays and
/// clears when the conversation ends.
///
/// Attach to any NPC or story trigger. Assign a CharacterDialogue asset.
/// Call Show() from whatever interaction or story event owns this character.
///
/// UI setup: one TMP_Text element anchored to the bottom of the screen
/// (subtitleText). No panel needed � the text itself shows/clears.
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    [Header("Dialogue Data")]
    [SerializeField] private CharacterDialogue dialogue;

    [Header("UI � Subtitles")]
    [Tooltip("TMP text anchored to the bottom of the screen. Always present in " +
             "the scene � this script just sets/clears its text.")]
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

        StoryPhase phase = StoryManager.instance.CurrentPhase;
        DayPhase timeOfDay = CalendarManager.instance != null ? CalendarManager.instance.CurrentPhase : DayPhase.Morning;
        int dayNumber = StoryManager.instance.DaysSincePhaseStart;

        DialogueBlock block = dialogue?.GetBlock(phase, timeOfDay, dayNumber);
        if (block == null || block.lines.Length == 0)
        {
            Debug.Log($"[Dialogue] No lines for {dialogue?.characterName} " +
                      $"in phase {phase} (time of day: {timeOfDay}, day {dayNumber})");
            return;
        }

        StartCoroutine(RunDialogue(block));
    }

    /// <summary>Play a specific block by phase directly � for tutorial triggers
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
        ActiveDialogueTracker.Begin();

        // Wait one frame before any input checking begins — otherwise the same
        // button press that triggered Show() (still "down" for this whole frame)
        // immediately satisfies the first line's WaitUntil, skipping it instantly.
        yield return null;

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
                    // The clip (if any) is purely decorative here — a short "noise" blip
                    // shouldn't affect pacing. Advancing depends only on player input,
                    // regardless of how quickly the clip itself finishes.
                    yield return new WaitUntil(() =>
                        Input.GetKeyDown(advanceKey) ||
                        SkipInput.Pressed());
                    if (audioSource.isPlaying) audioSource.Stop();
                }
                else
                {
                    // Auto-advance when clip finishes — for full voiced lines later.
                    yield return new WaitUntil(() => !audioSource.isPlaying);
                }
            }
            else
            {
                // No clip � wait for player input to advance
                yield return new WaitUntil(() =>
                    Input.GetKeyDown(advanceKey) ||
                    SkipInput.Pressed());
            }

            yield return new WaitForSeconds(0.1f); // prevent double-advance
        }

        // Clear subtitles
        if (subtitleText != null) subtitleText.text = string.Empty;
        if (speakerNameText != null) speakerNameText.text = string.Empty;

        IsRunning = false;
        ActiveDialogueTracker.End();
        OnDialogueComplete?.Invoke();
    }
}