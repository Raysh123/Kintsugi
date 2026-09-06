using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays a ConversationDialogue asset. Holds the scene-side speaker list
/// (display name + their own AudioSource) that the asset's lines reference
/// by index — this is the multi-character counterpart to DialogueRunner,
/// which only handles a single speaker with one shared AudioSource.
///
/// Attach to any object, assign speakers in the same order the conversation
/// asset expects (index 0 = first speaker, etc), and call Show().
/// </summary>
public class ConversationRunner : MonoBehaviour
{
    [System.Serializable]
    public class ConversationSpeaker
    {
        [Tooltip("Shown in the speaker name UI while this character is talking.")]
        public string speakerName;
        [Tooltip("This speaker's own AudioSource — e.g. positioned at their model for spatial audio.")]
        public AudioSource audioSource;
    }

    [Header("Conversation Data")]
    [SerializeField] private ConversationDialogue conversation;

    [Header("Speakers (index must match speakerIndex on each line in the asset)")]
    [SerializeField] private List<ConversationSpeaker> speakers = new List<ConversationSpeaker>();

    [Header("UI — Subtitles")]
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text speakerNameText;

    [Header("Settings")]
    [SerializeField] private KeyCode advanceKey = KeyCode.E;

    public bool IsRunning { get; private set; }
    public event System.Action OnDialogueComplete;

    public void Show()
    {
        if (IsRunning) return;

        StoryPhase phase = StoryManager.instance.CurrentPhase;
        DayPhase timeOfDay = CalendarManager.instance != null ? CalendarManager.instance.CurrentPhase : DayPhase.Morning;
        int dayNumber = StoryManager.instance.DaysSincePhaseStart;

        ConversationBlock block = conversation?.GetBlock(phase, timeOfDay, dayNumber);
        if (block == null || block.lines.Length == 0)
        {
            Debug.Log($"[Conversation] No lines for {name} in phase {phase} " +
                      $"(time of day: {timeOfDay}, day {dayNumber})");
            return;
        }

        StartCoroutine(RunConversation(block));
    }

    private IEnumerator RunConversation(ConversationBlock block)
    {
        IsRunning = true;

        // Wait one frame before any input checking begins — otherwise the same
        // button press that triggered Show() immediately satisfies the first
        // line's WaitUntil, skipping it instantly.
        yield return null;

        foreach (ConversationLine line in block.lines)
        {
            ConversationSpeaker speaker = (line.speakerIndex >= 0 && line.speakerIndex < speakers.Count)
                ? speakers[line.speakerIndex]
                : null;

            if (speaker == null)
                Debug.LogWarning($"{name}: line references speakerIndex {line.speakerIndex}, " +
                                  $"but only {speakers.Count} speakers are configured.");

            if (speakerNameText != null)
                speakerNameText.text = speaker != null ? speaker.speakerName : string.Empty;

            if (subtitleText != null)
                subtitleText.text = line.subtitle;

            AudioSource source = speaker?.audioSource;

            if (line.audioClip != null && source != null)
            {
                source.clip = line.audioClip;
                source.Play();

                if (line.waitForInput)
                {
                    // The clip (if any) is purely decorative here — a short "noise" blip
                    // shouldn't affect pacing. Advancing depends only on player input.
                    yield return new WaitUntil(() =>
                        Input.GetKeyDown(advanceKey) ||
                        SkipInput.Pressed());
                    if (source.isPlaying) source.Stop();
                }
                else
                {
                    // Auto-advance when clip finishes — for full voiced lines later.
                    yield return new WaitUntil(() => !source.isPlaying);
                }
            }
            else
            {
                // No clip (or no valid speaker AudioSource) - wait for player input to advance
                yield return new WaitUntil(() =>
                    Input.GetKeyDown(advanceKey) ||
                    SkipInput.Pressed());
            }

            yield return new WaitForSeconds(0.1f); // prevent double-advance
        }

        if (subtitleText != null) subtitleText.text = string.Empty;
        if (speakerNameText != null) speakerNameText.text = string.Empty;

        IsRunning = false;
        OnDialogueComplete?.Invoke();
    }
}