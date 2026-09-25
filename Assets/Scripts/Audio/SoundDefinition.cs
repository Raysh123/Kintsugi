using UnityEngine;

/// <summary>
/// One named, playable sound. Everything downstream (AudioManager, event triggers) refers
/// to sounds by string id rather than by AudioClip reference, so an event source only needs
/// to know "play 'footstep_dirt'" without caring what clip that resolves to.
/// </summary>
[CreateAssetMenu(fileName = "New Sound", menuName = "Audio/Sound Definition")]
public class SoundDefinition : ScriptableObject
{
    public enum Category { SFX, Music }

    [Tooltip("Unique id used to look this sound up at runtime, e.g. \"footstep_dirt\" or " +
             "\"theme_dojo\". Must be unique within whatever SoundLibrary registers it.")]
    public string id;

    [Tooltip("Purely informational right now (AudioManager routes SFX vs music through " +
             "separate calls regardless) — useful for organizing the library and filtering " +
             "in the inspector.")]
    public Category category = Category.SFX;

    [Tooltip("One or more clips. If more than one is assigned, a random one is picked each " +
             "time this sound plays — simple variation for things like footsteps or hits.")]
    public AudioClip[] clips;

    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Base pitch.")]
    public float pitch = 1f;

    [Tooltip("Random +/- range added to pitch on each play, for variation. 0 = fixed pitch.")]
    [Range(0f, 0.5f)] public float pitchVariance = 0f;

    [Tooltip("If true, AudioManager.PlaySFX loops this forever once played (it hands back " +
             "the AudioSource so you can Stop() it yourself) — use this only for something " +
             "with no real start/stop trigger, like a scene-long ambience. For a sound that " +
             "should loop for the duration of some condition (an animation state, an object " +
             "staying in a collider, etc.), leave this false and drive it with " +
             "AudioManager.PlayLoopingSFX/StopLoopingSFX instead, which is what " +
             "AudioEventTrigger.StartLoop/StopLoop and AudioCollisionTrigger's " +
             "LoopWhileTouching mode use. Music always loops and is controlled separately " +
             "via PlayMusic/StopMusic.")]
    public bool loop = false;

    [Tooltip("0 = flat 2D sound (UI, music). 1 = fully 3D/positional (footsteps, impacts). " +
             "Only takes effect if a world position is passed in when the sound is played.")]
    [Range(0f, 1f)] public float spatialBlend = 0f;

    public AudioClip GetClip()
    {
        if (clips == null || clips.Length == 0) return null;
        return clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
    }

    public float GetPitch()
    {
        if (pitchVariance <= 0f) return pitch;
        return pitch + Random.Range(-pitchVariance, pitchVariance);
    }
}
