using UnityEngine;

/// <summary>
/// Safety net for the player ending up somewhere they shouldn't after a teleport (travel, park bench, post-fight).
/// Put this on the player (the object with the CharacterController).
///
/// Every teleport calls PlayerFallSafety.MarkSafe(destination position, rotation) with the INTENDED spot.
///  - For a short time afterwards, if the player isn't where they were meant to land, they are put there.
///  - Afterwards, if they ever drop further than Fall Distance below that spot (or below the last place they stood
///    on solid ground), they are put back on the last safe spot.
/// </summary>
public class PlayerFallSafety : MonoBehaviour
{
    [Tooltip("How far below the last safe spot the player can be before they are put back.")]
    [SerializeField] private float fallDistance = 6f;
    [Tooltip("An absolute floor: anything below this height is always rescued. Leave very low to ignore.")]
    [SerializeField] private float absoluteMinY = -200f;
    [Tooltip("How often (seconds) the last safe spot is updated while grounded.")]
    [SerializeField] private float recordInterval = 0.25f;
    [Tooltip("For this many seconds after a teleport the arrival spot is double-checked.")]
    [SerializeField] private float verifyTime = 0.5f;
    [Tooltip("How far from the intended spot (sideways) counts as a bad arrival.")]
    [SerializeField] private float verifyDistance = 2f;

    private CharacterController controller;
    private Vector3 lastSafe;
    private Quaternion lastSafeRotation;
    private float nextRecord;
    private float verifyUntil;

    private static PlayerFallSafety instance;

    private void Awake()
    {
        instance = this;
        controller = GetComponent<CharacterController>();
        lastSafe = transform.position;
        lastSafeRotation = transform.rotation;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Call right after teleporting the player, with the position they were MEANT to arrive at.</summary>
    public static void MarkSafe(Vector3 position, Quaternion rotation)
    {
        if (instance == null) return;
        instance.lastSafe = position;
        instance.lastSafeRotation = rotation;
        instance.nextRecord = Time.time + 1f;
        instance.verifyUntil = Time.time + instance.verifyTime;
    }

    private void LateUpdate()
    {
        Vector3 p = transform.position;

        if (Time.time < verifyUntil)
        {
            Vector3 off = p - lastSafe;
            off.y = 0f;
            if (off.magnitude > verifyDistance) { Rescue(); return; }
        }

        if (p.y < lastSafe.y - fallDistance || p.y < absoluteMinY)
        {
            Rescue();
            return;
        }

        if (Time.time >= nextRecord && controller != null && controller.isGrounded)
        {
            lastSafe = p;
            lastSafeRotation = transform.rotation;
            nextRecord = Time.time + recordInterval;
        }
    }

    private void Rescue()
    {
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;

        transform.position = lastSafe + Vector3.up * 0.1f;
        transform.rotation = lastSafeRotation;
        Physics.SyncTransforms();

        if (controller != null) controller.enabled = wasEnabled;
        verifyUntil = 0f;
    }
}
