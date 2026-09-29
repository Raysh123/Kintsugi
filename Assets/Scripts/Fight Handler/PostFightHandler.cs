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
    }

    [Header("Player")]
    [SerializeField] private PlayerMovement player;

    [Header("Return Spawn Points")]
    [Tooltip("Match the 'id' here against the returnSpawnPointId set on each FightTrigger.")]
    [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    [Header("Post-Fight Dialogue")]
    [SerializeField] private DialogueRunner winDialogue;
    [SerializeField] private DialogueRunner loseDialogue;

    [Header("Time Cost")]
    [SerializeField] private int fightTimeCostTicks = 4;

    private void Start()
    {
        if (!FightSessionManager.hasPendingResult)
        {
            Debug.Log("[PostFightHandler] No pending fight result — normal scene load.");
            return;
        }

        StartCoroutine(HandleReturn());
    }

    private IEnumerator HandleReturn()
    {
        bool won = FightSessionManager.wonLastFight;
        string spawnId = FightSessionManager.returnSpawnPointId;

        Debug.Log($"[PostFightHandler] Returned from fight. Result: {(won ? "WIN" : "LOSS")}. Spawn id: '{spawnId}'.");

        // Consume immediately so this doesn't replay if the scene reloads again later.
        FightSessionManager.ClearPendingResult();

        Transform spawn = FindSpawn(spawnId);
        if (spawn != null)
            TeleportPlayer(spawn);
        else
            Debug.LogWarning($"[PostFightHandler] No spawn point found matching id '{spawnId}'.");

        if (player != null) player.movementLocked = true;

        DialogueRunner runner = won ? winDialogue : loseDialogue;
        if (runner != null)
        {
            runner.Show();
            yield return new WaitUntil(() => !runner.IsRunning);
        }
        else
        {
            Debug.LogWarning($"[PostFightHandler] No {(won ? "win" : "lose")} dialogue assigned.");
        }

        if (player != null) player.movementLocked = false;

        if (CalendarManager.instance != null)
            CalendarManager.instance.AdvanceTime(fightTimeCostTicks);
        else
            Debug.LogWarning("[PostFightHandler] CalendarManager.instance is null — time not advanced.");
    }

    private Transform FindSpawn(string id)
    {
        foreach (SpawnPoint sp in spawnPoints)
            if (sp.id == id) return sp.transform;
        return null;
    }

    private void TeleportPlayer(Transform destination)
    {
        if (player == null || destination == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        if (controller != null) controller.enabled = true;
    }
}
