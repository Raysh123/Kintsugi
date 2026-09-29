using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A flat, inspector-editable list of SoundDefinitions, looked up by id at runtime via an
/// internally-built dictionary. Assign one of these to AudioManager. You can split sounds
/// across multiple libraries (e.g. per-scene) if the list gets unwieldy — AudioManager only
/// takes a single library reference right now, so combine what you need into one asset, or
/// extend AudioManager to accept several if that becomes worth doing.
/// </summary>
[CreateAssetMenu(fileName = "New Sound Library", menuName = "Audio/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    [SerializeField] private List<SoundDefinition> sounds = new List<SoundDefinition>();

    private Dictionary<string, SoundDefinition> lookup;

    private void BuildLookupIfNeeded()
    {
        if (lookup != null) return;

        lookup = new Dictionary<string, SoundDefinition>();
        foreach (SoundDefinition sound in sounds)
        {
            if (sound == null || string.IsNullOrEmpty(sound.id)) continue;

            if (lookup.ContainsKey(sound.id))
            {
                Debug.LogWarning($"[SoundLibrary] Duplicate sound id '{sound.id}' in {name} — " +
                                  "only the first one registered will be used.");
                continue;
            }

            lookup.Add(sound.id, sound);
        }
    }

    public SoundDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        BuildLookupIfNeeded();

        if (lookup.TryGetValue(id, out SoundDefinition sound)) return sound;

        Debug.LogWarning($"[SoundLibrary] No sound registered with id '{id}' in {name}.");
        return null;
    }

    /// <summary>
    /// Call if the asset's sound list is edited at runtime (e.g. via a custom editor tool)
    /// and the id -> SoundDefinition cache needs to be rebuilt on next Get(). Not needed for
    /// normal play — the cache is built once on first lookup.
    /// </summary>
    public void InvalidateCache() => lookup = null;
}
