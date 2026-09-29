using UnityEngine;

/// <summary>
/// Stationary NPC placement — no movement, no patrol, just instant teleport
/// between named positions. Appears at a specific spot for a story beat
/// (e.g. "Mom at the bedroom door"), then returns to wherever it normally is
/// once that beat is done.
/// </summary>
public class NPCAppearance : MonoBehaviour
{
    [Tooltip("Where this NPC normally is / returns to when not needed for a specific story beat.")]
    [SerializeField] private Transform defaultPosition;
    [Tooltip("If true, hides the NPC entirely when not needed instead of placing it at Default Position.")]
    [SerializeField] private bool hideWhenNotNeeded = false;

    private void Start()
    {
        ReturnToDefault();
    }

    /// <summary>Instantly places the NPC at the given position/rotation and ensures it's visible.</summary>
    public void AppearAt(Transform position)
    {
        if (position == null) return;

        gameObject.SetActive(true);
        transform.position = position.position;
        transform.rotation = position.rotation;
    }

    /// <summary>Returns to Default Position, or hides entirely if Hide When Not Needed is set.</summary>
    public void ReturnToDefault()
    {
        if (hideWhenNotNeeded)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        if (defaultPosition != null)
        {
            transform.position = defaultPosition.position;
            transform.rotation = defaultPosition.rotation;
        }
    }
}