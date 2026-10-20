using UnityEngine;

/// <summary>
/// Wipes the game's persistent state so the next run starts from the beginning: destroys the
/// DontDestroyOnLoad singletons (matched by class name) and clears the static state that
/// otherwise survives scene loads.
/// </summary>
public static class GameReset
{
    private static readonly string[] persistentTypeNames =
    {
        "StoryManager", "HealthAndMotivation", "CalendarManager", "PersistentGameFlags",
    };

    public static void ResetAll()
    {
        Time.timeScale = 1f;

        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            foreach (string wanted in persistentTypeNames)
            {
                if (typeName != wanted) continue;
                mb.gameObject.SetActive(false);
                Object.Destroy(mb.gameObject);
                break;
            }
        }

        ActiveDialogueTracker.Reset();
        FightSessionManager.ClearAll();
       // DojoChallengeFight.ResetStatics();
        RecoverySoundscape.ClearStatics();
        FightSelectionMenu.pendingPartner = SparringPartner.None;
    }
}