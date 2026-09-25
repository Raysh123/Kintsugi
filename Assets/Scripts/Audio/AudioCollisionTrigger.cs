using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a sound when this object collides with (or is entered by) something matching the
/// configured filters. Works with both physical collisions (OnCollisionEnter/Exit) and
/// trigger volumes (OnTriggerEnter/Exit) — both sets of callbacks are always active, so just
/// set this object's Collider to "Is Trigger" or not depending on which behaviour you want;
/// only the matching pair will ever actually fire.
///
/// Two playback modes:
///   - OneShot: plays soundId once per qualifying hit (respecting cooldown). Tick
///     onlyTriggerOnce for a hit that should only ever fire the first time (uses
///     PersistentGameFlags, same pattern as MorningDojoTransition/ParkActivity).
///   - LoopWhileTouching: starts soundId looping on the first qualifying contact and stops
///     it once nothing qualifying is touching anymore. Tracks multiple simultaneous contacts
///     (e.g. two colliders with the matching tag touching at once) so it doesn't stop early
///     when only one of them leaves.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AudioCollisionTrigger : MonoBehaviour
{
    public enum PlayMode { OneShot, LoopWhileTouching }

    [SerializeField] private PlayMode playMode = PlayMode.OneShot;
    [SerializeField] private string soundId;

    [Tooltip("Leave empty to match any tag.")]
    [SerializeField] private string requiredTag = "";

    [SerializeField] private bool playAtCollisionPosition = true;

    [Header("OneShot settings")]
    [Tooltip("Minimum relative impact speed (m/s) required to fire. Only applies to physical " +
             "collisions — trigger volumes have no impact speed, so this is ignored for " +
             "OnTriggerEnter hits.")]
    [SerializeField] private float minImpactSpeed = 0f;

    [Tooltip("Impact speed at which the sound reaches full volume, scaling linearly from " +
             "minImpactSpeed. 0 = no scaling, always full volume. Ignored for trigger hits.")]
    [SerializeField] private float maxImpactSpeedForFullVolume = 0f;

    [Tooltip("Minimum time between plays, so e.g. a rolling/rattling collider doesn't spam.")]
    [SerializeField] private float cooldown = 0.1f;

    [Tooltip("If true, this only ever fires once per game — uses PersistentGameFlags, same " +
             "as MorningDojoTransition/ParkActivity's one-time beats.")]
    [SerializeField] private bool onlyTriggerOnce = false;
    [Tooltip("Leave empty to auto-generate one from this component + the sound id.")]
    [SerializeField] private string onceFlagId;

    [Header("LoopWhileTouching settings")]
    [SerializeField] private float loopFadeInTime = 0f;
    [SerializeField] private float loopFadeOutTime = 0f;

    private float lastPlayTime = -999f;
    private readonly HashSet<Collider> currentlyTouching = new HashSet<Collider>();

    private string LoopKey => $"{GetInstanceID()}_{soundId}";
    private string FlagId => string.IsNullOrEmpty(onceFlagId) ? $"AudioCollisionTrigger_{GetInstanceID()}_{soundId}_played" : onceFlagId;

    private void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        if (playMode == PlayMode.OneShot && speed < minImpactSpeed) return;

        float volumeScale = 1f;
        if (playMode == PlayMode.OneShot && maxImpactSpeedForFullVolume > minImpactSpeed)
            volumeScale = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeedForFullVolume, speed);

        Vector3 pos = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        HandleEnter(collision.collider, pos, volumeScale);
    }

    private void OnCollisionExit(Collision collision) => HandleExit(collision.collider);

    private void OnTriggerEnter(Collider other) => HandleEnter(other, transform.position, 1f);

    private void OnTriggerExit(Collider other) => HandleExit(other);

    private bool PassesFilter(Collider other) =>
        string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);

    private void HandleEnter(Collider other, Vector3 position, float volumeScale)
    {
        if (!PassesFilter(other)) return;

        if (playMode == PlayMode.OneShot)
        {
            if (Time.time - lastPlayTime < cooldown) return;
            lastPlayTime = Time.time;

            if (onlyTriggerOnce)
            {
                if (PersistentGameFlags.instance != null && PersistentGameFlags.instance.HasFlag(FlagId))
                    return;

                if (PersistentGameFlags.instance != null)
                    PersistentGameFlags.instance.SetFlag(FlagId);
                else
                    Debug.LogWarning("[AudioCollisionTrigger] onlyTriggerOnce is set but " +
                                      "PersistentGameFlags.instance is null — this won't be " +
                                      "remembered across scene reloads.");
            }

            if (AudioManager.instance == null)
            {
                Debug.LogWarning("[AudioCollisionTrigger] AudioManager.instance is null.");
                return;
            }

            AudioManager.instance.PlaySFX(soundId, playAtCollisionPosition ? position : (Vector3?)null, volumeScale);
        }
        else // LoopWhileTouching
        {
            bool wasEmpty = currentlyTouching.Count == 0;
            currentlyTouching.Add(other);

            if (!wasEmpty) return; // already looping because of some other contact

            if (AudioManager.instance == null)
            {
                Debug.LogWarning("[AudioCollisionTrigger] AudioManager.instance is null.");
                return;
            }

            AudioManager.instance.PlayLoopingSFX(LoopKey, soundId,
                playAtCollisionPosition ? position : (Vector3?)null, 1f, loopFadeInTime);
        }
    }

    private void HandleExit(Collider other)
    {
        if (playMode != PlayMode.LoopWhileTouching) return;
        if (!currentlyTouching.Remove(other)) return;

        if (currentlyTouching.Count == 0)
            AudioManager.instance?.StopLoopingSFX(LoopKey, loopFadeOutTime);
    }

    private void OnDisable()
    {
        // Safety net: if this object is disabled/destroyed mid-touch (e.g. pooled objects,
        // scene unload) don't leave an orphaned loop playing forever.
        if (playMode == PlayMode.LoopWhileTouching && currentlyTouching.Count > 0)
        {
            currentlyTouching.Clear();
            AudioManager.instance?.StopLoopingSFX(LoopKey, 0f);
        }
    }
}
