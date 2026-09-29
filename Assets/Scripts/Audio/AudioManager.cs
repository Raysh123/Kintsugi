using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central audio system for the project — built entirely on Unity's own AudioSource/
/// AudioMixer, no third-party middleware. Handles:
///   - 2 music channels that crossfade into each other (so a new track fades in while the
///     old one fades out, rather than hard-cutting)
///   - EQ control on the music and SFX buses via an AudioMixer's exposed parameters
///   - a small pooled AudioSource system for SFX, so overlapping sounds don't need one
///     AudioSource each
///   - a SoundLibrary lookup, so every sound is played by string id — event sources
///     (collisions, animation events, dialogue, anything) only need to know an id, never an
///     AudioClip reference
///
/// Three playback modes, all driven off the same pool:
///   - PlaySFX(id): fire-and-forget, plays once (or loops if the SoundDefinition itself is
///     authored as looping — e.g. a background ambience with no explicit stop point). Hands
///     back the AudioSource so you can Stop() it yourself if you need to.
///   - PlayLoopingSFX(loopKey, id) / StopLoopingSFX(loopKey): explicit "start when something
///     begins, stop when it ends" looping, addressed by an arbitrary string key rather than
///     an AudioSource reference — so an OnCollisionEnter/Exit pair, an animation state's
///     enter/exit events, or two entirely different pieces of code can start and stop the
///     same logical loop without passing an object reference between them. Calling
///     PlayLoopingSFX again on an already-active key is a no-op (other than updating
///     position), so it's safe to call from multiple overlapping triggers.
///   - Pass onlyPlayOnce-style gating at the caller (see AudioEventTrigger/
///     AudioCollisionTrigger) using PersistentGameFlags for "this should only ever fire the
///     first time" — AudioManager itself doesn't track that, it's a thin concern that
///     belongs at the trigger.
///
/// Not persistent — lives and dies with whatever scene it's in (unlike PersistentGameFlags).
/// Put one in each scene that needs audio, or make it part of a prefab you drop into every
/// scene. If you later want it to survive scene loads instead, add DontDestroyOnLoad(gameObject)
/// back into Awake(). See the Mixer section below for the one-time Audio Mixer asset setup
/// needed for volume/EQ control to do anything (SFX/music playback works even without it).
///
/// --- Audio Mixer setup (one-time, in the Unity Editor) ---
/// 1. Create an Audio Mixer asset (right-click in Project -> Create -> Audio Mixer).
/// 2. In it, create two Groups: "Music" and "SFX" (under Master).
/// 3. On each group, add an effect that gives you gain control per band — either a
///    "ParamEQ" (simplest: adds one adjustable band, add three instances for low/mid/high)
///    or Unity's built-in 3-band EQ effect if your version exposes one. Give each band's
///    gain parameter a distinct name when exposing it.
/// 4. Right-click each parameter you want scriptable (group Volume, each EQ band's gain)
///    and choose "Expose ... to script". Then open the mixer's "Exposed Parameters" list
///    (top-left dropdown) and rename each to match the *Param fields below exactly
///    (defaults: MusicVolume, SFXVolume, MusicLowGain, MusicMidGain, MusicHighGain,
///    SFXLowGain, SFXMidGain, SFXHighGain).
/// 5. Drag the Mixer asset and the two Groups onto this component in the Inspector.
/// Skipping this is fine — playback still works — but SetMusicEQ/SetSfxEQ/volume calls
/// will just log a warning and no-op until it's done.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("Library")]
    [SerializeField] private SoundLibrary library;

    [Header("Mixer (optional — see class comment for setup)")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Tooltip("Exposed parameter names on the AudioMixer. Must match exactly what you named " +
             "them in the Audio Mixer's Exposed Parameters list.")]
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";
    [SerializeField] private string musicLowGainParam = "MusicLowGain";
    [SerializeField] private string musicMidGainParam = "MusicMidGain";
    [SerializeField] private string musicHighGainParam = "MusicHighGain";
    [SerializeField] private string sfxLowGainParam = "SFXLowGain";
    [SerializeField] private string sfxMidGainParam = "SFXMidGain";
    [SerializeField] private string sfxHighGainParam = "SFXHighGain";

    [Header("SFX Pool")]
    [Tooltip("How many SFX (including active loops — a loop occupies a pool slot for its " +
             "entire lifetime) can play at once. If every source is busy when PlaySFX or " +
             "PlayLoopingSFX is called, the new sound is dropped (logged as a warning) " +
             "rather than cutting one off.")]
    [SerializeField] private int sfxPoolSize = 12;

    private readonly AudioSource[] musicChannels = new AudioSource[2];
    private int activeMusicChannel = -1;
    private string currentMusicId;
    private Coroutine musicFadeRoutine;

    private List<AudioSource> sfxPool;

    // Keyed loops: an arbitrary string -> the AudioSource currently playing that loop, so
    // "start" and "stop" calls can come from anywhere without passing an AudioSource around.
    private readonly Dictionary<string, AudioSource> activeLoops = new Dictionary<string, AudioSource>();
    private readonly Dictionary<string, Coroutine> loopFadeRoutines = new Dictionary<string, Coroutine>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[AudioManager] Another AudioManager already exists in this scene " +
                              "— destroying this one. Only one should be present at a time.");
            Destroy(gameObject);
            return;
        }

        instance = this;

        BuildMusicChannels();
        BuildSfxPool();
    }

    private void OnDestroy()
    {
        // Not persistent, so a scene change destroys this along with everything else — make
        // sure the static reference doesn't keep pointing at a dead object in the meantime.
        if (instance == this) instance = null;
    }

    private void BuildMusicChannels()
    {
        for (int i = 0; i < musicChannels.Length; i++)
        {
            GameObject channelObj = new GameObject($"MusicChannel_{i}");
            channelObj.transform.SetParent(transform);

            AudioSource source = channelObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = musicMixerGroup;
            source.volume = 0f;

            musicChannels[i] = source;
        }
    }

    private void BuildSfxPool()
    {
        sfxPool = new List<AudioSource>(sfxPoolSize);
        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject sourceObj = new GameObject($"SFXSource_{i}");
            sourceObj.transform.SetParent(transform);

            AudioSource source = sourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxMixerGroup;

            sfxPool.Add(source);
        }
    }

    // ---------------- SFX: fire-and-forget ----------------

    /// <summary>
    /// Plays a sound by id, once through (or looping forever if the SoundDefinition itself
    /// is authored with loop = true — use this for e.g. a rain ambience you'll only ever
    /// stop by scene change, not for anything you need to start/stop on cue; for that use
    /// PlayLoopingSFX/StopLoopingSFX instead). Pass a worldPosition to spatialize it — only
    /// audible as positional if the SoundDefinition's spatialBlend > 0. Returns the
    /// AudioSource actually used, so callers can Stop() a looping one later if needed; treat
    /// it as borrowed, it goes back into the free pool as soon as it stops playing.
    /// </summary>
    public AudioSource PlaySFX(string soundId, Vector3? worldPosition = null, float volumeScale = 1f)
    {
        SoundDefinition sound = library != null ? library.Get(soundId) : null;
        if (sound == null)
        {
            Debug.LogWarning($"[AudioManager] Could not play SFX '{soundId}' — not found in library.");
            return null;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] SoundDefinition '{soundId}' has no clips assigned.");
            return null;
        }

        AudioSource source = GetFreeSfxSource();
        if (source == null)
        {
            Debug.LogWarning($"[AudioManager] SFX pool exhausted (size {sfxPoolSize}) — dropped " +
                              $"'{soundId}'. Increase sfxPoolSize if this happens often.");
            return null;
        }

        source.transform.position = worldPosition ?? transform.position;
        source.spatialBlend = sound.spatialBlend;
        source.clip = clip;
        source.volume = sound.volume * volumeScale;
        source.pitch = sound.GetPitch();
        source.loop = sound.loop;
        source.Play();

        return source;
    }

    private AudioSource GetFreeSfxSource()
    {
        foreach (AudioSource source in sfxPool)
        {
            if (!source.isPlaying) return source;
        }
        return null;
    }

    // ---------------- SFX: explicit start/stop loops ----------------

    /// <summary>
    /// Starts soundId looping under loopKey — an arbitrary string you choose, e.g.
    /// "player_footsteps" or a per-instance key like "{GetInstanceID()}_engine_hum". Loops until StopLoopingSFX is
    /// called with the same key. If that key is already looping, this just updates its
    /// position (if one is given) and returns the existing source rather than starting a
    /// second overlapping copy — so it's safe to call repeatedly, e.g. from every frame a
    /// condition holds, or from more than one overlapping trigger using the same key.
    /// </summary>
    public AudioSource PlayLoopingSFX(string loopKey, string soundId, Vector3? worldPosition = null,
                                       float volumeScale = 1f, float fadeInTime = 0f)
    {
        if (string.IsNullOrEmpty(loopKey))
        {
            Debug.LogWarning("[AudioManager] PlayLoopingSFX called with an empty loopKey.");
            return null;
        }

        if (activeLoops.TryGetValue(loopKey, out AudioSource existing) && existing != null && existing.isPlaying)
        {
            if (worldPosition.HasValue) existing.transform.position = worldPosition.Value;
            return existing;
        }

        SoundDefinition sound = library != null ? library.Get(soundId) : null;
        if (sound == null)
        {
            Debug.LogWarning($"[AudioManager] Could not start loop '{loopKey}' — sound '{soundId}' not found in library.");
            return null;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] SoundDefinition '{soundId}' has no clips assigned.");
            return null;
        }

        AudioSource source = GetFreeSfxSource();
        if (source == null)
        {
            Debug.LogWarning($"[AudioManager] SFX pool exhausted (size {sfxPoolSize}) — couldn't start loop '{loopKey}'.");
            return null;
        }

        float targetVolume = sound.volume * volumeScale;

        source.transform.position = worldPosition ?? transform.position;
        source.spatialBlend = sound.spatialBlend;
        source.clip = clip;
        source.pitch = sound.GetPitch();
        source.loop = true; // explicit loop lifecycle, regardless of the SoundDefinition's own loop flag
        source.volume = fadeInTime > 0f ? 0f : targetVolume;
        source.Play();

        activeLoops[loopKey] = source;

        if (fadeInTime > 0f)
            StartLoopFade(loopKey, source, source.volume, targetVolume, fadeInTime, stopAtEnd: false);

        Debug.Log($"[AudioManager] Started loop '{loopKey}' ('{soundId}').");

        return source;
    }

    /// <summary>
    /// Stops the loop running under loopKey, if any. Safe to call even if nothing is playing
    /// under that key (e.g. calling StopLoop from an Exit event when Enter never actually
    /// started one) — it's just a no-op in that case.
    /// </summary>
    public void StopLoopingSFX(string loopKey, float fadeOutTime = 0f)
    {
        if (string.IsNullOrEmpty(loopKey)) return;
        if (!activeLoops.TryGetValue(loopKey, out AudioSource source) || source == null)
            return;

        activeLoops.Remove(loopKey);

        if (fadeOutTime <= 0f)
        {
            source.Stop();
            return;
        }

        StartLoopFade(loopKey, source, source.volume, 0f, fadeOutTime, stopAtEnd: true);

        Debug.Log($"[AudioManager] Stopping loop '{loopKey}' over {fadeOutTime}s.");
    }

    /// <summary>Repositions an already-running loop, e.g. to keep a looping sound (footsteps,
    /// an engine hum) attached to a moving object without restarting it.</summary>
    public void UpdateLoopingSFXPosition(string loopKey, Vector3 worldPosition)
    {
        if (activeLoops.TryGetValue(loopKey, out AudioSource source) && source != null)
            source.transform.position = worldPosition;
    }

    public bool IsLoopingSFXActive(string loopKey) =>
        activeLoops.TryGetValue(loopKey, out AudioSource source) && source != null && source.isPlaying;

    private void StartLoopFade(string loopKey, AudioSource source, float from, float to, float duration, bool stopAtEnd)
    {
        if (loopFadeRoutines.TryGetValue(loopKey, out Coroutine running) && running != null)
            StopCoroutine(running);

        loopFadeRoutines[loopKey] = StartCoroutine(FadeSource(source, from, to, duration, stopAtEnd));
    }

    private IEnumerator FadeSource(AudioSource source, float from, float to, float duration, bool stopAtEnd)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        source.volume = to;
        if (stopAtEnd) source.Stop();
    }

    // ---------------- Music ----------------

    /// <summary>
    /// Crossfades to the music track with this id: the currently playing channel fades out
    /// while the other channel fades in, over fadeTime seconds. Safe to call again mid-fade
    /// (restarts the crossfade toward the new target). Calling with the id that's already
    /// the active track is a no-op.
    /// </summary>
    public void PlayMusic(string musicId, float fadeTime = 1.5f)
    {
        if (musicId == currentMusicId && activeMusicChannel >= 0 &&
            musicChannels[activeMusicChannel].isPlaying)
        {
            return;
        }

        SoundDefinition sound = library != null ? library.Get(musicId) : null;
        if (sound == null)
        {
            Debug.LogWarning($"[AudioManager] Could not play music '{musicId}' — not found in library.");
            return;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] SoundDefinition '{musicId}' has no clips assigned.");
            return;
        }

        int nextChannel = activeMusicChannel == 0 ? 1 : 0;
        AudioSource incoming = musicChannels[nextChannel];
        AudioSource outgoing = activeMusicChannel >= 0 ? musicChannels[activeMusicChannel] : null;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.pitch = sound.GetPitch();
        incoming.loop = true;
        incoming.Play();

        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = StartCoroutine(CrossfadeMusic(incoming, outgoing, sound.volume, fadeTime));

        activeMusicChannel = nextChannel;
        currentMusicId = musicId;

        Debug.Log($"[AudioManager] Crossfading to music '{musicId}' over {fadeTime}s.");
    }

    /// <summary>Fades the currently playing music out and stops it. No-op if nothing is playing.</summary>
    public void StopMusic(float fadeTime = 1.5f)
    {
        if (activeMusicChannel < 0) return;

        AudioSource outgoing = musicChannels[activeMusicChannel];

        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = StartCoroutine(CrossfadeMusic(null, outgoing, 0f, fadeTime));

        activeMusicChannel = -1;
        currentMusicId = null;
    }

    private IEnumerator CrossfadeMusic(AudioSource incoming, AudioSource outgoing, float targetVolume, float fadeTime)
    {
        float outgoingStartVolume = outgoing != null ? outgoing.volume : 0f;

        if (fadeTime <= 0f)
        {
            if (incoming != null) incoming.volume = targetVolume;
            if (outgoing != null)
            {
                outgoing.volume = 0f;
                outgoing.Stop();
            }
            yield break;
        }

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float f = t / fadeTime;

            if (incoming != null) incoming.volume = Mathf.Lerp(0f, targetVolume, f);
            if (outgoing != null) outgoing.volume = Mathf.Lerp(outgoingStartVolume, 0f, f);

            yield return null;
        }

        if (incoming != null) incoming.volume = targetVolume;
        if (outgoing != null)
        {
            outgoing.volume = 0f;
            outgoing.Stop();
        }
    }

    // ---------------- Volume (requires Mixer setup) ----------------

    public void SetMusicVolume01(float linearVolume) => SetMixerVolume(musicVolumeParam, linearVolume);
    public void SetSfxVolume01(float linearVolume) => SetMixerVolume(sfxVolumeParam, linearVolume);

    private void SetMixerVolume(string param, float linearVolume)
    {
        if (mixer == null)
        {
            Debug.LogWarning("[AudioManager] No AudioMixer assigned — can't set mixer volume.");
            return;
        }

        // AudioMixer volume is in dB; 0 linear has no finite dB, so floor it instead of
        // taking Log10(0).
        float dB = linearVolume <= 0.0001f ? -80f : Mathf.Log10(linearVolume) * 20f;
        mixer.SetFloat(param, dB);
    }

    // ---------------- EQ (requires Mixer setup) ----------------

    /// <summary>
    /// Sets the music bus's 3-band EQ gain, in dB. Requires the mixer to have an EQ effect
    /// on the Music group with its band gains exposed under the names configured on this
    /// component (musicLowGainParam etc.) — see the class comment for setup steps.
    /// </summary>
    public void SetMusicEQ(float lowGainDb, float midGainDb, float highGainDb)
    {
        SetEQInternal(musicLowGainParam, musicMidGainParam, musicHighGainParam, lowGainDb, midGainDb, highGainDb);
    }

    /// <summary>Same as SetMusicEQ but for the SFX bus.</summary>
    public void SetSfxEQ(float lowGainDb, float midGainDb, float highGainDb)
    {
        SetEQInternal(sfxLowGainParam, sfxMidGainParam, sfxHighGainParam, lowGainDb, midGainDb, highGainDb);
    }

    private void SetEQInternal(string lowParam, string midParam, string highParam,
                                float lowGainDb, float midGainDb, float highGainDb)
    {
        if (mixer == null)
        {
            Debug.LogWarning("[AudioManager] No AudioMixer assigned — can't set EQ.");
            return;
        }

        mixer.SetFloat(lowParam, lowGainDb);
        mixer.SetFloat(midParam, midGainDb);
        mixer.SetFloat(highParam, highGainDb);
    }
}
