using UnityEngine;

/// <summary>
/// Global safety net: put ONE on the player. Whenever any dialogue or conversation finishes, it waits
/// 'graceSeconds'; if no new dialogue has started and nothing listed below is open, it force-clears
/// the player's movement lock (and cutscene lock), so a sequence script that forgot to unlock - or was
/// interrupted by a fast skip or a scene change - can never leave the player stuck.
/// </summary>
public class PlayerLockGuard : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;

    [Tooltip("Seconds after the last dialogue ends before the lock is force-cleared. Long enough for scripted " +
             "fades / follow-up steps, short enough that being stuck is barely noticeable.")]
    [SerializeField] private float graceSeconds = 2f;

    [Tooltip("While ANY of these is active the lock is left alone (panels that legitimately keep the player " +
             "still after a dialogue: injury panel, fight selection menu, travel map, intro panel...).")]
    [SerializeField] private GameObject[] suppressWhileActive;

    [Tooltip("Also clear 'interacting'. Leave OFF unless it gets stuck too - it is legitimately true during the cat, recovery and bed activities.")]
    [SerializeField] private bool alsoClearInteracting = false;

    private bool wasActive;
    private float idleTimer;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (player == null) return;

        bool active = ActiveDialogueTracker.AnyActive;
        if (active) { wasActive = true; idleTimer = 0f; return; }
        if (!wasActive) return;

        idleTimer += Time.unscaledDeltaTime;
        if (idleTimer < graceSeconds) return;

        wasActive = false;
        idleTimer = 0f;

        if (Time.timeScale == 0f) return;
        if (StoryManager.instance != null && StoryManager.instance.CurrentPhase == StoryPhase.BedRest) return;
        if (suppressWhileActive != null)
            foreach (GameObject g in suppressWhileActive)
                if (g != null && g.activeInHierarchy) return;

        player.movementLocked = false;
        player.cutsceneLocked = false;
        if (alsoClearInteracting) player.interacting = false;
    }
}
