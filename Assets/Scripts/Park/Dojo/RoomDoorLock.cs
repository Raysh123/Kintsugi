using UnityEngine;

/// <summary>
/// Swaps between two pre-built door objects — a closed door (blocks the doorway,
/// has its own collider) while locked, and an open door (passable, no blocking
/// collider) once unlocked. Same swap-a-whole-prebuilt-object pattern used
/// elsewhere in this project (e.g. ParkActivity's runningActivityModel).
///
/// Call Unlock() from wherever mom's dialogue finishes (e.g. right after
/// `yield return new WaitUntil(() => !momDialogue.IsRunning);` in her interaction
/// script) — this script doesn't know or care how that dialogue is triggered, it
/// just needs the one call once it's done.
/// </summary>
public class RoomDoorLock : MonoBehaviour
{
    [Tooltip("Shown while locked — a closed door model with its own blocking collider.")]
    [SerializeField] private GameObject closedDoorObject;

    [Tooltip("Shown while unlocked — an open/passable door model, typically no blocking collider.")]
    [SerializeField] private GameObject openDoorObject;

    [Tooltip("Starts locked by default — untick if this door should start open and only " +
             "lock later for some other reason.")]
    [SerializeField] private bool startsLocked = true;

    public bool IsLocked { get; private set; }

    private void Awake()
    {
        SetLocked(startsLocked);
    }

    public void Unlock()
    {
        Debug.Log($"[RoomDoorLock] '{gameObject.name}' unlocked.");
        SetLocked(false);
    }

    public void Lock()
    {
        SetLocked(true);
    }

    private void SetLocked(bool locked)
    {
        IsLocked = locked;

        if (closedDoorObject != null) closedDoorObject.SetActive(locked);
        if (openDoorObject != null) openDoorObject.SetActive(!locked);
    }
}