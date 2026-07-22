using UnityEngine;

/// <summary>
/// Attach to any NPC alongside a DialogueRunner and a trigger collider.
/// When the player walks into range and presses E, the dialogue plays.
/// Wire OnDialogueComplete on the DialogueRunner in the Inspector to
/// StoryManager.instance.AdvancePhase() for conversations that move the story.
/// </summary>
public class NPCInteraction : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [Tooltip("Optional prompt — a world space UI that says 'Press E to talk' etc.")]
    [SerializeField] private GameObject interactPrompt;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        if (interactPrompt != null) interactPrompt.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (!playerInRange || dialogueRunner.IsRunning) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit"))
            dialogueRunner.Show();
    }
}