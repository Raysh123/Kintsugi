using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Owns all phase-driven environmental changes:
///   - Post-processing transitions (vignette, chromatic aberration,
///     colour adjustments, lens distortion)
///   - Player movement restrictions
///   - Activity limits based on health during BedRest
///
/// Place in the main scene. Assign the Global Volume and PlayerMovement
/// references in the Inspector.
/// </summary>
public class StoryEnvironmentController : MonoBehaviour
{
    public static StoryEnvironmentController instance;

    [Header("References")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Transition Duration")]
    [Tooltip("How long post-processing effects take to blend between phases.")]
    [SerializeField] private float transitionDuration = 4f;

    // Post-processing effect references — pulled from the Global Volume profile
    private Vignette _vignette;
    private ChromaticAberration _chromaticAberration;
    private ColorAdjustments _colorAdjustments;
    private LensDistortion _lensDistortion;

    // Target values per phase — all tunable here in one place
    // BedRest
    private const float BedVignetteIntensity = 0.55f;
    private const float BedChromaticIntensity = 0.8f;
    private const float BedSaturation = -60f;
    private const float BedLensDistortion = -0.3f;

    // Unused since DojoRejection/HouseTraining were removed — kept in case a future
    // phase or beat wants this "grey, subdued" preset. Safe to delete if not needed.
    private const float GreyVignetteIntensity = 0.35f;
    private const float GreyChromaticIntensity = 0.2f;
    private const float GreySaturation = -30f;
    private const float GreyLensDistortion = 0f;

    // FriendVisit — starting to lift
    private const float LiftVignetteIntensity = 0.2f;
    private const float LiftChromaticIntensity = 0.05f;
    private const float LiftSaturation = -10f;
    private const float LiftLensDistortion = 0f;

    // NewDojoJoin onwards — full colour, sharp, bright
    private const float ClearVignetteIntensity = 0.1f;
    private const float ClearChromaticIntensity = 0f;
    private const float ClearSaturation = 0f;
    private const float ClearLensDistortion = 0f;

    // Activity limit tracking
    private int _activitiesDoneThisPhaseDay;

    private Coroutine _transitionRoutine;

    private void Start()
    {
        instance = this;

        // Pull effect references from the volume profile
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _vignette);
            globalVolume.profile.TryGet(out _chromaticAberration);
            globalVolume.profile.TryGet(out _colorAdjustments);
            globalVolume.profile.TryGet(out _lensDistortion);
        }

        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (StoryManager.instance == null || CalendarManager.instance == null
               || HealthAndMotivation.instance == null)
            yield return null;

        StoryManager.instance.OnPhaseChanged += OnPhaseChanged;
        CalendarManager.instance.OnDayChanged += OnDayChanged;
        HealthAndMotivation.instance.OnHealthChanged += OnHealthChanged;

        // Apply correct state for whatever phase we're starting in
        ApplyPhaseImmediate(StoryManager.instance.CurrentPhase);
        ApplyMovementRestrictions(StoryManager.instance.CurrentPhase);
    }

    private void OnDestroy()
    {
        if (StoryManager.instance != null)
            StoryManager.instance.OnPhaseChanged -= OnPhaseChanged;
        if (CalendarManager.instance != null)
            CalendarManager.instance.OnDayChanged -= OnDayChanged;
        if (HealthAndMotivation.instance != null)
            HealthAndMotivation.instance.OnHealthChanged -= OnHealthChanged;
    }

    // ── Phase Changes ────────────────────────────────────────────────────────────

    private void OnPhaseChanged(StoryPhase old, StoryPhase newPhase)
    {
        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionToPhase(newPhase));
        ApplyMovementRestrictions(newPhase);
    }

    private void ApplyMovementRestrictions(StoryPhase phase)
    {
        if (playerMovement == null) return;

        bool locked = phase == StoryPhase.BedRest;
        playerMovement.movementLocked = locked;

        Debug.Log($"[Environment] Player movement {(locked ? "LOCKED" : "UNLOCKED")} for phase {phase}");
    }

    // ── Activity Limit (BedRest only) ────────────────────────────────────────────

    /// <summary>
    /// Called by RecoveryInteraction/BedInteraction before starting an activity.
    /// Returns false if the player has hit their health-based daily limit.
    /// Only enforced during BedRest — all other phases have no limit here
    /// (CalendarManager's once-per-day rule still applies regardless).
    /// </summary>
    public bool CanDoActivity()
    {
        if (StoryManager.instance.CurrentPhase != StoryPhase.BedRest)
            return true;

        int limit = GetActivityLimit();
        if (_activitiesDoneThisPhaseDay >= limit)
        {
            Debug.Log($"[Environment] Activity limit reached for today ({limit} max at current health).");
            return false;
        }

        _activitiesDoneThisPhaseDay++;
        return true;
    }

    private int GetActivityLimit()
    {
        float health = HealthAndMotivation.instance.currentHealth;
        if (health < 30f) return 1;
        if (health < 60f) return 2;
        return 3;
    }

    // Reset daily activity count each new day
    private void OnDayChanged(GameDate newDate)
    {
        _activitiesDoneThisPhaseDay = 0;
        Debug.Log($"[Environment] Daily activity count reset for {newDate}. " +
                  $"Limit today: {GetActivityLimit()} (health: {HealthAndMotivation.instance.currentHealth})");
    }

    // Re-log the limit whenever health changes, so it's visible in Console as health improves
    private void OnHealthChanged(float newHealth)
    {
        if (StoryManager.instance.CurrentPhase == StoryPhase.BedRest)
            Debug.Log($"[Environment] Health now {newHealth:F0} — activity limit: {GetActivityLimit()} per day.");
    }

    // ── Post-Processing Transitions ──────────────────────────────────────────────

    private void ApplyPhaseImmediate(StoryPhase phase)
    {
        (float vig, float chrom, float sat, float lens) = GetTargetValues(phase);
        SetPostProcessing(vig, chrom, sat, lens);
    }

    private IEnumerator TransitionToPhase(StoryPhase phase)
    {
        (float targetVig, float targetChrom, float targetSat, float targetLens)
            = GetTargetValues(phase);

        float startVig = _vignette != null ? _vignette.intensity.value : 0f;
        float startChrom = _chromaticAberration != null ? _chromaticAberration.intensity.value : 0f;
        float startSat = _colorAdjustments != null ? _colorAdjustments.saturation.value : 0f;
        float startLens = _lensDistortion != null ? _lensDistortion.intensity.value : 0f;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            // Cubic ease in-out — much smoother than SmoothStep for long transitions
            t = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            SetPostProcessing(
                Mathf.Lerp(startVig, targetVig, t),
                Mathf.Lerp(startChrom, targetChrom, t),
                Mathf.Lerp(startSat, targetSat, t),
                Mathf.Lerp(startLens, targetLens, t)
            );
            yield return null;
        }

        SetPostProcessing(targetVig, targetChrom, targetSat, targetLens);
        _transitionRoutine = null;
    }

    private (float vig, float chrom, float sat, float lens) GetTargetValues(StoryPhase phase)
    {
        switch (phase)
        {
            case StoryPhase.BedRest:
                return (BedVignetteIntensity, BedChromaticIntensity, BedSaturation, BedLensDistortion);

            case StoryPhase.FriendVisit:
                return (LiftVignetteIntensity, LiftChromaticIntensity, LiftSaturation, LiftLensDistortion);

            case StoryPhase.NewDojoJoin:
            case StoryPhase.TournamentPrep:
            case StoryPhase.TournamentDay:
                return (ClearVignetteIntensity, ClearChromaticIntensity, ClearSaturation, ClearLensDistortion);

            default:
                return (0f, 0f, 0f, 0f);
        }
    }

    private void SetPostProcessing(float vig, float chrom, float sat, float lens)
    {
        if (_vignette != null)
            _vignette.intensity.Override(vig);
        if (_chromaticAberration != null)
            _chromaticAberration.intensity.Override(chrom);
        if (_colorAdjustments != null)
            _colorAdjustments.saturation.Override(sat);
        if (_lensDistortion != null)
            _lensDistortion.intensity.Override(lens);
    }
}