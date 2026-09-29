using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A persistent (DontDestroyOnLoad) store of simple string flags — for one-off
/// "has this already happened" state that isn't naturally tied to StoryPhase/
/// CalendarManager, and needs to survive a scene reload (e.g. going into the
/// fight scene and back via FightSessionManager).
///
/// Non-persistent scene objects like ParkManager get destroyed and recreated on
/// every scene reload, so any state stored directly on them (a plain bool field)
/// resets to default each time. Route that state through here instead: read it
/// back in Start()/Awake() rather than trusting a local field to still be true.
///
/// For anything that's already naturally represented by StoryPhase or
/// CalendarManager (e.g. "have I joined the dojo" == "CurrentPhase >= NewDojoJoin"),
/// prefer checking that directly instead of adding a redundant flag here.
/// </summary>
public class PersistentGameFlags : MonoBehaviour
{
    public static PersistentGameFlags instance;

    private readonly HashSet<string> flags = new HashSet<string>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool HasFlag(string id) => flags.Contains(id);

    public void SetFlag(string id)
    {
        flags.Add(id);
    }

    public void ClearFlag(string id)
    {
        flags.Remove(id);
    }
}