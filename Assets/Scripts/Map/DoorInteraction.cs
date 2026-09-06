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
        if (!playerInRange || travelMapController == null) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            travelMapController.OpenMap();
    }
}