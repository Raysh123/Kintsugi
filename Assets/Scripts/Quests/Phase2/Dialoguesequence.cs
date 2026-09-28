using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a list of DialogueRunners one after another — each Show() is called
/// only once the previous one fully finishes (same Show()+WaitUntil !IsRunning
/// pattern used everywhere else in this project). Lets a conversation
/// alternate between different characters' own DialogueRunners in a defined
/// order, without needing any changes to CharacterDialogue/DialogueRunner.
///
/// Call Play() to start. Fires OnSequenceComplete once every step has played.
/// </summary>
public class DialogueSequence : MonoBehaviour
{
    [Tooltip("Plays in order — each entry's Show() only fires after the previous one fully finishes.")]
    [SerializeField] private List<DialogueRunner> steps = new List<DialogueRunner>();

    public bool IsRunning { get; private set; }
    public event System.Action OnSequenceComplete;

    public void Play()
    {
        if (IsRunning) return;
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        IsRunning = true;

        foreach (DialogueRunner step in steps)
        {
            if (step == null) continue;

            step.Show();
            yield return new WaitUntil(() => !step.IsRunning);
        }

        IsRunning = false;
        OnSequenceComplete?.Invoke();
    }
}