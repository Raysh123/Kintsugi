using UnityEngine;

/// <summary>
/// A door the player interacts with to open the travel map. Just a trigger +
/// E-press wrapper around TravelMapController.OpenMap() — all the actual
/// unlock/cycling/teleport logic lives there.
/// </summary>
public class DoorInteraction : MonoBehaviour
{
    [SerializeField] private TravelMapController travelMapController;
    [Tooltip("Optional prompt — a world space UI that says 'Press E to travel' etc.")]
    [SerializeField] private GameObject interactPrompt;
    [Tooltip("Safety net: even if playerInRange is somehow stuck true (e.g. a missed " +
             "OnTriggerExit), the door won't respond unless the player is genuinely within " +
             "this distance. Set generously larger than the trigger collider itself.")]
    [SerializeField] private float maxInteractDistance = 3f;

    private Transform playerTransform;
    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        playerTransform = other.transform;
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
        if (!playerInRange || travelMapController == null) return;

        if (playerTransform != null &&
            Vector3.Distance(playerTransform.position, transform.position) > maxInteractDistance)
        {
            // playerInRange is stuck true (a missed OnTriggerExit) — correct it here
            // rather than letting every button press anywhere trigger this door.
            playerInRange = false;
            if (interactPrompt != null) interactPrompt.SetActive(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            travelMapController.OpenMap();
    }
}