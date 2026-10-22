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
/// 3. On each group, add three "Parametric Equalizer" effects (Unity has no built-in 3-band
///    EQ - one ParamEQ per band is the way): set one to a low Center freq (~200 Hz), one
///    mid (~1000 Hz), one high (~6000 Hz). Their "Frequency gain" is a LINEAR multiplier
///    (0.05 to 3.0, 1.0 = no change), not dB - the EQ methods below take dB and convert for
///    you (see eqGainParamsAreLinear). Give each band's gain parameter a distinct name when
///    exposing it.
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

    [Tooltip("Tick if your EQ band gain params are a LINEAR multiplier - Unity's built-in " +
             "Parametric Equalizer effect (0.05 to 3.0, 1.0 = no change), which is what the " +
             "setup steps above use. Untick only if the exposed params genuinely take dB. " +
             "SetMusicEQ/SetSfxEQ and the pause effect all take dB and convert accordingly.")]
    [SerializeField] private bool eqGainParamsAreLinear = true;

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

    [Header("Layered Music (2 simultaneous channels - see PlayMusicLayer)")]
    [Tooltip("Which layer index DuckMusicLayer/RestoreMusicLayer below operate on.")]
    [SerializeField] private int duckableLayerIndex = 1;
    [Tooltip("Fraction (0-1) of the duckable layer's normal volume it fades down to on " +
             "DuckMusicLayer, e.g. 0.3 = 30% of whatever that track normally plays at.")]
    [SerializeField] private float duckedVolume = 0.3f;
    [SerializeField] private float duckFadeTime = 0.5f;

    [Header("Music: auto-start one track on scene load (e.g. the fight scene)")]
    [Tooltip("SoundDefinition id of the music track to start as soon as this scene loads, via " +
             "the normal PlayMusic. Leave empty for no auto-start.")]
    [SerializeField] private string autoPlayMusicId;
    [SerializeField] private float autoPlayMusicFadeIn = 1.5f;

    [Header("Music: pause treatment (wire to MatchTimer.OnMatchStop / OnMatchStart)")]
    [Tooltip("Fraction of the music's current volume while paused, e.g. 0.4 = 40% as loud.")]
    [SerializeField] private float pauseVolumeMultiplier = 0.4f;
    [Tooltip("EQ change while paused, in dB relative to however each band is set right before " +
             "the pause. Defaults give a muffled 'held breath' feel: highs pulled down, mids a " +
             "little down, lows left alone.")]
    [SerializeField] private float pauseLowGainOffsetDb = 0f;
    [SerializeField] private float pauseMidGainOffsetDb = -2f;
    [SerializeField] private float pauseHighGainOffsetDb = -12f;
    [Tooltip("Playback speed multiplier while paused, e.g. 0.85 = 15% slower. 1 = no change. " +
             "AudioSource pitch changes tempo AND pitch together (tape-slowdown feel).")]
    [Range(0.3f, 1f)]
    [SerializeField] private float pausePitchMultiplier = 0.85f;
    [SerializeField] private float pauseTransitionTime = 0.4f;
    [SerializeField] private float resumeTransitionTime = 0.8f;

    // Pitch is tracked as (each channel's own base pitch) x (a shared pause factor), so the
    // restore is exact and a track that starts mid-pause still gets the slowdown applied.
    private readonly float[] musicBasePitch = new float[2] { 1f, 1f };
    private float musicPitchFactor = 1f;
    private Coroutine musicPitchRoutine;

    // Pause-treatment bookkeeping. Index: 0 = volume, 1 = low, 2 = mid, 3 = high.
    private bool musicPauseActive;
    private readonly float[] musicPauseBase = new float[4];
    private readonly bool[] musicPauseHas = new bool[4];
    private Coroutine musicBusRoutine;

    private readonly AudioSource[] layerChannels = new AudioSource[2];
    private readonly Coroutine[] layerFadeRoutines = new Coroutine[2];
    // The volume each layer was started at (the SoundDefinition's own authored volume) -
    // duck/restore scale relative to this, so restoring returns to the real level instead
    // of jumping to an absolute 1.0.
    private readonly float[] layerBaseVolume = new float[2] { 1f, 1f };

    private List<AudioSource> sfxPool;

    // Keyed loops: an arbitrary string -> the AudioSource currently playing that loop, so
    // "start" and "stop" calls can come from anywhere without passing an AudioSource around.
    private readonly Dictionary<string, AudioSource> activeLoops = new Dictionary<string, AudioSource>();
    private readonly Dictionary<string, Coroutine> loopFadeRoutines = new Dictionary<string, Coroutine>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        BuildMusicChannels();
        BuildLayerChannels();
        BuildSfxPool();
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(autoPlayMusicId)) return;

        PlayMusic(autoPlayMusicId, autoPlayMusicFadeIn);
    }

    private void OnDestroy()
    {
        // Not persistent, so a scene change destroys this along with everything else — make
        // sure the static reference doesn't keep pointing at a dead object in the meantime.
        if (instance == this) instance = null;

        // The mixer's exposed values outlive this scene - if the scene is left mid-pause
        // (e.g. straight to the next scene during the post-point freeze), put the music bus
        // back exactly where it was so the next scene doesn't start muffled and quiet.
        if (musicPauseActive && mixer != null)
        {
            string[] names = MusicBusParams();
            for (int i = 0; i < 4; i++)
                if (musicPauseHas[i]) mixer.SetFloat(names[i], musicPauseBase[i]);
            musicPauseActive = false;
        }

        // Channels are destroyed with this object, so no pitch restore needed there.
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

    private void BuildLayerChannels()
    {
        for (int i = 0; i < layerChannels.Length; i++)
        {
            GameObject channelObj = new GameObject($"MusicLayerChannel_{i}");
            channelObj.transform.SetParent(transform);

            AudioSource source = channelObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = musicMixerGroup;
            source.volume = 0f;

            layerChannels[i] = source;
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
            return null;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            return null;
        }

        AudioSource source = GetFreeSfxSource();
        if (source == null)
        {
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

    // A source handed out this frame is skipped for the rest of the frame: isPlaying can still read false right after Play(),
    // which let two sounds started on the same frame (e.g. two animation events on one frame) share a source and cut each other off.
    private readonly Dictionary<AudioSource, int> claimedFrame = new Dictionary<AudioSource, int>();

    private AudioSource GetFreeSfxSource()
    {
        foreach (AudioSource source in sfxPool)
        {
            if (source.isPlaying) continue;
            if (claimedFrame.TryGetValue(source, out int f) && f == Time.frameCount) continue;
            claimedFrame[source] = Time.frameCount;
            return source;
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
            return null;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            return null;
        }

        AudioSource source = GetFreeSfxSource();
        if (source == null)
        {
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
    /// the active track is a no-op. incomingFadeTime (optional): fade-in time for the NEW track on
    /// its own - 0 starts it at full volume instantly while the old one still fades over fadeTime.
    /// Default (-1) = same as fadeTime.
    /// </summary>
    public void PlayMusic(string musicId, float fadeTime = 1.5f, float incomingFadeTime = -1f)
    {
        if (musicId == currentMusicId && activeMusicChannel >= 0 &&
            musicChannels[activeMusicChannel].isPlaying)
        {
            return;
        }

        SoundDefinition sound = library != null ? library.Get(musicId) : null;
        if (sound == null)
        {
            return;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            return;
        }

        // A track that StopMusic is still fading out keeps its channel - don't start on top of it.
        int busyChannel = activeMusicChannel;
        if (busyChannel < 0)
            busyChannel = musicChannels[0].isPlaying ? 0 : (musicChannels[1].isPlaying ? 1 : -1);
        int nextChannel = busyChannel == 0 ? 1 : 0;
        AudioSource incoming = musicChannels[nextChannel];
        AudioSource outgoing = activeMusicChannel >= 0 ? musicChannels[activeMusicChannel] : null;

        incoming.clip = clip;
        incoming.volume = 0f;
        musicBasePitch[nextChannel] = sound.GetPitch();
        incoming.pitch = musicBasePitch[nextChannel] * musicPitchFactor;
        incoming.loop = true;
        incoming.Play();

        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = StartCoroutine(CrossfadeMusic(incoming, outgoing, sound.volume, fadeTime, incomingFadeTime));

        activeMusicChannel = nextChannel;
        currentMusicId = musicId;

    }

    /// <summary>Fades the currently playing music out and stops it. No-op if nothing is playing.</summary>
    public void StopMusic(float fadeTime = 1.5f)
    {
        if (activeMusicChannel < 0) return;

        AudioSource outgoing = musicChannels[activeMusicChannel];

        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = null;

        // Its own fade (not the shared crossfade routine) so a PlayMusic right after can't cancel
        // it and leave this track stuck at half volume.
        if (fadeTime <= 0f) outgoing.Stop();
        else StartCoroutine(FadeSource(outgoing, outgoing.volume, 0f, fadeTime, true));

        activeMusicChannel = -1;
        currentMusicId = null;
    }

    private IEnumerator CrossfadeMusic(AudioSource incoming, AudioSource outgoing, float targetVolume, float fadeTime, float incomingFadeTime = -1f)
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

        if (incomingFadeTime < 0f) incomingFadeTime = fadeTime;
        if (incoming != null && incomingFadeTime <= 0f) incoming.volume = targetVolume;

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float f = t / fadeTime;

            if (incoming != null && incomingFadeTime > 0f)
                incoming.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(t / incomingFadeTime));
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

    // ---------------- Layered Music (2 simultaneous channels) ----------------
    // Separate from the crossfade pair above - PlayMusic/StopMusic replace one track with
    // another. These two channels are instead meant to play AT THE SAME TIME (e.g. a
    // constant fight theme on layer 0 plus a secondary layer on layer 1 that gets ducked
    // rather than stopped during the between-fights pause), so starting one never touches
    // the other.

    /// <summary>Starts musicId looping on the given layer (0 or 1), independent of whatever's
    /// playing on the other layer. A no-op if that layer is already playing this exact clip,
    /// so it's safe to call every time a scene/fight starts without restarting the loop.</summary>
    public void PlayMusicLayer(int layerIndex, string musicId, float fadeInTime = 0f)
    {
        if (layerIndex < 0 || layerIndex >= layerChannels.Length)
        {
            return;
        }

        SoundDefinition sound = library != null ? library.Get(musicId) : null;
        if (sound == null)
        {
            return;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            return;
        }

        AudioSource source = layerChannels[layerIndex];
        if (source.isPlaying && source.clip == clip) return; // already playing this track on this layer

        if (layerFadeRoutines[layerIndex] != null)
        {
            StopCoroutine(layerFadeRoutines[layerIndex]);
            layerFadeRoutines[layerIndex] = null;
        }

        float targetVolume = sound.volume;
        layerBaseVolume[layerIndex] = targetVolume;

        source.clip = clip;
        source.pitch = sound.GetPitch();
        source.loop = true;
        source.volume = fadeInTime > 0f ? 0f : targetVolume;
        source.Play();

        if (fadeInTime > 0f)
            layerFadeRoutines[layerIndex] = StartCoroutine(FadeSource(source, 0f, targetVolume, fadeInTime, stopAtEnd: false));
    }

    /// <summary>Fades a currently-playing layer to a FRACTION (0-1) of its normal volume,
    /// without stopping it - 1 = the level the track was started at (the SoundDefinition's own
    /// volume), 0.3 = 30% of that. This is the "duck" call: SetMusicLayerVolume(1, 0.3f, 0.5f)
    /// pulls layer 1 down over half a second, SetMusicLayerVolume(1, 1f, 0.5f) brings it back
    /// to exactly where it started. Does nothing if that layer isn't currently playing.</summary>
    public void SetMusicLayerVolume(int layerIndex, float targetVolume01, float fadeTime = 0.5f)
    {
        if (layerIndex < 0 || layerIndex >= layerChannels.Length) return;

        AudioSource source = layerChannels[layerIndex];
        if (source == null || !source.isPlaying) return;

        if (layerFadeRoutines[layerIndex] != null) StopCoroutine(layerFadeRoutines[layerIndex]);

        float target = Mathf.Clamp01(targetVolume01) * layerBaseVolume[layerIndex];
        if (fadeTime <= 0f)
        {
            source.volume = target;
            return;
        }

        layerFadeRoutines[layerIndex] = StartCoroutine(FadeSource(source, source.volume, target, fadeTime, stopAtEnd: false));
    }

    /// <summary>Stops a layer outright, e.g. when leaving the fight scene entirely.</summary>
    public void StopMusicLayer(int layerIndex, float fadeOutTime = 0.5f)
    {
        if (layerIndex < 0 || layerIndex >= layerChannels.Length) return;

        AudioSource source = layerChannels[layerIndex];
        if (source == null) return;

        if (layerFadeRoutines[layerIndex] != null) StopCoroutine(layerFadeRoutines[layerIndex]);

        if (fadeOutTime <= 0f)
        {
            source.volume = 0f;
            source.Stop();
            return;
        }

        layerFadeRoutines[layerIndex] = StartCoroutine(FadeSource(source, source.volume, 0f, fadeOutTime, stopAtEnd: true));
    }

    /// <summary>
    /// No-argument convenience pair for MatchTimer.OnMatchStop / OnMatchStart — a plain
    /// UnityEvent in the Inspector can only bind a persistent call to a zero-argument method
    /// (or one with a single value typed directly into the Inspector), so these wrap
    /// SetMusicLayerVolume using the duckableLayerIndex/duckedVolume/duckFadeTime fields above
    /// rather than needing any extra wiring script. Drag this AudioManager onto
    /// MatchTimer.OnMatchStop and pick DuckMusicLayer, then onto OnMatchStart and pick
    /// RestoreMusicLayer.
    /// </summary>
    public void DuckMusicLayer() => SetMusicLayerVolume(duckableLayerIndex, duckedVolume, duckFadeTime);
    public void RestoreMusicLayer() => SetMusicLayerVolume(duckableLayerIndex, 1f, duckFadeTime);

    // ---------------- Music pause treatment (volume + EQ on the music bus) ----------------

    private string[] MusicBusParams() =>
        new[] { musicVolumeParam, musicLowGainParam, musicMidGainParam, musicHighGainParam };

    /// <summary>dB -> the value an EQ band param actually takes (linear multiplier for
    /// Unity's Parametric Equalizer, clamped to its 0.05-3.0 range; raw dB otherwise).</summary>
    private float DbToEqValue(float db) =>
        eqGainParamsAreLinear ? Mathf.Clamp(Mathf.Pow(10f, db / 20f), 0.05f, 3f) : db;

    /// <summary>Applies a dB offset on top of a band's current value, in that param's own unit.</summary>
    private float ApplyEqOffset(float currentValue, float offsetDb) =>
        eqGainParamsAreLinear
            ? Mathf.Clamp(currentValue * Mathf.Pow(10f, offsetDb / 20f), 0.05f, 3f)
            : currentValue + offsetDb;

    /// <summary>
    /// Music keeps playing exactly as normal - this just lowers its volume and shifts its EQ
    /// on the mixer's music bus (the pauseVolumeMultiplier / pause*GainOffsetDb fields above),
    /// fading over pauseTransitionTime. It remembers the bus's exact values from just before,
    /// so ClearMusicPauseEffect returns to those precisely. Zero-argument on purpose, so it
    /// can be bound directly to MatchTimer.OnMatchStop in the Inspector. Needs the mixer set up
    /// per the class comment (volume + EQ params exposed); any param that isn't exposed is
    /// skipped with a warning rather than blocking the rest.
    /// </summary>
    public void ApplyMusicPauseEffect()
    {
        if (mixer == null)
        {
            return;
        }

        if (musicPauseActive) return;

        string[] names = MusicBusParams();

        // Only capture the "normal" values when no fade is mid-flight - if a resume fade is
        // still running (a point scored right as the last pause ended), the live values are
        // partway between muffled and normal, which would become a wrong baseline. The
        // previously captured base is still right in that case.
        if (musicBusRoutine == null)
        {
            bool anyFound = false;
            for (int i = 0; i < 4; i++)
            {
                musicPauseHas[i] = mixer.GetFloat(names[i], out musicPauseBase[i]);
                anyFound |= musicPauseHas[i];
            }

            if (!anyFound) return;
        }

        musicPauseActive = true;

        float volumeDb = 20f * Mathf.Log10(Mathf.Max(pauseVolumeMultiplier, 0.0001f));
        float[] targets =
        {
            musicPauseBase[0] + volumeDb,
            ApplyEqOffset(musicPauseBase[1], pauseLowGainOffsetDb),
            ApplyEqOffset(musicPauseBase[2], pauseMidGainOffsetDb),
            ApplyEqOffset(musicPauseBase[3], pauseHighGainOffsetDb),
        };

        StartMusicBusFade(targets, pauseTransitionTime);
        StartMusicPitchFade(Mathf.Clamp(pausePitchMultiplier, 0.3f, 1f), pauseTransitionTime);
    }

    /// <summary>Fades the music bus back to exactly the volume/EQ it had before
    /// ApplyMusicPauseEffect, over resumeTransitionTime. Bind to MatchTimer.OnMatchStart. Safe
    /// to call when no pause effect is active (MatchTimer fires OnMatchStart once at scene
    /// start too) - it just does nothing.</summary>
    public void ClearMusicPauseEffect()
    {
        if (!musicPauseActive) return;

        musicPauseActive = false;
        StartMusicBusFade((float[])musicPauseBase.Clone(), resumeTransitionTime);
        StartMusicPitchFade(1f, resumeTransitionTime);
    }

    private void ApplyMusicPitch()
    {
        for (int i = 0; i < musicChannels.Length; i++)
            if (musicChannels[i] != null)
                musicChannels[i].pitch = musicBasePitch[i] * musicPitchFactor;
    }

    private void StartMusicPitchFade(float targetFactor, float duration)
    {
        if (musicPitchRoutine != null) StopCoroutine(musicPitchRoutine);
        musicPitchRoutine = StartCoroutine(FadeMusicPitch(targetFactor, duration));
    }

    private IEnumerator FadeMusicPitch(float targetFactor, float duration)
    {
        float from = musicPitchFactor;
        if (duration > 0f)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                musicPitchFactor = Mathf.Lerp(from, targetFactor, Mathf.Clamp01(t / duration));
                ApplyMusicPitch();
                yield return null;
            }
        }

        musicPitchFactor = targetFactor;
        ApplyMusicPitch();
        musicPitchRoutine = null;
    }

    private void StartMusicBusFade(float[] targets, float duration)
    {
        if (musicBusRoutine != null) StopCoroutine(musicBusRoutine);
        musicBusRoutine = StartCoroutine(FadeMusicBus(targets, duration));
    }

    private IEnumerator FadeMusicBus(float[] targets, float duration)
    {
        string[] names = MusicBusParams();

        float[] from = new float[4];
        for (int i = 0; i < 4; i++)
            if (musicPauseHas[i]) mixer.GetFloat(names[i], out from[i]);

        if (duration > 0f)
        {
            float t = 0f;
            while (t < duration)
            {
                // Unscaled - this runs during MatchTimer's real-time hit pause, and shouldn't
                // care what Time.timeScale is doing.
                t += Time.unscaledDeltaTime;
                float f = Mathf.Clamp01(t / duration);
                for (int i = 0; i < 4; i++)
                    if (musicPauseHas[i]) mixer.SetFloat(names[i], Mathf.Lerp(from[i], targets[i], f));
                yield return null;
            }
        }

        for (int i = 0; i < 4; i++)
            if (musicPauseHas[i]) mixer.SetFloat(names[i], targets[i]);

        musicBusRoutine = null;
    }

    // ---------------- Volume (requires Mixer setup) ----------------

    public void SetMusicVolume01(float linearVolume) => SetMixerVolume(musicVolumeParam, linearVolume);
    public void SetSfxVolume01(float linearVolume) => SetMixerVolume(sfxVolumeParam, linearVolume);

    private void SetMixerVolume(string param, float linearVolume)
    {
        if (mixer == null)
        {
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
            return;
        }

        // Callers pass dB; convert to whatever the exposed params actually take (a linear
        // multiplier for Unity's Parametric Equalizer - passing raw dB there would clamp, e.g.
        // -12 would slam the band to its 0.05 floor, nearly silent).
        mixer.SetFloat(lowParam, DbToEqValue(lowGainDb));
        mixer.SetFloat(midParam, DbToEqValue(midGainDb));
        mixer.SetFloat(highParam, DbToEqValue(highGainDb));
    }
}