using UnityEngine;

/// <summary>
/// A door that opens the travel map when the player walks into its trigger - no button
/// press. Just a trigger wrapper around TravelMapController.OpenMap(); all the actual
/// unlock/cycling/teleport logic lives there.
///
/// Walking in fires it ONCE per visit: after it fires (or is deliberately ignored, see below)
/// the door is "disarmed" and only re-arms once the player has left the trigger (or walked
/// further than Rearm Distance away, as a safety net for a missed OnTriggerExit). So
/// cancelling the map while still standing in the doorway doesn't instantly reopen it - the
/// player has to step out and back in.
///
/// Entering the trigger while movement is locked or a dialogue is running is ignored (and
/// still disarms the door). That matters because travel teleports the player: if a
/// destination's spawn point sits inside a door's trigger, arriving there would otherwise
/// pop the map straight back open.
/// </summary>
public class DoorInteraction : MonoBehaviour
{
    [SerializeField] private TravelMapController travelMapController;

    [Tooltip("Safety net: if OnTriggerExit is ever missed (it can be, when the player is " +
             "teleported), the door still re-arms once the player is this far away. Set " +
             "generously larger than the trigger collider itself.")]
    [SerializeField] private float rearmDistance = 3f;

    private Transform playerTransform;
    private bool armed = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerTransform = other.transform;

        if (!armed) return;
        armed = false; // disarm regardless of whether we open below - must leave to re-arm

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        bool playerBusy = (player != null && player.movementLocked) || ActiveDialogueTracker.AnyActive;
        if (playerBusy || travelMapController == null) return;

        travelMapController.OpenMap();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        armed = true;
    }

    private void Update()
    {
        if (armed || playerTransform == null) return;

        // Missed OnTriggerExit safety net.
        if (Vector3.Distance(playerTransform.position, transform.position) > rearmDistance)
            armed = true;
    }
}