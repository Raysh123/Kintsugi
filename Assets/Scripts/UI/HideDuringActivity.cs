using UnityEngine;

/// <summary>
/// Put on any UI object (an icon, the calendar, a HUD group) to fade it out while the player is busy with an activity
/// (recovery activities, sleeping - anything that sets the player's 'interacting' flag - plus cat play and the park run)
/// and fade it back in afterwards. It uses a CanvasGroup (added automatically if missing), so the object itself stays active.
/// </summary>
public class HideDuringActivity : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [Tooltip("Hide while the player is in an activity (the 'interacting' flag).")]
    [SerializeField] private bool hideDuringActivity = true;
    [Tooltip("Also hide while any dialogue / conversation is on screen.")]
    [SerializeField] private bool hideDuringDialogue = false;
    [SerializeField] private float fadeTime = 0.25f;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();

        bool hide = (hideDuringActivity && ((player != null && player.interacting) ||
                                            CatPlayInteraction.SessionInProgress || ParkActivity.RunInProgress)) ||
                    (hideDuringDialogue && ActiveDialogueTracker.AnyActive);

        float target = hide ? 0f : 1f;
        group.alpha = fadeTime > 0f
            ? Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / fadeTime)
            : target;
        group.blocksRaycasts = !hide;
        group.interactable = !hide;
    }
}