using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic entry point for playing sounds from anything event-driven: Animation Events
/// (Animator clip events can call these methods directly, with a string parameter where
/// noted), UnityEvents wired up in the Inspector, or a plain method call from another
/// script. Put this on whatever GameObject the trigger source lives on — for animation
/// events, that's the same object as the Animator.
///
/// All three playback modes:
///   - PlayOnce(id) / PlaySound(id): fires a single play-through, e.g. a footstep or punch
///     impact. Set onlyPlayOnce to make it fire at most once ever (uses PersistentGameFlags,
///     same pattern as MorningDojoTransition/ParkActivity's one-time beats) — e.g. a
///     discovery stinger that shouldn't repeat even if the event re-fires.
///   - StartLoop(id) / StopLoop(id): starts a sound looping and keeps it looping until
///     explicitly stopped — e.g. an animation state's "enter"/"exit" events bracketing a
///     looping effort/breathing sound, or a UnityEvent pair on a toggle. Calling StartLoop
///     again while already looping is a no-op; calling StopLoop when nothing is looping is
///     also a no-op — both are safe to call speculatively.
///   - PlayDefaultSound / StartDefaultLoop / StopDefaultLoop: no-argument versions of the
///     above, using defaultSoundId — for Animation Events that can't pass a parameter.
/// </summary>
public class AudioEventTrigger : MonoBehaviour
{
    [Tooltip("If true, sounds play positionally at this object's current position (only " +
             "audible as positional if the SoundDefinition's spatialBlend > 0). If false, " +
             "plays as a flat 2D sound.")]
    [SerializeField] private bool playAtThisPosition = false;

    [Tooltip("Sound id used by the no-argument methods (PlayDefaultSound, StartDefaultLoop, " +
             "StopDefaultLoop) — for Animation Events that can't pass a string parameter.")]
    [SerializeField] private string defaultSoundId;

    [Range(0f, 2f)] [SerializeField] private float volumeScale = 1f;

    [Header("Loop fades (StartLoop / StopLoop only)")]
    [SerializeField] private float loopFadeInTime = 0f;
    [SerializeField] private float loopFadeOutTime = 0f;

    [Header("Only-once gating (PlayOnce / PlaySound / PlayDefaultSound only)")]
    [Tooltip("If true, this only ever plays once per game — later calls are ignored. Uses " +
             "PersistentGameFlags, same as MorningDojoTransition/ParkActivity's one-time beats.")]
    [SerializeField] private bool onlyPlayOnce = false;
    [Tooltip("Leave empty to auto-generate one from this component + the sound id.")]
    [SerializeField] private string onceFlagId;

    private readonly HashSet<string> activeLoopSoundIds = new HashSet<string>();

    private Vector3? PositionOverride => playAtThisPosition ? transform.position : (Vector3?)null;

    /// <summary>A loop key unique to this component + sound, so StartLoop/StopLoop calls with
    /// the same soundId naturally target the same underlying AudioSource.</summary>
    private string LoopKey(string soundId) => $"{GetInstanceID()}_{soundId}";

    private string FlagId(string soundId) =>
        string.IsNullOrEmpty(onceFlagId) ? $"AudioEventTrigger_{GetInstanceID()}_{soundId}_played" : onceFlagId;

    // ---------------- One-shot ----------------

    /// <summary>Call from an Animation Event (string parameter = sound id), a UnityEvent, or code.</summary>
    public void PlaySound(string soundId) => PlayOnce(soundId);

    /// <summary>Plays soundId exactly once (per call, or ever — see onlyPlayOnce).</summary>
    public void PlayOnce(string soundId)
    {
        if (onlyPlayOnce)
        {
            if (PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(FlagId(soundId)))
                return;

            if (PersistentGameFlags.instance != null)
                PersistentGameFlags.instance.SetFlag(FlagId(soundId));
            else
                Debug.LogWarning("[AudioEventTrigger] onlyPlayOnce is set but PersistentGameFlags.instance " +
                                  "is null — this won't be remembered across scene reloads.");
        }

        if (AudioManager.instance == null)
        {
            Debug.LogWarning("[AudioEventTrigger] AudioManager.instance is null.");
            return;
        }

        AudioManager.instance.PlaySFX(soundId, PositionOverride, volumeScale);
    }

    /// <summary>No-argument variant of PlayOnce, using defaultSoundId.</summary>
    public void PlayDefaultSound() => PlayOnce(defaultSoundId);

    // ---------------- Loop (start while something is happening, stop when it ends) ----------------

    /// <summary>Starts soundId looping. Call StopLoop with the same id to end it — e.g. from
    /// an animation state's exit event, or whatever moment marks the condition as over.</summary>
    public void StartLoop(string soundId)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogWarning("[AudioEventTrigger] AudioManager.instance is null.");
            return;
        }

        AudioManager.instance.PlayLoopingSFX(LoopKey(soundId), soundId, PositionOverride, volumeScale, loopFadeInTime);
        activeLoopSoundIds.Add(soundId);
    }

    /// <summary>Stops the loop started by StartLoop with the same id. Safe to call even if no
    /// loop is currently active.</summary>
    public void StopLoop(string soundId)
    {
        AudioManager.instance?.StopLoopingSFX(LoopKey(soundId), loopFadeOutTime);
        activeLoopSoundIds.Remove(soundId);
    }

    /// <summary>No-argument variants using defaultSoundId — for Animation Events.</summary>
    public void StartDefaultLoop() => StartLoop(defaultSoundId);
    public void StopDefaultLoop() => StopLoop(defaultSoundId);

    private void OnDisable()
    {
        // Safety net: don't leave an orphaned loop playing if this object is disabled or
        // destroyed mid-loop (e.g. pooled objects, scene unload) without a matching StopLoop.
        foreach (string soundId in activeLoopSoundIds)
            AudioManager.instance?.StopLoopingSFX(LoopKey(soundId), 0f);

        activeLoopSoundIds.Clear();
    }
}
