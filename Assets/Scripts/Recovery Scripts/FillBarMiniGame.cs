using UnityEngine;

/// <summary>
/// Fill-and-drain recovery minigame — the "Interaction Type A" mechanic,
/// reworked from Pointer's jump-and-land-in-a-collider approach into a
/// single bar the player has to keep topped up.
///
/// Press the fill key to add to the bar; it drains continuously on its own.
/// At the end of a fixed duration, wherever the bar landed determines the
/// outcome via two percentage cutoffs instead of three physical zones:
///
///   fill <= injuryThreshold           → Injured
///   injuryThreshold < fill < winThreshold → Lost
///   fill >= winThreshold              → Won
///
/// Both cutoffs shift with HealthAndMotivation.instance.currentMotivation —
/// the same "win zone grows, injury zone shrinks as motivation rises" idea
/// MeterSize1 expressed through collider scale, just applied to these two
/// numbers instead.
///
/// Implements IRecoveryMinigame, so it's self-contained the same way Pointer
/// and RhythmSequenceMinigame are — any wrapper script can activate it, call
/// ResetMinigame(), poll finished, then read won/lost/injured.
/// </summary>
public class FillBarMinigame : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Total time the minigame runs before the current fill level locks in the outcome.")]
    [SerializeField] private float duration = 10f;

    [Header("Fill")]
    [SerializeField] private float fillPerPress = 15f;
    [Tooltip("Used only when Scale With Motivation is off — otherwise drainAtMin/MaxMotivation below take over.")]
    [SerializeField] private float drainPerSecond = 20f;
    [SerializeField] private KeyCode fillKey = KeyCode.Space;
    [SerializeField] private string fillButtonName = "Cancel";

    [Header("Thresholds (percent, 0-100, before motivation scaling)")]
    [Tooltip("Fill% at or above this is a Win.")]
    [SerializeField] private float baseWinThreshold = 70f;
    [Tooltip("Fill% at or below this is an Injury.")]
    [SerializeField] private float baseInjuryThreshold = 25f;

    [Header("Motivation Difficulty Scaling")]
    [SerializeField] private bool scaleWithMotivation = true;
    [SerializeField] private float motivationRangeMin = 0f;
    [SerializeField] private float motivationRangeMax = 100f;
    [Tooltip("Win threshold at minimum motivation — higher means harder to reach Win.")]
    [SerializeField] private float winThresholdAtMinMotivation = 85f;
    [Tooltip("Win threshold at maximum motivation — lower means the win zone is bigger/easier.")]
    [SerializeField] private float winThresholdAtMaxMotivation = 45f;
    [Tooltip("Injury threshold at minimum motivation — higher means the injury zone is bigger/riskier.")]
    [SerializeField] private float injuryThresholdAtMinMotivation = 40f;
    [Tooltip("Injury threshold at maximum motivation — lower means the injury zone is smaller/safer.")]
    [SerializeField] private float injuryThresholdAtMaxMotivation = 10f;

    [Tooltip("Drain per second at minimum motivation — higher means the bar empties faster, harder to keep up.")]
    [SerializeField] private float drainAtMinMotivation = 50f;
    [Tooltip("Drain per second at maximum motivation — lower means the bar empties slower, easier to keep up.")]
    [SerializeField] private float drainAtMaxMotivation = 15f;

    [Header("Outcome")]
    [SerializeField] private bool _won;
    [SerializeField] private bool _lost;
    [SerializeField] private bool _injured;
    [SerializeField] private bool _finished;
    public bool won => _won;
    public bool lost => _lost;
    public bool injured => _injured;
    public bool finished => _finished;

    [Header("Progress (read-only, drives the UI)")]
    public float currentFillPercent;
    public float currentWinThreshold;
    public float currentInjuryThreshold;
    public float currentDrainPerSecond;

    [Tooltip("Fired every time the fill level changes — current percent (0-100).")]
    public event System.Action<float> OnFillChanged;
    [Tooltip("Fired once at the start of each attempt — (injuryThreshold, winThreshold), for the UI to position its band markers.")]
    public event System.Action<float, float> OnThresholdsChanged;

    private float timeRemaining;
    private bool running;

    /// <summary>Call to (re)start an attempt — same role as RhythmSequenceMinigame.ResetMinigame().</summary>
    public void ResetMinigame()
    {
        _won = _lost = _injured = _finished = false;
        currentFillPercent = 0f;
        timeRemaining = duration;
        running = true;

        RecalculateThresholds();
        RecalculateDrainRate();
        OnFillChanged?.Invoke(currentFillPercent);
    }

    private void Update()
    {
        if (!running || _finished) return;

        timeRemaining -= Time.deltaTime;

        if (Input.GetKeyDown(fillKey) || Input.GetButtonDown(fillButtonName))
            currentFillPercent += fillPerPress;

        currentFillPercent -= currentDrainPerSecond * Time.deltaTime;
        currentFillPercent = Mathf.Clamp(currentFillPercent, 0f, 100f);

        OnFillChanged?.Invoke(currentFillPercent);

        if (timeRemaining <= 0f)
            EndMinigame();
    }

    /// <summary>Snapshotted once per attempt in ResetMinigame(), same as RecalculateThresholds — " +
    /// motivation is read once at the start of the attempt, not live every frame.</summary>
    private void RecalculateDrainRate()
    {
        if (scaleWithMotivation && HealthAndMotivation.instance != null)
        {
            float motivation = HealthAndMotivation.instance.currentMotivation;
            float t = Mathf.InverseLerp(motivationRangeMin, motivationRangeMax, motivation);
            currentDrainPerSecond = Mathf.Lerp(drainAtMinMotivation, drainAtMaxMotivation, t);
        }
        else
        {
            currentDrainPerSecond = drainPerSecond;
        }
    }

    private void RecalculateThresholds()
    {
        float winThreshold = baseWinThreshold;
        float injuryThreshold = baseInjuryThreshold;

        if (scaleWithMotivation && HealthAndMotivation.instance != null)
        {
            float motivation = HealthAndMotivation.instance.currentMotivation;
            float t = Mathf.InverseLerp(motivationRangeMin, motivationRangeMax, motivation);
            winThreshold = Mathf.Lerp(winThresholdAtMinMotivation, winThresholdAtMaxMotivation, t);
            injuryThreshold = Mathf.Lerp(injuryThresholdAtMinMotivation, injuryThresholdAtMaxMotivation, t);
        }

        currentWinThreshold = winThreshold;
        currentInjuryThreshold = injuryThreshold;
        OnThresholdsChanged?.Invoke(currentInjuryThreshold, currentWinThreshold);
    }

    private void EndMinigame()
    {
        running = false;
        _finished = true;

        if (currentFillPercent >= currentWinThreshold)
            _won = true;
        else if (currentFillPercent <= currentInjuryThreshold)
            _injured = true;
        else
            _lost = true;
    }
}