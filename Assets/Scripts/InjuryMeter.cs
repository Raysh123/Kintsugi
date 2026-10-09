using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The player's injury meter. Every attack and every block the player performs adds to it; when
/// it fills, the fight ends, the player's HP is reduced and he's sent back to the RPG scene.
///
/// How much each action fills depends on the player's HP (HealthAndMotivation.currentHealth),
/// which recovery activities raise: low HP = each action hurts a lot, high HP = barely at all.
/// The fill is a straight line between two points you set in the Inspector, e.g. 33% per action
/// at 10 HP down to 6% per action at 100 HP.
///
/// Always remember to add InputHandler in the scene and reference it properly.
/// </summary>
public class InjuryMeter : MonoBehaviour
{
    public static InjuryMeter instance;

    public Slider injuryMeterValue;

    [Header("Fill per action (depends on player HP)")]
    [Tooltip("HP at (or below) which actions fill the meter the most.")]
    [SerializeField] private float lowHealth = 10f;
    [Tooltip("HP at (or above) which actions fill the meter the least.")]
    [SerializeField] private float highHealth = 100f;
    [Range(0f, 1f)]
    [Tooltip("Meter fill per action at Low Health, as a fraction of the full meter (0.33 = 33%).")]
    [SerializeField] private float fillAtLowHealth = 0.33f;
    [Range(0f, 1f)]
    [Tooltip("Meter fill per action at High Health (0.06 = 6%). HP in between is interpolated.")]
    [SerializeField] private float fillAtHighHealth = 0.06f;
    [Tooltip("Multiplier on the fill for a PUNCH (below 1 = punches hurt less).")]
    [SerializeField] private float punchFillMultiplier = 0.7f;
    [Tooltip("Multiplier on the fill for a KICK (above 1 = kicks cost more).")]
    [SerializeField] private float kickFillMultiplier = 1.4f;
    [Tooltip("Multiplier on the fill for a dodge (kept small - a dodge only chips at the meter).")]
    [SerializeField] private float dodgeFillMultiplier = 0.3f;
    [Tooltip("Multiplier on the fill for a block.")]
    [SerializeField] private float blockFillMultiplier = 1f;
    [Tooltip("A block's fill is held back for the perfect-block window plus this many extra " +
             "seconds. If a perfect block lands in that time, the block costs nothing and the " +
             "meter never moves - otherwise it fills once the time is up.")]
    [SerializeField] private float blockFillExtraDelay = 0.03f;
    [Tooltip("HP used when there's no HealthAndMotivation in the scene (e.g. testing the fight " +
             "scene on its own).")]
    [SerializeField] private float fallbackHealth = 50f;

    [Header("Draining")]
    [Tooltip("How much the meter drops per SECOND while the match is running. This used to be " +
             "applied per FRAME (about 60x faster), which emptied the meter almost instantly. " +
             "0 = it never drains.")]
    public float injuryMeterDepleteRate = 0.02f;
    [Tooltip("Optional - if set, the meter doesn't drain while the match is stopped (intro, " +
             "pause after a point).")]
    [SerializeField] private MatchTimer matchTimer;

    [Header("When the meter fills")]
    [Range(0.5f, 1f)]
    [Tooltip("Meter value (0-1) that counts as full.")]
    [SerializeField] private float fullThreshold = 0.98f;
    [Tooltip("HP removed from the player's health when the meter fills.")]
    [SerializeField] private float healthLossOnInjury = 15f;
    [Tooltip("Real seconds between the meter filling and the scene change - room for a " +
             "referee call / injured animation.")]
    [SerializeField] private float endFightDelay = 2f;
    [Tooltip("Fired the moment the meter fills (play an injured animation, show text, etc.). " +
             "The match is already frozen when this fires.")]
    public UnityEvent onInjuryKnockout;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private float lastBlockFill;
    private float pendingBlockFill;
    private Coroutine pendingBlockRoutine;
    private bool ending;
    private float statusLogTimer;

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Update()
    {
        if (injuryMeterValue == null || ending) return;

        if (injuryMeterValue.value >= fullThreshold)
        {
            ending = true;
            StartCoroutine(EndFightInjured());
            return;
        }

        bool paused = matchTimer != null && matchTimer.matchStopped;
        if (debugLog && injuryMeterValue.value > 0f)
        {
            statusLogTimer -= Time.unscaledDeltaTime;
            if (statusLogTimer <= 0f)
            {
                statusLogTimer = 2f;
                Debug.Log($"[Injury] status: value={injuryMeterValue.value:F3} (slider max={injuryMeterValue.maxValue}, " +
                          $"whole numbers={injuryMeterValue.wholeNumbers}) rate={injuryMeterDepleteRate}/s " +
                          $"matchStopped={(matchTimer != null ? matchTimer.matchStopped.ToString() : "no timer assigned")} " +
                          $"-> {(paused ? "NOT draining (match stopped)" : (injuryMeterDepleteRate <= 0f ? "NOT draining (rate is 0)" : "draining"))}");
            }
        }
        if (!paused && injuryMeterDepleteRate > 0f)
            injuryMeterValue.value = Mathf.Max(0f, injuryMeterValue.value - injuryMeterDepleteRate * Time.deltaTime);
    }

    private float CurrentHealth()
    {
        return HealthAndMotivation.instance != null
            ? HealthAndMotivation.instance.currentHealth
            : fallbackHealth;
    }

    /// <summary>Fill per action for the player's current HP (fraction of the full meter).</summary>
    public float GetFillPerAction()
    {
        float t = Mathf.InverseLerp(lowHealth, highHealth, CurrentHealth());
        return Mathf.Lerp(fillAtLowHealth, fillAtHighHealth, t);
    }

    private void Add(float amount, string what)
    {
        if (injuryMeterValue == null || ending) return;
        injuryMeterValue.value = Mathf.Clamp01(injuryMeterValue.value + amount);
        if (debugLog)
            Debug.Log($"[Injury] {what} +{amount * 100f:F1}% (HP {CurrentHealth():F0}) -> {injuryMeterValue.value * 100f:F0}%");
    }

    /// <summary>Call when the player starts an attack.</summary>
    public void AddAttackInjury(bool isKick)
    {
        float mult = isKick ? kickFillMultiplier : punchFillMultiplier;
        Add(GetFillPerAction() * mult, isKick ? "kick" : "punch");
    }

    /// <summary>Call when the player starts a dodge.</summary>
    public void AddDodgeInjury()
    {
        Add(GetFillPerAction() * dodgeFillMultiplier, "dodge");
    }

    /// <summary>Call when the player presses block. The fill is held back briefly so a
    /// perfect block (RefundLastBlock) can cancel it before the meter ever moves.</summary>
    public void AddBlockInjury()
    {
        // A previous block still waiting? Settle it now rather than losing it.
        if (pendingBlockRoutine != null) CommitPendingBlock();

        pendingBlockFill = GetFillPerAction() * blockFillMultiplier;
        pendingBlockRoutine = StartCoroutine(PendingBlockRoutine());
    }

    private IEnumerator PendingBlockRoutine()
    {
        float window = PlayerStateMachine.instance != null ? PlayerStateMachine.instance.PerfectBlockWindow : 0.15f;
        yield return new WaitForSeconds(window + blockFillExtraDelay);
        pendingBlockRoutine = null;
        CommitPendingBlockFill();
    }

    private void CommitPendingBlock()
    {
        if (pendingBlockRoutine != null)
        {
            StopCoroutine(pendingBlockRoutine);
            pendingBlockRoutine = null;
        }
        CommitPendingBlockFill();
    }

    private void CommitPendingBlockFill()
    {
        lastBlockFill = pendingBlockFill;
        pendingBlockFill = 0f;
        Add(lastBlockFill, "block");
    }

    /// <summary>Called when a perfect block lands: cancels the block's pending fill so it costs
    /// nothing (or refunds it if it somehow already went in).</summary>
    public void RefundLastBlock()
    {
        if (pendingBlockRoutine != null)
        {
            StopCoroutine(pendingBlockRoutine);
            pendingBlockRoutine = null;
            pendingBlockFill = 0f;
            if (debugLog) Debug.Log("[Injury] perfect block - block cost cancelled before it filled.");
            return;
        }
        if (lastBlockFill <= 0f || injuryMeterValue == null || ending) return;
        injuryMeterValue.value = Mathf.Clamp01(injuryMeterValue.value - lastBlockFill);
        if (debugLog)
            Debug.Log($"[Injury] perfect block refund -{lastBlockFill * 100f:F1}% -> {injuryMeterValue.value * 100f:F0}%");
        lastBlockFill = 0f;
    }

    private IEnumerator EndFightInjured()
    {
        if (debugLog)
            Debug.Log($"[Injury] Meter full - ending the fight. HP {CurrentHealth():F0} -> {Mathf.Max(0f, CurrentHealth() - healthLossOnInjury):F0}.");

        if (matchTimer != null) matchTimer.matchStopped = true;
        onInjuryKnockout?.Invoke();

        if (HealthAndMotivation.instance != null)
            HealthAndMotivation.instance.ModifyHealth(-healthLossOnInjury);
        else
            Debug.LogWarning("[Injury] No HealthAndMotivation in the scene - HP was not reduced.");

        // Hold the match frozen for the delay (a pending post-point resume must not restart it).
        float t = 0f;
        while (t < endFightDelay)
        {
            if (matchTimer != null) matchTimer.matchStopped = true;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Counts as a loss for the RPG scene (PostFightHandler plays the lose dialogue).
        FightSessionManager.ReportResult(false);
    }
}