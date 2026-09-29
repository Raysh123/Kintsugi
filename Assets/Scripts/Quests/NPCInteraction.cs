using UnityEngine;

/// <summary>
/// Attach to any NPC alongside a DialogueRunner and a trigger collider.
/// When the player walks into range and presses E, the dialogue plays.
/// Wire OnDialogueComplete on the DialogueRunner in the Inspector to
/// StoryManager.instance.AdvancePhase() for conversations that move the story.
///
/// If this NPC also has an NPCPatrol component, movement pauses in place for
/// the duration of the conversation and resumes automatically once it ends —
/// pausing only happens if dialogue actually started (guards against pausing
/// forever if DialogueRunner silently has no valid block for the current phase).
/// </summary>
public class NPCInteraction : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [Tooltip("Optional prompt — a world space UI that says 'Press E to talk' etc.")]
    [SerializeField] private GameObject interactPrompt;
    [Tooltip("Optional — if assigned, patrolling pauses while this dialogue plays and resumes once it ends.")]
   // [SerializeField] private NPCPatrol npcPatrol;

    private bool playerInRange;

    private void OnEnable()
    {
        if (dialogueRunner != null)
            dialogueRunner.OnDialogueComplete += HandleDialogueComplete;
    }

    private void OnDisable()
    {
        if (dialogueRunner != null)
            dialogueRunner.OnDialogueComplete -= HandleDialogueComplete;
    }

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
        {
            dialogueRunner.Show();

            // Only pause if dialogue actually started — Show() sets IsRunning
            // synchronously, so this correctly skips pausing if there was no
            // valid block for the current phase (which would mean
            // OnDialogueComplete never fires, leaving the NPC stuck paused).
            //if (dialogueRunner.IsRunning && npcPatrol != null)
            //    npcPatrol.PauseForDialogue();
        }
    }

    private void HandleDialogueComplete()
    {
        //if (npcPatrol != null) npcPatrol.ResumeAfterDialogue();
    }
}