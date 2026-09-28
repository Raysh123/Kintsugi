using UnityEngine;

/// <summary>
/// Tracker rhythm minigame — the "Interaction Type B" mechanic, reworked from
/// TimedPointer's collider-based "canHit while inside a Winstate trigger"
/// approach into a fully UI-driven one.
///
/// A tracker value pings-pongs between 0 and 1 at a constant speed. A hit
/// zone — a [zoneMin, zoneMax] band somewhere on that 0-1 range — is
/// randomized in position each attempt, and scales wider with motivation
/// (easier at higher motivation, same "gets easier as motivation rises" idea
/// used everywhere else). Press the hit key: tracker inside the zone counts
/// as a hit, outside counts as a miss.
///
///   hitCount  reaches winHitsRequired   → Won
///   missCount reaches injuryMissLimit   → Injured (ends immediately)
///   time runs out before either         → Lost
///
/// Self-contained the same way FillBarMinigame/RhythmSequenceMinigame are —
/// any wrapper script can activate it, call ResetMinigame(), poll finished,
/// then read won/lost/injured.
/// </summary>
public class TrackerRhythmMinigame : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Total time the attempt runs before it's an automatic Loss (if not already won/injured).")]
    [SerializeField] private float duration = 15f;

    [Header("Tracker Speed (motivation scaling)")]
    [Tooltip("Tracker speed (0-1 units/sec) at minimum motivation — higher means faster, harder to time.")]
    [SerializeField] private float trackerSpeedAtMinMotivation = 1f;
    [Tooltip("Tracker speed at maximum motivation — lower means slower, easier to time.")]
    [SerializeField] private float trackerSpeedAtMaxMotivation = 0.4f;
    [SerializeField] private bool scaleWithMotivation = true;
    [SerializeField] private float motivationRangeMin = 0f;
    [SerializeField] private float motivationRangeMax = 100f;

    [Header("Hit Requirements")]
    public int winHitsRequired = 10;
    public int injuryMissLimit = 3;

    [Header("Input")]
    [SerializeField] private KeyCode hitKey = KeyCode.Space;
    [SerializeField] private string hitButtonName = "Cancel";

    [Header("Hit Zone")]
    [Tooltip("Fixed width of the hit zone (0-1 range) — stays constant every attempt; only its position randomizes.")]
    [SerializeField] private float zoneWidth = 0.15f;
    [Tooltip("Keeps the zone's position away from both ends of the track, as a fraction of the 0-1 " +
             "range — e.g. 0.15 means the zone never starts before 15% or extend past 85%.")]
    [SerializeField] private float edgeMargin = 0.15f;

    [Header("Input Leeway")]
    [Tooltip("Extra forgiveness on the actual hit detection vs the visible zone, as a percent of the " +
             "zone's own width — e.g. 10 means the real hit window is 10% wider than what's shown on " +
             "screen, split evenly on both sides, so a press just before/after the visual zone still " +
             "counts as a hit. Purely a player-feel cushion — the UI still shows the smaller visual zone.")]
    [SerializeField] private float hitLeewayPercent = 10f;

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
    [Tooltip("Current tracker position, 0-1, ping-ponging between the ends.")]
    public float trackerPosition;
    public float currentTrackerSpeed;
    public float currentZoneMin;
    public float currentZoneMax;
    [Tooltip("The actual (padded) bounds used for hit detection — wider than currentZoneMin/Max by hitLeewayPercent.")]
    public float currentHitZoneMin;
    public float currentHitZoneMax;
    public int hitCount;
    public int missCount;

    [Tooltip("Fired every time the tracker moves — current position (0-1).")]
    public event System.Action<float> OnTrackerPositionChanged;
    [Tooltip("Fired once per attempt when the hit zone is (re)randomized — (zoneMin, zoneMax).")]
    public event System.Action<float, float> OnZoneChanged;
    [Tooltip("Fired whenever hitCount/missCount changes — (hitCount, missCount).")]
    public event System.Action<int, int> OnProgressChanged;
    [Tooltip("Fired specifically on a successful hit only (not misses) — for particle/juice feedback.")]
    public event System.Action OnSuccessfulHit;
    [Tooltip("Fired specifically on a wrong-timing miss only (not hits) — for reaction animation/feedback.")]
    public event System.Action OnMissedHit;
    [Tooltip("Fired specifically on the miss that pushes missCount to injuryMissLimit — the bigger, " +
             "final reaction moment, separate from every regular OnMissedHit.")]
    public event System.Action OnInjuryTriggered;
    [Tooltip("Fired once at the start of each attempt, after zone/speed are (re)randomized — a clean " +
             "signal for UI that needs to rebuild rather than just update (e.g. respawning hearts to " +
             "match injuryMissLimit, which can vary between exercises).")]
    public event System.Action OnMinigameReset;

    private float timeRemaining;
    private bool running;
    private bool movingForward = true;

    public void ResetMinigame()
    {
        _won = _lost = _injured = _finished = false;
        hitCount = 0;
        missCount = 0;
        trackerPosition = 0f;
        movingForward = true;
        timeRemaining = duration;
        running = true;

        RandomizeZone();
        RecalculateTrackerSpeed();
        OnTrackerPositionChanged?.Invoke(trackerPosition);
        OnProgressChanged?.Invoke(hitCount, missCount);
        OnMinigameReset?.Invoke();
    }

    private void Update()
    {
        if (!running || _finished) return;

        timeRemaining -= Time.deltaTime;

        MoveTracker();

        if (Input.GetKeyDown(hitKey) || Input.GetButtonDown(hitButtonName))
            HandlePress();

        if (!_finished && timeRemaining <= 0f)
        {
            _lost = true;
            EndMinigame();
        }
    }

    private void MoveTracker()
    {
        float delta = currentTrackerSpeed * Time.deltaTime * (movingForward ? 1f : -1f);
        trackerPosition += delta;

        if (trackerPosition >= 1f)
        {
            trackerPosition = 1f;
            movingForward = false;
        }
        else if (trackerPosition <= 0f)
        {
            trackerPosition = 0f;
            movingForward = true;
        }

        OnTrackerPositionChanged?.Invoke(trackerPosition);
    }

    private void HandlePress()
    {
        bool inZone = trackerPosition >= currentHitZoneMin && trackerPosition <= currentHitZoneMax;

        if (inZone)
        {
            hitCount++;
            OnProgressChanged?.Invoke(hitCount, missCount);
            OnSuccessfulHit?.Invoke();

            if (hitCount >= winHitsRequired)
            {
                _won = true;
                EndMinigame();
            }
        }
        else
        {
            missCount++;
            OnProgressChanged?.Invoke(hitCount, missCount);
            OnMissedHit?.Invoke();

            if (missCount >= injuryMissLimit)
            {
                _injured = true;
                OnInjuryTriggered?.Invoke();
                EndMinigame();
            }
        }
    }

    private void RandomizeZone()
    {
        float width = Mathf.Clamp(zoneWidth, 0.01f, 1f);
        float margin = Mathf.Clamp(edgeMargin, 0f, 0.49f);

        float minStart = margin;
        float maxStart = (1f - margin) - width;

        if (maxStart < minStart)
        {
            Debug.LogWarning($"{name}: zoneWidth plus edgeMargin on both sides leaves no room to " +
                              "randomize position — centering the zone instead.");
            currentZoneMin = Mathf.Clamp01((1f - width) * 0.5f);
        }
        else
        {
            currentZoneMin = Random.Range(minStart, maxStart);
        }

        currentZoneMax = currentZoneMin + width;

        float extraPerSide = (width * (hitLeewayPercent / 100f)) * 0.5f;
        currentHitZoneMin = Mathf.Clamp01(currentZoneMin - extraPerSide);
        currentHitZoneMax = Mathf.Clamp01(currentZoneMax + extraPerSide);

        OnZoneChanged?.Invoke(currentZoneMin, currentZoneMax);
    }

    /// <summary>Snapshotted once per attempt in ResetMinigame(), same as the other motivation-scaled values.</summary>
    private void RecalculateTrackerSpeed()
    {
        if (scaleWithMotivation && HealthAndMotivation.instance != null)
        {
            float motivation = HealthAndMotivation.instance.currentMotivation;
            float t = Mathf.InverseLerp(motivationRangeMin, motivationRangeMax, motivation);
            currentTrackerSpeed = Mathf.Lerp(trackerSpeedAtMinMotivation, trackerSpeedAtMaxMotivation, t);
        }
        else
        {
            currentTrackerSpeed = trackerSpeedAtMinMotivation;
        }
    }

    private void EndMinigame()
    {
        running = false;
        _finished = true;
    }
}