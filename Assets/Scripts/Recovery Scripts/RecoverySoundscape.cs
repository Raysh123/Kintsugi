using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything audible in the RPG / recovery scene, in one component:
///   1. Footsteps  - one step per stride of real CharacterController movement (works with any
///                   model swap, silent while movement is locked or inside an activity).
///   2. Ambience   - looping layers, optionally tied to the time of day (CalendarManager).
///   3. Activities - start chime, win / lose / injured stings, an optional loop while it runs,
///                   and (rhythm activities) action / miss sounds. Defaults apply to every
///                   activity; add an entry to "Activity Overrides" to give one activity its own.
///
/// Put ONE of these in the scene. The three recovery interaction scripts call its static
/// methods, so they need no sound fields of their own. Every sound is a SoundDefinition id in
/// the AudioManager's library; a blank id plays nothing.
/// </summary>
public class RecoverySoundscape : MonoBehaviour
{
    public static RecoverySoundscape instance;

    // ---------------------------------------------------------------- Footsteps
    [System.Serializable]
    public class Surface
    {
        [Tooltip("Tag on the ground collider, e.g. \"Wood\" or \"Grass\".")]
        public string groundTag;
        public string soundId;
    }

    [Header("Footsteps")]
    [Tooltip("The player's CharacterController. Left empty, it finds the object tagged Player.")]
    [SerializeField] private CharacterController playerController;
    [SerializeField] private string footstepId = "footstep";
    [SerializeField] private List<Surface> surfaces = new List<Surface>();
    [Tooltip("Distance (units) between steps. Larger = slower step rate. Tune against the walk " +
             "animation until the sound lands on the foot.")]
    [SerializeField] private float strideLength = 1.6f;
    [Tooltip("Below this horizontal speed no footsteps play.")]
    [SerializeField] private float minSpeed = 0.5f;
    [Range(0f, 2f)][SerializeField] private float footstepVolume = 1f;
    [SerializeField] private LayerMask groundMask = ~0;
    private float travelled;
    private Vector3 lastPlayerPos;
    private bool haveLastPos;
    private PlayerMovement playerMovement;

    // ---------------------------------------------------------------- Ambience
    [System.Serializable]
    public class AmbienceLayer
    {
        [Tooltip("SoundDefinition id (plays as a loop whatever its own Loop box says).")]
        public string soundId;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Leave EMPTY to play in every phase of the day.")]
        public DayPhase[] playDuring;
    }

    [System.Serializable]
    public class AmbienceArea
    {
        [Tooltip("Just a label, e.g. Park / Home / Dojo.")]
        public string name = "Area";
        [Tooltip("One or more colliders that mark out this location - add a BoxCollider to an " +
                 "empty object, stretch it over the whole area, tick Is Trigger. The area is " +
                 "active while the player stands inside ANY of them (height is ignored).")]
        public Collider[] zones;
        [Tooltip("Ambience that plays only in this area.")]
        public List<AmbienceLayer> layers = new List<AmbienceLayer>();
    }

    [Header("Ambience")]
    [Tooltip("Plays in every area (e.g. a distant city hum). Usually left empty.")]
    [SerializeField] private List<AmbienceLayer> everywhere = new List<AmbienceLayer>();
    [Tooltip("One entry per location: Park, Home, Dojo... The NAME must match the travel map's " +
             "location name (case doesn't matter), because the travel map tells the soundscape " +
             "where you've arrived.")]
    [SerializeField] private List<AmbienceArea> areas = new List<AmbienceArea>();
    [Tooltip("Area used when the scene starts, until the first travel / fight return changes it.")]
    [SerializeField] private string startingArea = "Home";
    [Tooltip("OPTIONAL extra: also switch area by which Zone collider the player is standing in. " +
             "Off by default - the travel map and post-fight handler set the area directly, " +
             "which also works while the player model is hidden during an activity.")]
    [SerializeField] private bool detectFromZones = false;
    [Tooltip("Fade time when the ambience changes (area or time of day).")]
    [SerializeField] private float ambienceFade = 2f;
    [Tooltip("How often (seconds) it checks which area the player is in.")]
    [SerializeField] private float areaCheckInterval = 0.25f;
    [SerializeField] private bool debugAmbience = false;

    private readonly HashSet<string> playingKeys = new HashSet<string>();
    private CalendarManager subscribedCalendar;
    private DayPhase currentPhase;
    // Survives scene loads (the fight scene unloads this one), so coming back from a fight resumes
    // the area you left from (the dojo) instead of falling back to Starting Area.
    private static string lastAreaName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { lastAreaName = null; }

    private int currentArea = -1;      // stays on the last area entered if the player is between zones
    private bool ambienceReady;
    private float areaTimer;

    // ---------------------------------------------------------------- Activities
    [System.Serializable]
    public class ActivitySounds
    {
        [Tooltip("Plays the moment the activity is accepted (before any tutorial popup).")]
        public string startChime = "activity_start";
        [Tooltip("Loops while the activity is running; stops by itself when it ends.")]
        public string loop;
        public string win = "activity_win";
        public string lose = "activity_lose";
        public string injured = "activity_injured";
        [Tooltip("Rhythm activities: played on each action press.")]
        public string action;
        [Tooltip("Rhythm activities: played on a missed hit.")]
        public string miss;
    }

    [System.Serializable]
    public class ActivityOverride
    {
        [Tooltip("The activity this entry is for (the same ActivityDefinition asset the " +
                 "interaction uses).")]
        public ActivityDefinition activity;
        [Tooltip("Only the ids you fill in here replace the defaults - blank ones fall back.")]
        public ActivitySounds sounds = new ActivitySounds { startChime = "", win = "", lose = "", injured = "" };
    }

    [Header("Activities (defaults for all)")]
    [SerializeField] private ActivitySounds activityDefaults = new ActivitySounds();
    [Tooltip("Optional per-activity sounds - e.g. a different loop for the gym than the bag.")]
    [SerializeField] private List<ActivityOverride> activityOverrides = new List<ActivityOverride>();
    [SerializeField] private float activityLoopFade = 0.4f;

    // ================================================================ lifecycle
    private void Awake()
    {
        instance = this;
        if (playerController == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerController = p.GetComponent<CharacterController>();
        }
        if (playerController != null) playerMovement = playerController.GetComponent<PlayerMovement>();
    }

    private void OnEnable() => StartCoroutine(BeginAmbience());

    private void OnDisable()
    {
        if (subscribedCalendar != null) subscribedCalendar.OnPhaseChanged -= ApplyAmbience;
        subscribedCalendar = null;
        foreach (string key in playingKeys) Sfx.StopLoop(key, 0f);
        playingKeys.Clear();
        ambienceReady = false;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        UpdateFootsteps();

        if (!ambienceReady || !detectFromZones) return;
        areaTimer -= Time.unscaledDeltaTime;
        if (areaTimer > 0f) return;
        areaTimer = areaCheckInterval;

        int found = DetectArea();
        if (found != currentArea)
        {
            currentArea = found;
            if (debugAmbience)
                Debug.Log($"[Soundscape] Entered area: {(currentArea >= 0 ? areas[currentArea].name : "none")}");
            RefreshAmbience();
        }
    }

    // ================================================================ footsteps
    private void UpdateFootsteps()
    {
        if (playerController == null || string.IsNullOrEmpty(footstepId)) return;

        // Measure real movement from the position, NOT CharacterController.velocity: velocity only
        // updates when Move() is called, so it stays at its last value while movement is locked
        // (map, fight menu, dialogue) and the footsteps would keep going.
        Vector3 pos = playerController.transform.position;
        float speed = 0f;
        if (haveLastPos)
        {
            Vector3 delta = pos - lastPlayerPos;
            delta.y = 0f;
            // A huge one-frame jump is a teleport (travel map, fight return), not walking.
            if (delta.magnitude < 2f)
                speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }
        lastPlayerPos = pos;
        haveLastPos = true;

        bool lockedOut = playerMovement != null &&
                         (playerMovement.movementLocked || playerMovement.cutsceneLocked || playerMovement.interacting || ActiveDialogueTracker.AnyActive);

        if (lockedOut || !playerController.isGrounded || speed < minSpeed)
        {
            // So the first step after setting off lands promptly instead of after a full stride.
            travelled = Mathf.Max(travelled, strideLength * 0.5f);
            return;
        }

        travelled += speed * Time.deltaTime;
        if (travelled >= strideLength)
        {
            travelled -= strideLength;
            Sfx.Play(CurrentFootstepId(), footstepVolume);
        }
    }

    private string CurrentFootstepId()
    {
        if (surfaces.Count > 0 &&
            Physics.Raycast(playerController.transform.position + Vector3.up * 0.2f, Vector3.down,
                            out RaycastHit hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            foreach (Surface s in surfaces)
                if (!string.IsNullOrEmpty(s.groundTag) && hit.collider.CompareTag(s.groundTag))
                    return s.soundId;
        }
        return footstepId;
    }

    // ================================================================ ambience
    private IEnumerator BeginAmbience()
    {
        Debug.Log("[Soundscape] BeginAmbience started - waiting for an AudioManager in this scene.");
        float audioWait = 0f;
        bool warned = false;
        while (AudioManager.instance == null)
        {
            audioWait += Time.unscaledDeltaTime;
            if (!warned && audioWait > 2f)
            {
                warned = true;
                Debug.LogWarning("[Soundscape] Still no AudioManager.instance after 2s - ambience can't play. " +
                                 "Is there an active AudioManager in THIS scene, and does it set instance in Awake?");
            }
            yield return null;
        }
        Debug.Log("[Soundscape] AudioManager found.");

        currentPhase = DayPhase.Morning;
        float wait = 0f; // scenes without a calendar just ignore the phase filter
        while (CalendarManager.instance == null && wait < 1f) { wait += Time.unscaledDeltaTime; yield return null; }
        if (CalendarManager.instance != null)
        {
            subscribedCalendar = CalendarManager.instance;
            subscribedCalendar.OnPhaseChanged += ApplyAmbience;
            currentPhase = subscribedCalendar.CurrentPhase;
        }

        // An arrival that happened before this finished starting up (e.g. a fight return) wins.
        if (currentArea < 0 && !string.IsNullOrEmpty(lastAreaName)) currentArea = FindArea(lastAreaName);
        if (currentArea < 0) currentArea = FindArea(startingArea);
        if (detectFromZones) currentArea = DetectArea();
        if (currentArea >= 0) lastAreaName = areas[currentArea].name;
        Debug.Log($"[Soundscape] Starting in area: {(currentArea >= 0 ? areas[currentArea].name : "none")}, phase {currentPhase}, " +
                  $"{areas.Count} area(s), {everywhere.Count} 'everywhere' layer(s).");
        RefreshAmbience();
        areaTimer = areaCheckInterval;
        ambienceReady = true;
    }

    /// <summary>Switches the ambience to the area with this name (case-insensitive). Called by
    /// TravelMapController on arrival and PostFightHandler on a fight return. Does nothing if
    /// there's no soundscape in the scene.</summary>
    public static void SetArea(string areaName)
    {
        if (instance != null) instance.SetAreaByName(areaName);
    }

    private void SetAreaByName(string areaName)
    {
        int idx = FindArea(areaName);
        if (idx < 0 || idx == currentArea) return;
        currentArea = idx;
        lastAreaName = areas[idx].name;
        if (debugAmbience) Debug.Log($"[Soundscape] Area set to: {areas[idx].name}");
        if (ambienceReady) RefreshAmbience();
    }

    private int FindArea(string areaName)
    {
        if (string.IsNullOrEmpty(areaName)) return -1;
        for (int i = 0; i < areas.Count; i++)
            if (string.Equals(areas[i].name, areaName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        Debug.LogWarning($"[Soundscape] No ambience area named '{areaName}' - add one with that exact name " +
                         "(or rename an existing one) so the ambience can follow it.");
        return -1;
    }

    private void ApplyAmbience(DayPhase phase)
    {
        currentPhase = phase;
        RefreshAmbience();
    }

    /// <summary>Which area the player is standing in (by the zone colliders, ignoring height).
    /// Between zones it keeps the previous area, so walking from one location to the next
    /// never drops to silence.</summary>
    private int DetectArea()
    {
        if (playerController == null) return currentArea;
        Vector3 p = playerController.transform.position;

        for (int i = 0; i < areas.Count; i++)
        {
            if (areas[i].zones == null) continue;
            foreach (Collider zone in areas[i].zones)
            {
                if (zone == null || !zone.gameObject.activeInHierarchy) continue;
                Bounds b = zone.bounds;
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z)
                    return i;
            }
        }
        return currentArea;
    }

    /// <summary>Works out which layers should be playing now (everywhere + the current area,
    /// filtered by time of day) and fades the rest out / the new ones in.</summary>
    private void RefreshAmbience()
    {
        var wanted = new Dictionary<string, AmbienceLayer>();

        for (int j = 0; j < everywhere.Count; j++)
            if (Wanted(everywhere[j])) wanted[$"amb_{GetInstanceID()}_g{j}"] = everywhere[j];

        if (currentArea >= 0 && currentArea < areas.Count)
        {
            List<AmbienceLayer> layers = areas[currentArea].layers;
            for (int j = 0; j < layers.Count; j++)
                if (Wanted(layers[j])) wanted[$"amb_{GetInstanceID()}_a{currentArea}_{j}"] = layers[j];
        }

        if (wanted.Count == 0)
            Debug.LogWarning($"[Soundscape] No ambience layers qualify right now (area: " +
                             $"{(currentArea >= 0 && currentArea < areas.Count ? areas[currentArea].name : "NONE - area name didn't match")}, " +
                             $"phase: {currentPhase}). Check: area name matches 'Starting Area'/location name, layers have a Sound Id, " +
                             "and 'Play During' includes the current phase (or is empty).");

        // Stop what's no longer wanted...
        var toStop = new List<string>();
        foreach (string key in playingKeys)
            if (!wanted.ContainsKey(key)) toStop.Add(key);
        foreach (string key in toStop)
        {
            Sfx.StopLoop(key, ambienceFade);
            playingKeys.Remove(key);
        }

        // ...and start what's new.
        foreach (var kv in wanted)
        {
            if (playingKeys.Contains(kv.Key)) continue;
            Debug.Log($"[Soundscape] Starting ambience '{kv.Value.soundId}' at layer volume {kv.Value.volume:F2} (area: {(currentArea >= 0 ? areas[currentArea].name : "none")}, phase: {currentPhase}).");
            if (kv.Value.volume <= 0.001f)
                Debug.LogWarning($"[Soundscape] Layer '{kv.Value.soundId}' has volume 0 - it will be silent. Set Volume to 1.");
            Sfx.StartLoop(kv.Key, kv.Value.soundId, ambienceFade, kv.Value.volume);
            playingKeys.Add(kv.Key);
        }
    }

    private bool Wanted(AmbienceLayer layer)
    {
        if (string.IsNullOrEmpty(layer.soundId)) return false;
        if (layer.playDuring == null || layer.playDuring.Length == 0) return true;
        foreach (DayPhase p in layer.playDuring) if (p == currentPhase) return true;
        return false;
    }

    // ================================================================ activities
    // Static entry points - called by RecoveryInteraction / Type B / Type C. Safe to call with
    // no soundscape in the scene (they just do nothing).
    public static void ActivityStarted(ActivityDefinition a) { if (instance != null) Sfx.Play(instance.Pick(a, s => s.startChime)); }
    public static void ActivityLoopStart(ActivityDefinition a) { if (instance != null) instance.StartActivityLoop(a); }
    public static void ActivityLoopStop(ActivityDefinition a) { if (instance != null) instance.StopActivityLoop(a); }
    public static void ActivityWon(ActivityDefinition a) { if (instance != null) { instance.StopActivityLoop(a); Sfx.Play(instance.Pick(a, s => s.win)); } }
    public static void ActivityLost(ActivityDefinition a) { if (instance != null) { instance.StopActivityLoop(a); Sfx.Play(instance.Pick(a, s => s.lose)); } }
    public static void ActivityInjured(ActivityDefinition a) { if (instance != null) { instance.StopActivityLoop(a); Sfx.Play(instance.Pick(a, s => s.injured)); } }
    public static void ActivityAction(ActivityDefinition a) { if (instance != null) Sfx.Play(instance.Pick(a, s => s.action)); }
    public static void ActivityMiss(ActivityDefinition a) { if (instance != null) Sfx.Play(instance.Pick(a, s => s.miss)); }

    private string ActivityLoopKey(ActivityDefinition a) => $"activity_{(a != null ? a.GetInstanceID() : 0)}";

    private void StartActivityLoop(ActivityDefinition a) => Sfx.StartLoop(ActivityLoopKey(a), Pick(a, s => s.loop), activityLoopFade);
    private void StopActivityLoop(ActivityDefinition a) => Sfx.StopLoop(ActivityLoopKey(a), activityLoopFade);

    /// <summary>The activity's own id if it has an override with that field filled in,
    /// otherwise the default.</summary>
    private string Pick(ActivityDefinition a, System.Func<ActivitySounds, string> field)
    {
        if (a != null)
        {
            foreach (ActivityOverride o in activityOverrides)
            {
                if (o.activity != a || o.sounds == null) continue;
                string id = field(o.sounds);
                if (!string.IsNullOrEmpty(id)) return id;
                break;
            }
        }
        return field(activityDefaults);
    }
}