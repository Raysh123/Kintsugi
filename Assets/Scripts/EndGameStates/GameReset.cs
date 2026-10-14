using UnityEngine;

/// <summary>
/// Wipes the game's persistent state so the next run starts from the beginning. The singletons
/// (StoryManager, HealthAndMotivation, CalendarManager, PersistentGameFlags) live in
/// DontDestroyOnLoad, so a scene load alone would carry the old run over into the new one.
/// This destroys them; the first gameplay scene recreates them fresh when it loads.
///
/// Matches them by class NAME, so this file compiles whatever those classes look like. Add any
/// other persistent object's class name to the list below.
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
        int destroyed = 0;
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            foreach (string wanted in persistentTypeNames)
            {
                if (typeName != wanted) continue;
                Debug.Log($"[GameReset] Destroying persistent '{typeName}' on '{mb.gameObject.name}'.");
                Object.Destroy(mb.gameObject);
                destroyed++;
                break;
            }
        }
        Debug.Log($"[GameReset] Reset done ({destroyed} persistent object(s) removed).");
    }
}
