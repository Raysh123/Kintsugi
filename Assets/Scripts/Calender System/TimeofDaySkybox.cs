using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a global day/night look via 4 presets, one per DayPhase (Morning/Afternoon/Evening/
/// Night) - skybox material, sun rotation, sun color/intensity. Applies immediately on
/// OnEnable (so it's correct the moment the scene loads) and again every time
/// CalendarManager.OnPhaseChanged fires.
///
/// Deliberately a hard swap, not a crossfade - CalendarManager's phase only changes while
/// traveling or asleep (confirmed: nothing changes mid-scene while the player is actually
/// watching, e.g. sitting on the park bench), so there's never a visible pop to smooth over.
/// If that ever stops being true for some new location, this would need the Skybox/Blended
/// shader + a lerped _Blend value instead of the direct material swap below.
///
/// RenderSettings.skybox is global/scene-wide - if multiple locations share one scene, the
/// swap applies everywhere at once, not just around whichever location triggered it. That's
/// intentional per your call (bench-specific skybox would need a second camera with its own
/// Skybox component override instead of RenderSettings, since RenderSettings can't differ
/// per-camera).
/// </summary>
public class TimeOfDaySkybox : MonoBehaviour
{
    [System.Serializable]
    public class LightingPreset
    {
        public DayPhase phase;

        [Tooltip("Assigned as RenderSettings.skybox for this phase. Give each preset its OWN " +
                 "material - Skybox Rotation below is written directly onto whichever material " +
                 "is assigned here, so sharing one material across two presets that need " +
                 "different rotations will fight each other.")]
        public Material skybox;

        [Header("Sun (Directional Light)")]
        [Tooltip("Elevation/pitch - how high the sun sits. Low (~10-20°) for Morning/Evening, " +
                 "high (~60-90°) for Afternoon, low or negative for Night.")]
        public float sunRotationX;
        [Tooltip("Heading - which direction (compass-wise) the light comes from. Tune " +
                 "together with Skybox Rotation below so the texture's painted sun/horizon " +
                 "glow lines up with where the actual light is pointing.")]
        public float sunRotationY;
        [Tooltip("Written directly onto this preset's Skybox material's _Rotation property, " +
                 "so the baked-in sun/horizon art can be turned to match Sun Rotation Y.")]
        public float skyboxRotation;

        public Color lightColor = Color.white;
        [Range(0f, 8f)] public float lightIntensity = 1f;
    }

    [Tooltip("The scene's directional light.")]
    [SerializeField] private Light sun;

    [Tooltip("One entry per DayPhase - Morning, Afternoon, Evening, Night.")]
    [SerializeField] private List<LightingPreset> presets = new List<LightingPreset>();

    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribeAndApply();
    }

    private void Start()
    {
        // OnEnable can run before CalendarManager.Awake sets its instance (script order
        // between different objects isn't guaranteed) - Start() always runs after every
        // Awake, so retry here if OnEnable couldn't connect.
        if (!subscribed) TrySubscribeAndApply();

        if (!subscribed)
        {
            Debug.LogWarning("[TimeOfDaySkybox] CalendarManager.instance still null after Start() - " +
                              "no lighting will ever be applied. Is there a CalendarManager in this scene?");
        }
    }

    private void TrySubscribeAndApply()
    {
        if (CalendarManager.instance == null) return;

        ApplyPreset(CalendarManager.instance.CurrentPhase);
        CalendarManager.instance.OnPhaseChanged += ApplyPreset;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && CalendarManager.instance != null)
            CalendarManager.instance.OnPhaseChanged -= ApplyPreset;
        subscribed = false;
    }

    private void ApplyPreset(DayPhase phase)
    {
        LightingPreset preset = presets.Find(p => p.phase == phase);
        if (preset == null)
        {
            Debug.LogWarning($"[TimeOfDaySkybox] No preset configured for {phase} - skipping.");
            return;
        }

        if (preset.skybox != null)
        {
            RenderSettings.skybox = preset.skybox;
            if (preset.skybox.HasProperty("_Rotation"))
                preset.skybox.SetFloat("_Rotation", preset.skyboxRotation);
        }

        Light light = sun != null ? sun : RenderSettings.sun;
        if (light == null)
        {
            Debug.LogWarning("[TimeOfDaySkybox] Sun is not assigned (and RenderSettings.sun is empty " +
                              "too) - the directional light will NOT change. Drag your directional " +
                              "light into this component's Sun field.");
        }
        else
        {
            light.transform.rotation = Quaternion.Euler(preset.sunRotationX, preset.sunRotationY, 0f);
            light.color = preset.lightColor;
            light.intensity = preset.lightIntensity;

#if UNITY_EDITOR
            if (light.lightmapBakeType == LightmapBakeType.Baked)
            {
                Debug.LogWarning($"[TimeOfDaySkybox] '{light.name}' is set to Baked mode - a baked light " +
                                  "ignores runtime changes entirely, so its rotation/color/intensity " +
                                  "will have no visible effect. Set its Mode to Realtime or Mixed.");
            }
#endif
        }

        // Ambient/GI lighting is derived from the skybox - without this it can lag a frame
        // or more behind the hard swap above, showing the old ambient tint briefly.
        DynamicGI.UpdateEnvironment();

        Debug.Log($"[TimeOfDaySkybox] Applied {phase} preset -> light '{(light != null ? light.name : "NONE")}' " +
                  $"rot=({preset.sunRotationX}, {preset.sunRotationY}) intensity={preset.lightIntensity}.");
    }
}