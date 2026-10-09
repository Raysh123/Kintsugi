using UnityEngine;

/// <summary>Null-safe one-liners for playing sounds by id. An empty id or a missing
/// AudioManager just does nothing, so a half-set-up scene never throws.</summary>
public static class Sfx
{
    public static void Play(string id, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(id) || AudioManager.instance == null) return;
        AudioManager.instance.PlaySFX(id, null, volumeScale);
    }

    public static void StartLoop(string key, string id, float fadeIn = 0f, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(id) || AudioManager.instance == null) return;
        AudioManager.instance.PlayLoopingSFX(key, id, null, volumeScale, fadeIn);
    }

    public static void StopLoop(string key, float fadeOut = 0f)
    {
        if (AudioManager.instance == null) return;
        AudioManager.instance.StopLoopingSFX(key, fadeOut);
    }
}
