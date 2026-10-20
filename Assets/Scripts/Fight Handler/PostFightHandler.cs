using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives in the RPG scene (e.g. on a manager object near the dojo). On scene load,
/// checks whether we just returned from a fight (FightSessionManager.hasPendingResult)
/// and if so: teleports the player to the right spawn point, plays a win/lose
/// dialogue, and advances calendar time to account for the match.
/// </summary>
public class PostFightHandler : MonoBehaviour
{
    [System.Serializable]
    public class SpawnPoint
    {
        public string id;
        public Transform transform;
        [Tooltip("Optional - if set, PlayerAppearanceController.SetModel(this) is called " +
                 "after teleporting here (e.g. \"Dojo\"). Leave blank to not touch appearance " +
                 "at all for this spawn point. This bypasses DojoArrivalHandler entirely - " +
                 "PostFightHandler teleports directly rather than going through " +
                 "TravelMapController/OnArrival, so nothing else would set it.")]
        public string modelId;
        [Tooltip("Optional - name of the RecoverySoundscape ambience area to switch to when " +
                 "returning here (e.g. \"Dojo\"). Blank = leave the ambience as it is.")]
        public string ambienceArea;
    }

    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private PlayerAppearanceController appearance;

    [Header("Return Spawn Points")]
    [Tooltip("Match the 'id' here against the returnSpawnPointId set on each FightTrigger.")]
    [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    [Header("Post-Fight Image Panels")]
    [Tooltip("Shown after a win INSTEAD of the win dialogue. Keep it inactive in the scene - it is switched on and " +
             "off here. Leave empty to use the win dialogue below.")]
    [SerializeField] private GameObject winPanel;
    [Tooltip("Shown after a loss INSTEAD of the lose dialogue. Keep it inactive in the scene. Leave empty to use the " +
             "lose dialogue below.")]
    [SerializeField] private GameObject losePanel;
    [Tooltip("Shown INSTEAD of the loss panel when the fight ended because the injury meter filled. Keep it inactive in " +
             "the scene. Leave empty to show the normal loss panel / dialogue for injuries too.")]
    [SerializeField] private GameObject injuredPanel;
    [Tooltip("The panel can't be dismissed for this long, so a button still held from the fight doesn't skip it.")]
    [SerializeField] private float panelMinTime = 0.8f;
    [Tooltip("Seconds before the panel closes by itself. 0 = wait for the player to press E / A / Submit.")]
    [SerializeField] private float panelAutoCloseSeconds = 0f;

    [Header("Post-Fight Dialogue (used only when no panel is assigned)")]
    [SerializeField] private DialogueRunner winDialogue;
    [SerializeField] private DialogueRunner loseDialogue;

    [Header("Time Cost")]
    [SerializeField] private int fightTimeCostTicks = 4;

    private void Start()
    {
        if (!FightSessionManager.hasPendingResult)
        {
            return;
        }

        StartCoroutine(HandleReturn());
    }

    private IEnumerator HandleReturn()
    {
        bool won = FightSessionManager.wonLastFight;
        string spawnId = FightSessionManager.returnSpawnPointId;


        // Consume immediately so this doesn't replay if the scene reloads again later.
        FightSessionManager.ClearPendingResult();

        SparringPartner partner = FightSelectionMenu.pendingPartner;
        FightSelectionMenu.pendingPartner = SparringPartner.None;

        SpawnPoint spawn = FindSpawn(spawnId);
        if (spawn != null)
        {
            TeleportPlayer(spawn.transform);
            if (!string.IsNullOrEmpty(spawn.ambienceArea))
                RecoverySoundscape.SetArea(spawn.ambienceArea);
            if (!string.IsNullOrEmpty(spawn.modelId) && appearance != null)
                appearance.SetModel(spawn.modelId);
        }

        if (player != null) player.movementLocked = true;

        GameObject panel = won ? winPanel : (FightSessionManager.injuredLastFight && injuredPanel != null ? injuredPanel : losePanel);
        DialogueRunner runner = won ? winDialogue : loseDialogue;
        if (panel != null)
        {
            yield return ShowPanel(panel);
        }
        else if (runner != null)
        {
            runner.Show();
            yield return new WaitUntil(() => !runner.IsRunning);
        }

        if (player != null) player.movementLocked = false;

        // Beat a sparring partner: record it AFTER their win dialogue. If that was the second one,
        // StoryManager jumps straight to the ending (EndGameController takes it from there).
        if (won && partner != SparringPartner.None)
        {
            if (StoryManager.instance != null)
                StoryManager.instance.RegisterPartnerDefeated(partner == SparringPartner.PartnerA);
        }

        if (CalendarManager.instance != null)
            CalendarManager.instance.AdvanceTime(fightTimeCostTicks);
    }

    /// <summary>Shows an image panel, then closes it on the interact button (or after the auto-close time).</summary>
    private IEnumerator ShowPanel(GameObject panel)
    {
        ActiveDialogueTracker.Begin();
        panel.SetActive(true);

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;

            if (panelAutoCloseSeconds > 0f && elapsed >= panelAutoCloseSeconds) break;

            if (elapsed >= panelMinTime &&
                (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit")))
                break;

            yield return null;
        }

        panel.SetActive(false);
        ActiveDialogueTracker.End();
    }

    private SpawnPoint FindSpawn(string id)
    {
        foreach (SpawnPoint sp in spawnPoints)
            if (sp.id == id) return sp;
        return null;
    }

    private void TeleportPlayer(Transform destination)
    {
        if (player == null || destination == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        Physics.SyncTransforms();
        if (controller != null) controller.enabled = true;
        PlayerFallSafety.MarkSafe(destination.position, destination.rotation);
    }
}