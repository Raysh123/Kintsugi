using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using static PlayerStateMachine;

public class EnemyStateMachine : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Approach,
        Attack,
        Block,
        Retreat,
        Hurt,
        Stun,
        Swap      // being carried across by the player's swap: does nothing at all
    }
    public EnemyState currentState;
    public Transform Player;
    public bool shouldApproach;
    public bool shouldRetreat;
    [SerializeField]
    float moveSpeed = 10f;
    [Header("Aggression (scales the values below, so it works even if the Inspector overrides them)")]
    [Tooltip("1 = exactly the values below. 1.5 = moves 50% faster, re-decides 50% sooner, attack " +
             "cooldown 33% shorter, attack chance 50% higher (capped at 100%).")]
    [Range(0.5f, 3f)]
    [SerializeField] private float aggression = 1.5f;
    [Tooltip("While he is farther than this from the player he sprints.")]
    [SerializeField] private float chaseSprintDistance = 2.5f;
    [Tooltip("Speed multiplier while sprinting in from far away.")]
    [SerializeField] private float chaseSprintMultiplier = 1.4f;
    [Tooltip("Added to Attack Range: he starts attacking from this much farther away (his lunge " +
             "closes the rest of the gap). 0 = old behaviour.")]
    [SerializeField] private float attackRangeBonus = 0.6f;

    private float Aggro => Mathf.Max(0.1f, aggression);
    private float DecisionInterval => decisionInterval / Aggro;
    private float EngageRange => attackRange + attackRangeBonus;

    [Header("Attack Tempo")]
    [Tooltip("How often (seconds) he re-rolls his decisions, including 'do I attack now?'. " +
             "Lower = he reacts and attacks sooner.")]
    [SerializeField] private float decisionInterval = 0.5f;
    [Tooltip("Minimum seconds between the START of one attack and the next. Was hardcoded at 3.")]
    [SerializeField] private float attackCooldown = 1.4f;
    [Range(0f, 100f)]
    [Tooltip("Chance (%) that, once in range and off cooldown, he actually launches an attack on " +
             "each decision roll. Was effectively 50.")]
    [SerializeField] private float attackChancePercent = 75f;

    [Header("Walking")]
    [Tooltip("Animator state played while he moves TOWARD the player.")]
    [SerializeField] private string walkForwardAnim = "Walk_Forward";
    [Tooltip("Animator state played while he moves AWAY from the player.")]
    [SerializeField] private string walkBackwardAnim = "Walk_Backward";
    [Tooltip("How quickly he gets up to Move Speed (units/second per second). Higher = snappier " +
             "start, lower = a slower build-up.")]
    [SerializeField] private float moveAcceleration = 25f;
    [Tooltip("How quickly he slows down when he stops.")]
    [SerializeField] private float moveDeceleration = 40f;
    [Tooltip("Within this distance of his stopping point he eases off instead of arriving at full " +
             "speed, so he doesn't overshoot into the player.")]
    [SerializeField] private float approachSlowRadius = 1.0f;
    private float moveVelX;

    [Header("Debug")]
    [Tooltip("Logs one line per AI decision (every Decision Interval): his state, distance, attack " +
             "cooldown, the roll, and the movement flags - plus a line whenever an attack starts. " +
             "Turn on, play a bit, and paste the Console if he isn't behaving.")]
    [SerializeField] private bool debugAI = false;

    [Header("Close Range (stops him walking into the player)")]
    [Tooltip("He stops walking toward the player once he's this close. Was effectively 0.5, which " +
             "is why he kept shuffling forward while already touching the player. Keep it " +
             "smaller than Attack Range so he stops INSIDE attack range.")]
    [SerializeField] private float approachStopDistance = 1.2f;

    [Header("Back Dash (replaces the old walk-away retreat)")]
    [Tooltip("Animator state played during the dash. Falls back to Walk Backward if no state " +
             "with this name exists.")]
    [SerializeField] private string backDashAnim = "Dash_Backward";
    [Tooltip("How far the dash carries him straight away from the player.")]
    [SerializeField] private float backDashDistance = 1.8f;
    [Tooltip("How long the dash takes, in seconds.")]
    [SerializeField] private float backDashDuration = 0.3f;
    [Range(0f, 100f)]
    [Tooltip("When he's inside attack range and the dash cooldown is over, the chance (%) per " +
             "decision roll (every Decision Interval) that he back-dashes instead of attacking.")]
    [SerializeField] private float backDashChancePercent = 35f;
    [Range(0f, 100f)]
    [Tooltip("Chance (%) that after a dash he goes straight back in and attacks (ignores his " +
             "attack cooldown and roll). Otherwise he just returns to Idle and resumes normal AI.")]
    [SerializeField] private float attackAfterDashChancePercent = 50f;
    [Header("Guaranteed-ish Dodges (back dash in recovery situations)")]
    [Range(0f, 100f)]
    [Tooltip("When his combo is broken (perfect block) and his short recovery freeze ends, the " +
             "chance (%) he back-dashes out instead of just resuming.")]
    [SerializeField] private float dodgeAfterComboBreakPercent = 50f;
    [Range(0f, 100f)]
    [Tooltip("Once per stun: the chance (%) that at a random moment in the final part of the stun he " +
             "dashes out of it early (posture is dropped just under the stun threshold so he " +
             "isn't instantly re-stunned).")]
    [SerializeField] private float dodgeDuringStunPercent = 10f;
    [Range(1f, 100f)]
    [Tooltip("The stun dodge can only happen in the LAST this-many percent of the stun " +
             "(20 = the final 2 seconds of a 10s stun).")]
    [SerializeField] private float stunDodgeWindowPercent = 20f;
    [Range(0f, 100f)]
    [Tooltip("When a stun ends, the chance (%) he back-dashes right away instead of going " +
             "straight back to normal AI.")]
    [SerializeField] private float dodgeAfterStunPercent = 80f;
    [Tooltip("How long a posture-break stun lasts, in seconds. This used to be hardcoded at 10.")]
    [SerializeField] private float stunDuration = 3f;
    [Range(0f, 1f)]
    [Tooltip("Posture value he's set to when a stun ends (including dodging out of it). 0 = empty, " +
             "so he can't be stunned again straight away.")]
    [SerializeField] private float postureAfterStun = 0f;
    private bool parryRecoveryPending;
    private Coroutine backDashRoutine;
    private bool attackNextOpportunity;
    private float attackNextTimer;

    [Header("Lunge (step in while attacking)")]
    [Tooltip("How far his attack can actually reach. If he starts an attack from further away " +
             "than this (but still inside Attack Range) he can lunge forward during the " +
             "wind-up, like the step-in between combo hits, instead of whiffing from out of reach. " +
             "Tune it to roughly where the hitbox really lands.")]
    [SerializeField] private float hitRange = 1.3f;
    [Range(0f, 100f)]
    [Tooltip("Chance (%) he lunges when he starts an attack from outside Hit Range. " +
             "0 = never (he just swings from where he is), 100 = always.")]
    [SerializeField] private float lungeChancePercent = 60f;
    [Tooltip("Furthest a single lunge can carry him, in units.")]
    [SerializeField] private float maxLungeDistance = 1.5f;
    [Tooltip("How long the lunge takes, in seconds. Shorter = snappier. Should finish before " +
             "the attack's first hit window opens.")]
    [SerializeField] private float lungeDuration = 0.2f;
    float decisionTimer;
    float minDist = 0.5f;
    float maxDist = 2f;

    [Header("Back Dash Cooldown (scales with posture)")]
    [Tooltip("Average seconds between back dashes when posture is empty (0).")]
    [SerializeField] private float retreatCooldownAtZeroPosture = 6f;
    [Tooltip("Average seconds between retreats when posture is maxed (near stun) - lower than " +
             "the zero-posture cooldown so he backs off more often the more rattled he is.")]
    [SerializeField] private float retreatCooldownAtMaxPosture = 1.5f;
    [Tooltip("Random +/- applied around whichever average cooldown posture currently gives - " +
             "this is the 'randomly' part now that approach itself is no longer a coin flip.")]
    [SerializeField] private float retreatCooldownJitter = 1.5f;
    private float retreatTimer;

    [Tooltip("Master switch for the breathers below. OFF = he never stops mid-chase to stand " +
             "still (this is what was making him walk, stop, walk, stop).")]
    [SerializeField] private bool enableIdleBreathers = false;
    [Tooltip("Average seconds of nonstop chasing before he takes a breather - this is what " +
             "stops the approach from reading as relentless/robotic. Randomized between Min " +
             "and Max each time.")]
    [SerializeField] private float idlePauseCooldownMin = 2f;
    [SerializeField] private float idlePauseCooldownMax = 5f;
    [Tooltip("How long a single breather lasts, randomized between Min and Max each time.")]
    [SerializeField] private float idlePauseDurationMin = 0.5f;
    [SerializeField] private float idlePauseDurationMax = 1.5f;
    private float idlePauseTimer;
    private float idlePauseElapsed;
    private float idlePauseDurationCurrent;
    private bool isIdlePausing;

    [Header("Perfect-Block Recovery")]
    [Tooltip("After the player perfect-blocks him, he freezes in place (no approach, no " +
             "retreat, no re-attacking) for this many seconds before resuming normal AI. " +
             "Without this he'd immediately start closing the distance again the instant the " +
             "block happens, which reads as him not reacting to it at all.")]
    [SerializeField] private float parryRecoveryDuration = 0.5f;
    private float decisionLockTimer;

    [Header("Counter Window (after YOU perfect-block him)")]
    [Tooltip("After a perfect block he is defenseless for this many seconds: he won't block, and a PUNCH " +
             "that lands in this time scores. Tune it against your punch animation: long enough for the " +
             "punch to connect, shorter than the kick's wind-up so a kick arrives too late. " +
             "(He also stays frozen at least this long.)")]
    [SerializeField] private float counterWindowSeconds = 0.8f;
    [Range(0f, 100f)]
    [Tooltip("Chance (%) that he slips a punch thrown in the window (back dash, the hit misses). " +
             "Rolled once per perfect block.")]
    [SerializeField] private float punchDodgeChancePercent = 12f;
    [Tooltip("A KICK thrown after a perfect block is always answered: he reacts as it starts and either blocks it or " +
             "dodges it (see Kick Dodge Chance). Only punches are the counter.")]
    [SerializeField] private bool alwaysDodgeKickInWindow = true;
    [Range(0f, 100f)]
    [Tooltip("When a kick comes in the counter window: chance (%) he dodges it (back dash). Otherwise he blocks it.")]
    [SerializeField] private float kickDodgeChancePercent = 50f;
    [Tooltip("Kicks are also ignored for this many seconds AFTER the counter window ends (a kick's wind-up is " +
             "longer than a punch's, so it lands late). Total kick immunity = Counter Window + this.")]
    [SerializeField] private float kickImmuneExtraSeconds = 0.7f;
    private float kickImmuneUntil;
    private float counterOpenUntil;   // Time.time based, so it works even if Update is gated
    private float counterWindowStart;
    private bool counterDodgeRolled;

    /// <summary>True for a short time after the player perfect-blocks him - he's open to a punch.</summary>
    public bool InCounterWindow => Time.time < counterOpenUntil;

    /// <summary>Called by DmgDetection when a player attack is about to land on him. Returns true if he
    /// slips it instead (the hit must then be ignored). Only ever true inside the counter window.</summary>
    public bool TryCounterDodge(bool isKick)
    {
        // Safety net only: HandleIdle already makes him block or dodge a kick as it STARTS. If one still reaches him
        // after a perfect block (he was mid-dash, or in a state that couldn't react), he slips it - it never scores.
        // (DmgDetection checks his Block state first, so a kick he blocked never gets here.)
        if (isKick && alwaysDodgeKickInWindow && (InCounterWindow || Time.time < kickImmuneUntil))
        {
            { }
            if (currentState == EnemyState.Idle)
            {
                counterOpenUntil = 0f;
                decisionLockTimer = 0f;
                StartBackDash();
            }
            return true;   // the hit is ignored whether or not he could dash
        }

        if (!InCounterWindow || currentState != EnemyState.Idle) return false;

        float into = Time.time - counterWindowStart;
        bool dodge = isKick ? alwaysDodgeKickInWindow : counterDodgeRolled;
        { }
        if (!dodge) return false;

        counterOpenUntil = 0f;
        decisionLockTimer = 0f;
        StartBackDash();
        return backDashRoutine != null;
    }
    public Animator anim;
    float decisionRoll;
    bool isBlocking;
    [SerializeField] private float facingRotationSpeed = 50f;
    public Transform opponent;
    private float attackTimer;
    private float blockTimer;
    //private float blockCooldown = 2f;
    public Vector3 enemyInitPos;
    private bool isAttacking;
    [SerializeField]
    private MatchTimer matchTimer;
    [SerializeField] List<DmgDetection> hitBoxes;
    [SerializeField] private PlayerStateMachine playerStateMachine;
    private PlayerStateMachine.AttackTypes incomingAttackTypes;
    private bool hasBlockedCurrentPlayerAttack;
    [SerializeField] private Posture enemyPosture;
    [SerializeField] private float moveSpeedInCombo = 0.5f;
    // ------------------------------------------------------------------ combos
    [Header("Combos (he picks one each attack)")]
    [Tooltip("Type the Animator STATE name of each combo (one per entry). He picks one at random every attack. " +
             "Empty list = the old single combo 'InPlaceCombo1'. Names that don't exist on the Animator yet are skipped.")]
    [SerializeField] private List<string> comboAnimations = new List<string>();
    [Tooltip("Don't pick the same combo twice in a row (when more than one is available).")]
    [SerializeField] private bool avoidRepeatingCombo = true;
    [SerializeField] private bool logComboChoice = true;
    private int lastComboIndex = -1;
    /// <summary>Name of the combo he is (or last was) performing.</summary>
    public string CurrentComboName { get; private set; } = "InPlaceCombo1";

    [Tooltip("Combo states that snap (no blend) when a parry / block / hit cuts them off - for clips whose pose is mid-turn and makes him spin when blended out of. All other combos blend normally.")]
    [SerializeField] private string[] snapWhenInterruptedCombos = { "InPlaceCombo2" };

    /// <summary>True while he is in one of the combos listed above.</summary>
    public bool InSnapCombo()
    {
        if (anim == null || snapWhenInterruptedCombos == null) return false;
        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
        foreach (string n in snapWhenInterruptedCombos)
            if (!string.IsNullOrEmpty(n) && st.IsName(n)) return true;
        return false;
    }

    public void BlendInterrupt(string stateName)
    {
        if (anim == null) return;
        anim.PlayBlend(stateName, InSnapCombo() ? 0f : -1f);
    }

    // Random choice among the combos that exist on the Animator. Returns null for the legacy single combo.
    private string PickCombo()
    {
        if (comboAnimations == null || comboAnimations.Count == 0) { lastComboIndex = -1; return null; }

        var candidates = new List<int>();
        for (int i = 0; i < comboAnimations.Count; i++)
        {
            string n = comboAnimations[i];
            if (string.IsNullOrWhiteSpace(n)) continue;
            if (anim != null && !anim.HasState(0, Animator.StringToHash(n.Trim()))) continue;   // not set up yet
            candidates.Add(i);
        }
        if (candidates.Count == 0) { lastComboIndex = -1; return null; }
        if (avoidRepeatingCombo && candidates.Count > 1) candidates.Remove(lastComboIndex);

        int chosen = candidates[Random.Range(0, candidates.Count)];
        lastComboIndex = chosen;
        return comboAnimations[chosen].Trim();
    }

    private Coroutine attackRoutine;
    public string requiredTag;
    private float attackLength;
    [Tooltip("Played immediately on entering Stun - this is what actually cuts off the " +
             "combo animation. Stopping AttackRoutine's coroutine alone only stops the " +
             "underlying hit-window logic; it doesn't touch whatever the Animator is " +
             "currently playing, so without this the attack animation (e.g. InPlaceCombo1) " +
             "just keeps playing to its natural end even though the attack was cancelled.")]
    [SerializeField] private string stunAnimationName = "Stun"; // promoted from a local var so OpenEnemyHitWindow/CloseEnemyHitWindow (separate Animation-Event-called methods) can read it too
    [Tooltip("Played immediately whenever an attack is cancelled for any reason OTHER than " +
             "Stun (e.g. a perfect block, or the player stepping out of breakComboRange) - " +
             "same forced-cut idea as stunAnimationName above, just for every other exit.")]
    [SerializeField] private string idleAnimationName = "Idle";

    [Header("Range")]
    [Tooltip("Distance the AI uses to decide 'am I close enough to attack/stay attacking' - " +
             "used for starting an attack, breaking a combo if the player backs away, and " +
             "staying engaged. This is a DECISION distance only - it has no connection to " +
             "the attack hitbox's actual physical reach (that's purely determined by the " +
             "collider on the fist/foot and the animation itself). If attacks are whiffing " +
             "from a distance that looks 'in range', lower this to roughly match how far " +
             "the hitbox genuinely extends when the attack animation plays - test in Play " +
             "mode with Gizmos/the hitbox collider visible to find the right number.")]
    [SerializeField] private float attackRange = 2f;
    [Tooltip("Separate, LARGER distance used only to decide when to fully give up an attack " +
             "(StopCombo -> Idle) once already engaged. attackRange alone used to do both jobs " +
             "- deciding when a hit can actually land AND deciding when to bail - which meant " +
             "shrinking attackRange to stop whiffing also made the enemy bail out the instant " +
             "you stepped back even slightly. Now, between attackRange and breakComboRange, he " +
             "just won't start a fresh attack (and leans on MoveBetCombo's mid-combo step-closer " +
             "to close the gap) instead of abandoning the engagement outright. Keep this bigger " +
             "than attackRange - if it's equal or smaller it behaves exactly like before.")]
    [SerializeField] private float breakComboRange = 3f;

    [Header("Attack Telegraph")]
    [Tooltip("Shown for the whole windup, except during the flash window right around the " +
             "real parry instant below.")]
    [SerializeField] private AttackTelegraph telegraph;
    [SerializeField] private Color telegraphSafeColor = Color.blue;
    [Tooltip("The flash below blinks between this color and Perfect, rather than between " +
             "Safe and Perfect - gives the flash its own distinct look.")]
    [SerializeField] private Color telegraphWarningColor = Color.yellow;
    [Tooltip("Shown solid once the flash window ends, right as the hitbox actually goes live.")]
    [SerializeField] private Color telegraphPerfectColor = Color.red;
    [Tooltip("How many seconds BEFORE the exact moment a perfect block would need to be " +
             "pressed (read directly from the attack clip's own OpenEnemyHitWindow Animation " +
             "Event - no guessing) the flash starts. 'A few frames' at 60fps is roughly 0.03-0.08.")]
    [SerializeField] private float telegraphFlashLeadTime = 0.06f;
    [Tooltip("How many seconds AFTER that same instant the flash continues before settling " +
             "solid on Perfect color.")]
    [SerializeField] private float telegraphFlashTrailTime = 0.06f;
    [Tooltip("Blink speed (seconds per on/off step) for that flash.")]
    [SerializeField] private float telegraphFlashInterval = 0.04f;
    [Tooltip("ON = the telegraph appears ONLY for the perfect-block window: it shows up exactly Perfect Block Window " +
             "seconds before each hit lands (that is when pressing block counts as perfect), shrinking to the instant " +
             "of the hit, then goes solid Perfect color while the hit is live and disappears. Nothing is shown during " +
             "the rest of the wind-up. OFF = the old behaviour (blue for the whole wind-up + a flash).")]
    [SerializeField] private bool telegraphOnlyPerfectWindow = true;
    [Tooltip("Show it this many seconds earlier than the perfect window starts (reaction cushion). 0 = exactly the window.")]
    [SerializeField] private float telegraphWindowEarlyBy = 0.08f;
    private Coroutine flashRoutine;
    private void Awake()
    {
        enemyInitPos = transform.position;
        retreatTimer = ComputeNextRetreatCooldown();
        idlePauseTimer = ComputeNextIdlePauseCooldown();
    }

    /// <summary>Called by DmgDetection right after a perfect block lands on him (the non-stun
    /// branch - a stun already freezes him far longer than this). Freezes AI decision-making
    /// for parryRecoveryDuration so he doesn't immediately resume chasing as if nothing
    /// happened.</summary>
    /// <summary>Used by PlayerSwap: freezes his decisions/movement while he is being carried across.</summary>
    public void HoldForSwap(float seconds)
    {
        shouldApproach = false;
        shouldRetreat = false;
        isIdlePausing = false;
        attackNextOpportunity = false;
        moveVelX = 0f;
        currentMovementAnim = null;
        if (currentState != EnemyState.Stun)
        {
            StopCombo();                       // his attack is cancelled outright
            ChangeState(EnemyState.Swap);      // and he can't do anything until EndSwap
        }
    }

    /// <summary>Ends the swap hold: back to Idle, but doing nothing for 'recovery' more seconds.</summary>
    public void EndSwap(float recovery)
    {
        if (currentState != EnemyState.Swap) return;
        decisionLockTimer = Mathf.Max(decisionLockTimer, recovery);
        if (anim != null) anim.PlayBlend(idleAnimationName);
        ChangeState(EnemyState.Idle);
    }

    public void OnPerfectBlocked()
    {
        shouldApproach = false;
        shouldRetreat = false;
        isIdlePausing = false;
        decisionLockTimer = Mathf.Max(parryRecoveryDuration, counterWindowSeconds);
        parryRecoveryPending = true;

        counterWindowStart = Time.time;
        counterOpenUntil = Time.time + counterWindowSeconds;
        kickImmuneUntil = Time.time + counterWindowSeconds + Mathf.Max(0f, kickImmuneExtraSeconds);
        counterDodgeRolled = Random.value * 100f < punchDodgeChancePercent;
        if (debugAI) { }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StopAllCoroutines();
        ChangeState(EnemyState.Idle);
    }
    public void ChangeState(EnemyState state)
    {
        if (state != EnemyState.Attack) ReleaseHold();
        // Single chokepoint every transition goes through. Leaving Attack for ANY reason
        // must fully cancel whatever's actually running, not just relabel currentState - a
        // perfect block or a posture-stun calling ChangeState(Stun) while AttackRoutine is
        // still mid-coroutine would otherwise leave the combo running in the background
        // regardless of what currentState claims (exactly what caused "stunned during a
        // combo but it kept going").
        if (state != EnemyState.Attack && currentState == EnemyState.Attack && attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
            CloseEnemyHitWindow();
            isAttacking = false;

            // CloseEnemyHitWindow() above unconditionally re-shows the telegraph blue and
            // restarts flashRoutine (ScheduleParryFlash) - correct for "between hits,
            // more combo coming," wrong here, since this whole block only runs when the
            // attack is being cancelled outright (currently: ChangeState(Stun) called
            // directly, bypassing StopCombo - every other caller already goes through
            // StopCombo first, which nulls attackRoutine before ever reaching ChangeState).
            // Without cancelling it again right here, that freshly-restarted flashRoutine
            // keeps running in the background and fires telegraph.SetColor(...) later,
            // re-lighting the telegraph well after Hide() already turned it off below - this
            // is what was making the telegraph "randomly" pop back up after a stun.
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            // Stopping the coroutine only stops the C# side - the Animator has no idea and
            // just keeps playing the attack clip out to its natural end. Any Animation Events
            // still embedded in that clip (OpenEnemyHitWindow/CloseEnemyHitWindow) keep firing
            // right along with it, which is exactly what was re-lighting the telegraph after a
            // perfect block even though the combo was supposedly over - CloseEnemyHitWindow
            // calls telegraph.Show() on every hit gap, combo-stopped or not. Entering Stun
            // already force-cuts the Animator below with its own clip; every OTHER exit from
            // Attack (Idle via a perfect block/distance bail, Block via a reaction) needs the
            // same forced cut, just back to idleAnimationName instead.
            if (state != EnemyState.Stun && anim != null)
            {
                anim.PlayBlend(idleAnimationName);
            }
        }

        // MoveBetCombo's Lerp (fired via Animation Event mid-combo) wasn't tracked before -
        // if it's still running when an attack gets cancelled (e.g. a perfect block landing
        // mid-combo), it would keep silently pulling the enemy back toward the player every
        // frame for its remaining duration, overwriting any pushback (PushBackEnemy) that
        // just happened in the same moment.
        if (state != EnemyState.Attack && moveBetComboRoutine != null)
        {
            StopCoroutine(moveBetComboRoutine);
            moveBetComboRoutine = null;
        }

        if (state != EnemyState.Retreat && backDashRoutine != null)
        {
            StopCoroutine(backDashRoutine);
            backDashRoutine = null;
        }

        if (state != EnemyState.Stun && currentState == EnemyState.Stun && stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        // Entering Stun must force the Animator off whatever it's currently playing -
        // otherwise a combo animation that was mid-playback keeps running to its natural
        // end regardless of the coroutine/hit-window cancellation above, since Unity's
        // Animator doesn't know or care that the C# side gave up on it.
        if (state == EnemyState.Stun && currentState != EnemyState.Stun)
        {
            FightSounds.Stun();
            FightVFX.Play(FightVFX.Kind.Stun, transform.position, transform);
            StunHalo.Begin(false, stunDuration);
            if (anim != null)
            {
                { }
                BlendInterrupt(stunAnimationName);
            }
            else
            {
                Debug.LogWarning("[EnemyStateMachine] Anim is not assigned - can't force the stun animation.");
            }
        }

        if (state != EnemyState.Attack && telegraph != null)
            telegraph.Hide();

        if (currentState == EnemyState.Stun && state != EnemyState.Stun)
            StunHalo.Stop(false);   // no effect if the stars are already shattering

        currentState = state;
    }
    // Update is called once per frame
    void Update()
    {
        if (matchTimer.matchStopped) return;

        HandleFacing();
        decisionTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        blockTimer -= Time.deltaTime;
        if (decisionLockTimer > 0f) decisionLockTimer -= Time.deltaTime;
        retreatTimer -= Time.deltaTime; // back-dash cooldown
        if (parryRecoveryPending && decisionLockTimer <= 0f)
        {
            parryRecoveryPending = false;
            if (currentState == EnemyState.Idle && Random.value * 100f < dodgeAfterComboBreakPercent)
            {
                if (debugAI) { }
                StartBackDash();
            }
        }
        if (attackNextOpportunity)
        {
            attackNextTimer -= Time.deltaTime;
            if (attackNextTimer <= 0f) attackNextOpportunity = false;
        }
        if (currentState != EnemyState.Attack &&
            currentState != EnemyState.Block &&
            currentState != EnemyState.Retreat &&
            currentState != EnemyState.Hurt &&
            currentState != EnemyState.Swap &&
            currentState != EnemyState.Stun &&   // no decisions (and no back dash) while stunned;
                                                 // the only dodge out of a stun is StunRoutine's own roll
            decisionLockTimer <= 0f &&
            decisionTimer <= 0)
        {
            decisionRoll = Random.Range(0, 100);
            DistanceDecisionMaker();
            decisionTimer = DecisionInterval;
            if (debugAI)
            { }
        }
        // After the decision tick on purpose: if the tick starts a back dash, state is already
        // Retreat here, so HandleIdle can't attack on the same frame and pre-empt it.
        HandleStates();
        // Keep closing every frame until he's at his stopping distance (not just attack range -
        // stopping the moment he crossed attackRange is what made him take one step and stop).
        // Attacking or a back dash is what interrupts this, not the range line.
        if (currentState == EnemyState.Idle && decisionLockTimer <= 0f &&
            !isIdlePausing && !shouldRetreat &&
            Vector3.Distance(transform.position, Player.position) > approachStopDistance)
        {
            shouldApproach = true;
        }
        HandleMovement();
    }
    public void StopForRoundReset()
    {
        StopAllCoroutines();

        CloseEnemyHitWindow();

        // CloseEnemyHitWindow() just above starts a brand new flashRoutine (telegraph.Show +
        // ScheduleParryFlash) - StopAllCoroutines() ran BEFORE that, so it can't have
        // caught this one. Left running, it fires telegraph.SetColor(...) later, well after
        // this method's own Hide() via ChangeState(Idle) below already turned the telegraph
        // off - same "telegraph randomly pops back up" bug as ChangeState's direct-Stun path,
        // just via the round-reset path instead.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        attackRoutine = null;
        backDashRoutine = null;
        attackNextOpportunity = false;
        moveVelX = 0f;
        currentMovementAnim = null;
        isAttacking = false;
        isBlocking = false;

        shouldApproach = false;
        shouldRetreat = false;

        anim.PlayBlend("Idle");
        ChangeState(EnemyState.Idle);
    }
    private void HandleFacing()
    {
        if (opponent == null) return;
        if (currentState == EnemyState.Swap) return;   // the swap turns him itself

        Vector3 dir = opponent.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            facingRotationSpeed * Time.deltaTime
        );
    }
    public void HandleStates()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;
            case EnemyState.Approach:
                HandleApproach();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
            case EnemyState.Retreat:
                HandleRetreat();
                break;
            case EnemyState.Hurt:
                HandleHurt();
                break;
            case EnemyState.Block:
                HandleBlock();
                break;
            case EnemyState.Stun:
                HandleStun();
                break;
        }
    }
    private Coroutine stunRoutine;
    IEnumerator StunRoutine()
    {
        bool dodgeOut = Random.value * 100f < dodgeDuringStunPercent;
        float windowStart = stunDuration * (1f - stunDodgeWindowPercent / 100f);
        float dodgeAt = Random.Range(windowStart, stunDuration - 0.1f);
        float t = 0f;
        while (t < stunDuration)
        {
            t += Time.deltaTime;
            if (dodgeOut && t >= dodgeAt)
            {
                if (debugAI) { }
                StunHalo.Shatter(false);
                stunRoutine = null;
                // Otherwise HandleIdle would see posture still over the threshold and re-stun him.
                if (enemyPosture != null)
                    enemyPosture.postureSlider.value = postureAfterStun;
                StartBackDash();
                yield break;
            }
            yield return null;
        }
        stunRoutine = null;
        if (enemyPosture != null) enemyPosture.postureSlider.value = postureAfterStun;
        ChangeState(EnemyState.Idle);
        if (Random.value * 100f < dodgeAfterStunPercent)
        {
            if (debugAI) { }
            StartBackDash();
        }
    }
    private void HandleStun()
    {
        // HandleStun runs once per frame for the ENTIRE duration of being stunned (same as
        // every other HandleX method here), not just once on entry - without this guard, a
        // brand new 10-second StunRoutine would start every single frame, and every one of
        // those leftover coroutines would still fire its own ChangeState(Idle) call later,
        // potentially interrupting some completely unrelated future attack at a random time.
        if (stunRoutine == null)
            stunRoutine = StartCoroutine(StunRoutine());
    }
    #region ComboTriggers
    private bool hitWindowOpen;
    /// <summary>True while one of his hits is live (between DealDamage and CancelDamage).</summary>
    public bool HitWindowOpen => hitWindowOpen;

    /// <summary>True while he is attacking and the NEXT (or currently live) hit of his attack is a PUNCH.
    /// Read from the attack clip's own OpenEnemyHitWindow events, so it is correct before the hit goes live.
    /// Used by PlayerSwap: you can only swap against an incoming punch.</summary>
    public bool IncomingIsPunch() => IncomingIsPunch(out _);

    public bool IncomingIsPunch(out string info)
    {
        info = "";
        if (currentState != EnemyState.Attack) { info = $"state is {currentState}"; return false; }
        if (hitWindowOpen)
        {
            info = $"hit window is open, requiredTag={requiredTag}";
            return requiredTag == "ePunch";
        }
        if (anim == null) { info = "enemy Anim is not assigned"; return false; }

        AnimatorClipInfo[] infos = anim.GetCurrentAnimatorClipInfo(0);
        if (infos == null || infos.Length == 0 || infos[0].clip == null) { info = "no current clip on his animator"; return false; }
        AnimationClip clip = infos[0].clip;
        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
        float nt = st.loop ? Mathf.Repeat(st.normalizedTime, 1f) : Mathf.Clamp01(st.normalizedTime);
        float now = nt * clip.length;

        float bestTime = float.MaxValue;
        int bestType = -1;
        string all = "";
        foreach (AnimationEvent evt in clip.events)
        {
            all += $"{evt.functionName}@{evt.time:F2}(int {evt.intParameter}) ";
            // Your clips call the hit-open event "DealDamage" (and close it with "CancelDamage").
            if ((evt.functionName != "OpenEnemyHitWindow" && evt.functionName != "DealDamage") || evt.time <= now) continue;
            if (evt.time < bestTime) { bestTime = evt.time; bestType = evt.intParameter; }
        }
        info = $"clip '{clip.name}' at {now:F2}s of {clip.length:F2}s, next hit type={bestType}, events: {all}";
        return bestType == 0;
    }

    public void OpenEnemyHitWindow(int attackType)
    {
        hitWindowOpen = true;
        // The flash around this exact instant is scheduled ahead of time by
        // ScheduleParryFlash (started in AttackRoutine/CloseEnemyHitWindow) using the real
        // time read straight from the clip's own Animation Event - so there's nothing to
        // start here. This SetColor is just a safety net: if that schedule's prediction was
        // ever off (fallback case in GetNextOpenHitWindowTime), the hitbox going live for
        // real still forces solid red right now regardless.
        if (telegraph != null) telegraph.SetColor(telegraphPerfectColor);
        requiredTag = attackType == 0 ? "ePunch" : "eKick";
        FightSounds.EnemySwing(attackType != 0);
        foreach (var hitbox in hitBoxes)
        {
            bool isThisAttack = hitbox.CompareTag(requiredTag);
            hitbox.canDealDamage = isThisAttack;
            if (isThisAttack)
            {
                hitbox.ResetHit();
            }
            //hitbox.ResetHit();
            //hitbox.canDealDamage = true;
        }
    }
    public void CloseEnemyHitWindow()
    {
        hitWindowOpen = false;
        // Back to blue (full size again) rather than hiding - this fires between EVERY hit
        // in a combo, not just the last one, and there's no way to know from here whether
        // another hit is still coming. Reusing attackLength as the shrink duration here is
        // an approximation (a combo's individual sub-hits likely each have a shorter real
        // gap than the whole clip's length) - good enough for a consistent visual, but if a
        // later hit's shrink feels too slow/fast, this is the number to adjust. The true
        // hide only happens once AttackRoutine fully finishes.
        if (telegraph != null && telegraphOnlyPerfectWindow)
        {
            telegraph.Hide();              // between hits nothing is shown
            ScheduleNextPerfectWindow();   // ...until the next hit's perfect window
        }
        else if (telegraph != null)
        {
            telegraph.Show(telegraphSafeColor, attackLength);
            // Re-schedule the flash for the NEXT hit's OpenEnemyHitWindow too - without this,
            // only the first hit of a combo ever gets one. GetNextOpenHitWindowTime looks
            // for the next event AFTER the clip's current playback time, so this correctly
            // finds hit #2, #3, etc. rather than re-finding hit #1's already-passed event.
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(ScheduleParryFlash(GetNextOpenHitWindowTime(attackLength)));
        }
        foreach (var hitbox in hitBoxes)
            hitbox.canDealDamage = false;
    }
    private IEnumerator MoveTowardsPlayer()
    {
        float inRange = Vector3.Distance(Player.position, transform.position);

        if (inRange <= 1.5f)
            yield break;

        float direction = Mathf.Sign(Player.position.x - transform.position.x);

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos +
                            new Vector3(direction * moveSpeedInCombo, 0, 0);

        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);

            yield return null;
        }

        transform.position = targetPos;
    }
    private IEnumerator LungeToward(float distance)
    {
        // Close the gap to just inside Hit Range, never past it, and never further than the cap.
        float travel = Mathf.Min(distance - hitRange * 0.85f, maxLungeDistance);
        if (travel <= 0f) yield break;

        float direction = Mathf.Sign(Player.position.x - transform.position.x);
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + new Vector3(direction * travel, 0f, 0f);

        float elapsed = 0f;
        while (elapsed < lungeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(lungeDuration, 0.01f)));
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        transform.position = targetPos;
    }

    private Coroutine moveBetComboRoutine;
    public void MoveBetCombo()
    {
        if (moveBetComboRoutine != null) StopCoroutine(moveBetComboRoutine);
        moveBetComboRoutine = StartCoroutine(MoveTowardsPlayer());
    }
    #endregion
    private void HandleIdle()
    {
        // Counter window: he's open after a perfect block, so he does NOT react to a PUNCH. A KICK is different -
        // he always answers it (block or dodge), right as it starts, so it can never connect.
        bool playerAttackingNow = playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack;
        bool kickIncoming = playerAttackingNow && playerStateMachine.currentAttackType == PlayerStateMachine.AttackTypes.Kick;
        if (playerAttackingNow && (!InCounterWindow || (kickIncoming && alwaysDodgeKickInWindow)))
        {
            if (!hasBlockedCurrentPlayerAttack)
            {
                hasBlockedCurrentPlayerAttack = true;
                incomingAttackTypes = playerStateMachine.currentAttackType;
                shouldApproach = false;
                shouldRetreat = false;

                // A kick during the perfect-block aftermath: dodge it (back dash) or block it - his choice, never neither.
                if (kickIncoming && InCounterWindow && Random.value * 100f < kickDodgeChancePercent)
                {
                    StartBackDash();
                    if (backDashRoutine != null)
                    {
                        counterOpenUntil = 0f;
                        decisionLockTimer = 0f;
                        if (debugAI) { }
                        return;
                    }
                }
                if (debugAI && kickIncoming && InCounterWindow) { }
                ChangeState(EnemyState.Block);
            }
            return;
        }
        hasBlockedCurrentPlayerAttack = false;

        // Frozen in place after being perfect-blocked (see OnPerfectBlocked) - still allowed
        // to react to an incoming player attack above (that's a defensive reflex, not him
        // being pushy), just can't decide to re-attack or resume approach/retreat yet.
        if (decisionLockTimer > 0f) return;

        float distance = Vector3.Distance(transform.position, Player.position);
        if (distance < EngageRange)
        {
            bool forced = attackNextOpportunity;
            if (forced || (decisionRoll > 100f - Mathf.Min(100f, attackChancePercent * Aggro) && attackTimer <= 0))
            {
                attackNextOpportunity = false;
                shouldApproach = false;
                shouldRetreat = false;
                attackTimer = attackCooldown / Aggro;
                if (debugAI) { }
                ChangeState(EnemyState.Attack);
            }
            //else if (decisionRoll > 20)
            //{
            //    shouldApproach = false;
            //    shouldRetreat = false;
            //    ChangeState(EnemyState.Block);
            //}
            else
            {
                // Inside attack range but not attacking right now: keep walking in until he's at
                // his stopping distance, only then stand still.
                if (distance <= approachStopDistance) shouldApproach = false;
            }
        }
        if (enemyPosture.postureSlider.value >= 0.85)
        {
            ChangeState(EnemyState.Stun);
        }
        //if (InputHandler.instance.punch && blockTimer <= 0 && decisionRoll > 30)
        //{
        //    blockTimer = blockCooldown;
        //    ChangeState(EnemyState.Block);
        //}
    }
    private void HandleBlock()
    {
        if (!isBlocking)
        {
            StartCoroutine(BlockRoutine());
        }
    }

    private void HandleHurt()
    {

    }

    private void HandleRetreat()
    {

    }

    private void HandleAttack()
    {
        float distance = Vector3.Distance(transform.position, Player.position);

        if (distance > breakComboRange)
        {
            // StopCombo() already handles attackRoutine == null safely (it's just a no-op
            // stop in that case) and always closes the hit window + hides the telegraph, so
            // call it unconditionally here instead of the separate CloseEnemyHitWindow() +
            // isAttacking = false that used to follow it - that second CloseEnemyHitWindow()
            // call was redundant, and would have re-shown blue even when nothing was
            // actually attacking.
            //
            // Uses breakComboRange, NOT attackRange - a small step back shouldn't abandon the
            // whole engagement, only a retreat far enough that chasing no longer makes sense.
            StopCombo();
            ChangeState(EnemyState.Idle);
            return;
        }
        if (playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            StopCombo();
            incomingAttackTypes = playerStateMachine.currentAttackType;
            hasBlockedCurrentPlayerAttack = true;

            ChangeState(EnemyState.Block);
            return;
        }
        if (isAttacking || currentState == EnemyState.Stun)
        {
            return;
        }
        if (distance < EngageRange && !isAttacking)
        {
            attackRoutine = StartCoroutine(AttackRoutine());
        }
        //else if (distance > 2)
        //{
        //    ChangeState(EnemyState.Idle);
        //}
    }

    [Tooltip("When his attack LANDS on the player, his animation keeps playing this many seconds before it is cut to idle, instead of vanishing the same frame.")]
    [SerializeField] private float hitFollowThrough = 0.3f;
    public float HitFollowThrough => hitFollowThrough;
    private Coroutine releaseAnimRoutine;

    private IEnumerator ReleaseAnimAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        releaseAnimRoutine = null;
        if (anim != null && attackRoutine == null && currentState != EnemyState.Stun && currentState != EnemyState.Attack)
            anim.PlayBlend(idleAnimationName);
    }

    [Header("Attack anticipation hold")]
    [Tooltip("His animation freezes on the wind-up pose for this many seconds just before each hit lands, so the strike reads clearly. 0 = off.")]
    [SerializeField] private float anticipationHoldSeconds = 0.12f;
    [Tooltip("The freeze starts when the hit is this many seconds away in the clip (about 3 frames = 0.05).")]
    [SerializeField] private float anticipationHoldLead = 0.05f;
    [Tooltip("The freeze eases in and out over this many seconds each, instead of stopping dead - 0 = hard stop.")]
    [SerializeField] private float anticipationEaseSeconds = 0.14f;
    private Coroutine holdRoutine;
    private bool holding;
    private float holdPrevSpeed = 1f;
    private float extraHoldTime;

    private void ReleaseHold()
    {
        if (holdRoutine != null) { StopCoroutine(holdRoutine); holdRoutine = null; }
        if (holding)
        {
            holding = false;
            if (anim != null) anim.speed = holdPrevSpeed;
        }
    }

    private IEnumerator AnticipationHoldRoutine()
    {
        var done = new System.Collections.Generic.HashSet<int>();
        while (isAttacking && currentState == EnemyState.Attack && anim != null)
        {
            AnimatorClipInfo[] infos = anim.GetCurrentAnimatorClipInfo(0);
            if (infos != null && infos.Length > 0 && infos[0].clip != null)
            {
                AnimationClip clip = infos[0].clip;
                AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
                float now = Mathf.Clamp01(st.normalizedTime) * clip.length;
                foreach (AnimationEvent evt in clip.events)
                {
                    if (evt.functionName != "OpenEnemyHitWindow" && evt.functionName != "DealDamage") continue;
                    int key = Mathf.RoundToInt(evt.time * 1000f);
                    if (done.Contains(key) || evt.time <= now) continue;
                    if (evt.time - now > anticipationHoldLead + 0.5f * Mathf.Max(0f, anticipationEaseSeconds)) continue;

                    done.Add(key);
                    holdPrevSpeed = anim.speed;
                    holding = true;
                    float ease = Mathf.Max(0f, anticipationEaseSeconds);
                    float t = 0f;
                    while (t < ease)
                    {
                        t += Time.deltaTime;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / ease));
                        anim.speed = Mathf.Max(0.03f, holdPrevSpeed * (1f - k));
                        yield return null;
                    }
                    anim.speed = 0.03f;   // a slow creep rather than a dead stop
                    yield return new WaitForSeconds(anticipationHoldSeconds);
                    t = 0f;
                    while (t < ease)
                    {
                        t += Time.deltaTime;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / ease));
                        anim.speed = Mathf.Max(0.03f, holdPrevSpeed * k);
                        yield return null;
                    }
                    holding = false;
                    anim.speed = holdPrevSpeed;
                    extraHoldTime += anticipationHoldSeconds + ease;
                    break;
                }
            }
            yield return null;
        }
        holdRoutine = null;
    }

    public void StopCombo(float animHold = 0f)
    {
        ReleaseHold();
        bool wasRunning = attackRoutine != null;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        CloseEnemyHitWindow();
        // CloseEnemyHitWindow just scheduled a window for a hit that will never come - cancel it.
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        isAttacking = false;

        // Stopping the coroutine only stops the C# side - the Animator has no idea and just
        // keeps playing the attack clip out to its natural end, with any Animation Events
        // still embedded in it (OpenEnemyHitWindow/CloseEnemyHitWindow) still firing right
        // along with it. This is the actual fix for "combo keeps going after a perfect block"
        // - ChangeState()'s own forced-cut never ran here because by the time ChangeState(Idle)
        // gets called (every caller calls StopCombo() first), attackRoutine is already null,
        // so its "currentState == Attack && attackRoutine != null" guard was always false.
        // Forcing it here instead, right where the coroutine is actually being torn down, is
        // what actually stops the clip mid-playback. Guarded by wasRunning so a StopCombo()
        // called when nothing was attacking anyway doesn't yank the Animator for no reason.
        if (releaseAnimRoutine != null) { StopCoroutine(releaseAnimRoutine); releaseAnimRoutine = null; }
        if (wasRunning && anim != null)
        {
            if (animHold > 0f) releaseAnimRoutine = StartCoroutine(ReleaseAnimAfter(animHold));
            else anim.PlayBlend(idleAnimationName);
        }

        // Unlike CloseEnemyHitWindow's own blue re-show (which assumes the combo continues),
        // StopCombo specifically means the combo is being forcibly ended - no more hits are
        // coming, so force a real hide here rather than leaving it stuck on blue with nothing
        // left to ever turn it off (StopCoroutine above kills AttackRoutine immediately,
        // without running its own end-of-routine Hide() safety net).
        if (telegraph != null) telegraph.Hide();
    }

    IEnumerator AttackRoutine()
    {
        foreach (var hitbox in hitBoxes)
        {
            hitbox.canDealDamage = false;
        }
        isAttacking = true;
        string picked = PickCombo();
        string comboState = picked ?? "InPlaceCombo1";
        CurrentComboName = comboState;
        if (logComboChoice) { }
        anim.Play(comboState);
        yield return null; // one frame so GetCurrentAnimatorStateInfo reflects the new clip, not the previous one
        attackLength = anim.GetCurrentAnimatorStateInfo(0).length;

        // Inside attack range but too far for the hit to land: sometimes step in during the
        // wind-up. Tracked in moveBetComboRoutine, so leaving Attack (hit, block, stun) cancels it.
        float startDistance = Vector3.Distance(transform.position, Player.position);
        if (startDistance > hitRange && Random.value * 100f < lungeChancePercent)
        {
            if (moveBetComboRoutine != null) StopCoroutine(moveBetComboRoutine);
            moveBetComboRoutine = StartCoroutine(LungeToward(startDistance));
        }
        if (telegraph != null)
        {
            if (telegraphOnlyPerfectWindow) ScheduleNextPerfectWindow();
            else
            {
                telegraph.Show(telegraphSafeColor, attackLength);
                flashRoutine = StartCoroutine(ScheduleParryFlash(GetNextOpenHitWindowTime(attackLength)));
            }
        }

        extraHoldTime = 0f;
        if (anticipationHoldSeconds > 0f) holdRoutine = StartCoroutine(AnticipationHoldRoutine());
        float waited = 0f;
        while (waited < attackLength + extraHoldTime)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        ReleaseHold();
        isAttacking = false;
        attackRoutine = null;

        // Must actually StopCoroutine here, not just null the reference - if the last combo
        // hit's CloseEnemyHitWindow restarted ScheduleParryFlash and it's still mid-wait when
        // the clip ends, merely dropping the reference would let it keep running in the
        // background and re-light the telegraph AFTER the Hide() right below already turned
        // it off - the exact "stray coroutine outlives its purpose" bug pattern this file has
        // already been bitten by more than once.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (telegraph != null) telegraph.Hide(); // safety net in case CloseEnemyHitWindow's Animation Event never fired (e.g. attack interrupted)
        ChangeState(EnemyState.Idle);
    }

    /// <summary>
    /// Reads the actual scheduled time (seconds into the clip) of the NEXT upcoming
    /// OpenEnemyHitWindow Animation Event, i.e. the real moment a perfect block would need to
    /// land - AnimationClip.events is readable at runtime (not just in the Editor), so this is
    /// the real authored moment, not a guess. Looks only at events AFTER the clip's current
    /// playback position, so mid-combo (called again from CloseEnemyHitWindow for hit #2, #3,
    /// etc.) this correctly finds the NEXT hit's event rather than re-finding hit #1's, which
    /// has already passed by then. Falls back to the clip's full length if nothing qualifies,
    /// which just means the flash ends up scheduled late rather than firing too early.
    /// </summary>
    private float GetNextOpenHitWindowTime(float fallbackTime)
    {
        AnimatorClipInfo[] clipInfos = anim.GetCurrentAnimatorClipInfo(0);
        if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
        {
            AnimationClip clip = clipInfos[0].clip;
            float currentTime = anim.GetCurrentAnimatorStateInfo(0).normalizedTime * clip.length;
            float best = -1f;
            foreach (AnimationEvent evt in clip.events)
            {
                if ((evt.functionName != "OpenEnemyHitWindow" && evt.functionName != "DealDamage") || evt.time <= currentTime)
                    continue;
                if (best < 0f || evt.time < best)
                    best = evt.time;
            }
            if (best >= 0f) return best;
        }
        return fallbackTime;
    }

    // ---- perfect-window-only telegraph -------------------------------------------------
    /// <summary>Seconds from NOW until the next hit-open event in the playing clip (DealDamage / OpenEnemyHitWindow).</summary>
    private bool TryGetSecondsUntilNextHit(out float seconds)
    {
        seconds = 0f;
        if (anim == null) return false;
        AnimatorClipInfo[] infos = anim.GetCurrentAnimatorClipInfo(0);
        if (infos == null || infos.Length == 0 || infos[0].clip == null) return false;
        AnimationClip clip = infos[0].clip;
        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
        float now = (st.loop ? Mathf.Repeat(st.normalizedTime, 1f) : Mathf.Clamp01(st.normalizedTime)) * clip.length;
        float speed = Mathf.Max(0.01f, anim.speed * st.speedMultiplier);
        float best = -1f;
        foreach (AnimationEvent evt in clip.events)
        {
            if ((evt.functionName != "OpenEnemyHitWindow" && evt.functionName != "DealDamage") || evt.time <= now) continue;
            if (best < 0f || evt.time < best) best = evt.time;
        }
        if (best < 0f) return false;
        seconds = (best - now) / speed;
        return true;
    }

    [SerializeField] private bool logTelegraph = true;

    private void ScheduleNextPerfectWindow()
    {
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        if (telegraph == null) { if (logTelegraph) Debug.LogWarning("[Telegraph] No AttackTelegraph assigned on the enemy."); return; }
        if (TryGetSecondsUntilNextHit(out float secs))
        {
            if (logTelegraph) { }
            flashRoutine = StartCoroutine(PerfectWindowRoutine(secs));
        }
        else if (logTelegraph) { }
    }

    // Shows the telegraph for exactly the perfect-block window before the hit lands.
    private IEnumerator PerfectWindowRoutine(float secondsUntilHit)
    {
        float window = playerStateMachine != null ? playerStateMachine.PerfectBlockWindow : 0.15f;
        float showFor = window + Mathf.Max(0f, telegraphWindowEarlyBy);
        float wait = Mathf.Max(0f, secondsUntilHit - showFor);
        yield return new WaitForSeconds(wait);
        if (telegraph != null)
        {
            if (logTelegraph) { }
            telegraph.Show(telegraphWarningColor, Mathf.Min(showFor, secondsUntilHit));
        }
        // The hit opening (OpenEnemyHitWindow) turns it solid Perfect; CloseEnemyHitWindow hides it.
        flashRoutine = null;
    }

    /// <summary>
    /// Flashes the telegraph (Warning &lt;-&gt; Perfect color) for a short window centered on
    /// the exact real instant a perfect block needs to land - telegraphFlashLeadTime before it
    /// and telegraphFlashTrailTime after, both read from the Inspector as "a few frames" worth
    /// of seconds. No block-window math, no PlayerStateMachine coupling - purely built around
    /// the one moment we can read with certainty straight from the clip itself.
    /// </summary>
    private IEnumerator ScheduleParryFlash(float openTime)
    {
        float leadTime = Mathf.Max(0f, telegraphFlashLeadTime);
        float waitTime = Mathf.Max(0f, openTime - leadTime);
        yield return new WaitForSeconds(waitTime);

        float flashDuration = leadTime + Mathf.Max(0f, telegraphFlashTrailTime);
        yield return telegraph.FlashFor(telegraphWarningColor, flashDuration, telegraphFlashInterval);
        telegraph.SetColor(telegraphPerfectColor); // FlashFor restores whatever was showing before it started (Safe) - land solid Perfect once the flash window ends
        flashRoutine = null;
    }

    IEnumerator BlockRoutine()
    {
        isBlocking = true;
        if (incomingAttackTypes == PlayerStateMachine.AttackTypes.Punch)
        {
            BlendInterrupt("Middle_Block");
        }
        else if (incomingAttackTypes == PlayerStateMachine.AttackTypes.Kick)
        {
            yield return new WaitForSeconds(0.1f);
            BlendInterrupt("Lower_Block");
        }
        yield return new WaitUntil(() =>
            playerStateMachine.currentState != PlayerStateMachine.PlayerStates.Attack);
        isBlocking = false;
        attackTimer = 0f;
        ChangeState(EnemyState.Idle);
    }
    private void HandleApproach()
    {

    }

    /// <summary>
    /// Called once per decisionInterval while Idle. He always closes toward the player; the only
    /// "backing off" is an occasional discrete back dash (StartBackDash) when he's in range but
    /// on attack cooldown, plus the idle breathers.
    /// </summary>
    public void DistanceDecisionMaker()
    {
        float distance = Vector3.Distance(transform.position, Player.position);

        if (isIdlePausing)
        {
            idlePauseElapsed += DecisionInterval;
            if (idlePauseElapsed >= idlePauseDurationCurrent)
            {
                isIdlePausing = false;
                shouldApproach = true;
                idlePauseTimer = ComputeNextIdlePauseCooldown();
            }
            return;
        }

        // Inside attack range: HandleIdle attacks as soon as he's off cooldown. Until then he
        // keeps closing to his stopping distance, or occasionally back-dashes (one discrete
        // move, then Idle or a follow-up attack - never a continuous walk-away).
        if (distance < EngageRange)
        {
            shouldApproach = distance > approachStopDistance;
            if (retreatTimer <= 0f && Random.value * 100f < backDashChancePercent)
            {
                if (debugAI) { }
                StartBackDash();
            }
            return;
        }

        shouldApproach = true;

        idlePauseTimer -= DecisionInterval;

        if (enableIdleBreathers && idlePauseTimer <= 0f)
        {
            shouldApproach = false;
            isIdlePausing = true;
            idlePauseElapsed = 0f;
            idlePauseDurationCurrent = Random.Range(idlePauseDurationMin, idlePauseDurationMax);
        }
    }

    private void StartBackDash()
    {
        if (backDashRoutine != null) return;
        // Posture is over the stun line: HandleIdle is about to stun him, and a dash would
        // pre-empt that (stun -> dash -> stun again). Let the stun happen first. The dash out of a
        // stun lowers posture below this before calling, so it isn't affected.
        if (enemyPosture != null && enemyPosture.postureSlider.value >= 0.85f)
        {
            if (debugAI) { }
            return;
        }
        shouldApproach = false;
        shouldRetreat = false;
        retreatTimer = ComputeNextRetreatCooldown();
        ChangeState(EnemyState.Retreat);
        backDashRoutine = StartCoroutine(BackDashRoutine());
    }

    /// <summary>One committed backward dash straight away from the player (same idea as the
    /// player's dodge), then back to Idle - optionally primed to attack the moment he's back in
    /// range. Cancelled by ChangeState if he gets stunned/blocks/etc.</summary>
    private IEnumerator BackDashRoutine()
    {
        moveVelX = 0f;
        currentMovementAnim = null;
        float direction = -Mathf.Sign(Player.position.x - transform.position.x);

        if (anim != null)
        {
            bool hasDash = !string.IsNullOrEmpty(backDashAnim) &&
                           anim.HasState(0, Animator.StringToHash(backDashAnim));
            anim.PlayBlend(hasDash ? backDashAnim : walkBackwardAnim);
            if (!hasDash)
                Debug.LogWarning($"[EnemyStateMachine] No animator state \"{backDashAnim}\" - using \"{walkBackwardAnim}\" for the back dash.");
        }

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + new Vector3(direction * backDashDistance, 0f, 0f);
        float duration = Mathf.Max(backDashDuration, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (matchTimer != null && matchTimer.matchStopped)
            {
                // A point / pause landed mid-dash: abandon it rather than resuming later.
                backDashRoutine = null;
                if (anim != null) anim.PlayBlend(idleAnimationName);
                ChangeState(EnemyState.Idle);
                yield break;
            }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - (1f - t) * (1f - t); // ease-out: bursts away, then settles
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        transform.position = targetPos;

        backDashRoutine = null;
        if (anim != null) anim.PlayBlend(idleAnimationName);

        if (Random.value * 100f < attackAfterDashChancePercent)
        {
            attackNextOpportunity = true;
            attackNextTimer = 3f;   // expires if he can't get back in range
            decisionTimer = 0f;     // re-decide immediately
        }
        ChangeState(EnemyState.Idle);
    }

    /// <summary>Lower posture -> longer, more spread-out cooldown (rarer back dashes). Higher
    /// posture -> shorter cooldown (he backs off more often the closer he is to being
    /// stunned). The jitter keeps consecutive cooldowns from feeling mechanically identical.</summary>
    private float ComputeNextRetreatCooldown()
    {
        float postureT = enemyPosture != null ? Mathf.Clamp01(enemyPosture.postureSlider.value) : 0f;
        float baseCooldown = Mathf.Lerp(retreatCooldownAtZeroPosture, retreatCooldownAtMaxPosture, postureT);
        return Mathf.Max(0.1f, baseCooldown + Random.Range(-retreatCooldownJitter, retreatCooldownJitter));
    }

    private float ComputeNextIdlePauseCooldown()
    {
        return Random.Range(idlePauseCooldownMin, idlePauseCooldownMax);
    }
    // Tracks whichever movement clip is currently playing via HandleMovement, purely so
    // PlayMovementAnim below can tell "already playing this, leave it alone" apart from
    // "just started moving, actually trigger it" - see that method's comment for why this
    // matters.
    private string currentMovementAnim;
    private bool wasMovingLogged;

    public void HandleMovement()
    {
        if (currentState == EnemyState.Attack ||
        currentState == EnemyState.Block ||
        currentState == EnemyState.Hurt ||
        currentState == EnemyState.Retreat ||
        currentState == EnemyState.Swap ||
        currentState == EnemyState.Stun)
        {
            currentMovementAnim = null; // so the clip restarts cleanly next time he moves, instead of HandleMovement thinking it's still mid-loop from before
            if (debugAI && wasMovingLogged)
            { }
            wasMovingLogged = false;
            moveVelX = 0f;
            return;
        }

        float direction = Mathf.Sign(Player.position.x - transform.position.x);
        float distance = Vector3.Distance(transform.position, Player.position);

        // Desired signed velocity along X.
        float desiredVel = 0f;
        if (shouldApproach)
        {
            if (distance > approachStopDistance)
            {
                // Ease off as he nears his stopping distance instead of arriving at full speed.
                float arrive = Mathf.Clamp01((distance - approachStopDistance) / Mathf.Max(approachSlowRadius, 0.01f));
                float sprint = distance > chaseSprintDistance ? chaseSprintMultiplier : 1f;
                desiredVel = direction * moveSpeed * Aggro * sprint * Mathf.Max(arrive, 0.25f);
            }
        }

        float rate = Mathf.Abs(desiredVel) > Mathf.Abs(moveVelX) ? moveAcceleration : moveDeceleration;
        moveVelX = Mathf.MoveTowards(moveVelX, desiredVel, rate * Time.deltaTime);

        if (Mathf.Abs(moveVelX) > 0.05f)
        {
            if (debugAI && !wasMovingLogged)
            { }
            wasMovingLogged = true;
            transform.position += new Vector3(moveVelX * Time.deltaTime, 0f, 0f);
            bool towardPlayer = Mathf.Sign(moveVelX) == direction;
            string clip = towardPlayer ? walkForwardAnim : walkBackwardAnim;
            PlayMovementAnim(clip);
            // Only for clips NOT set to loop: they end after one cycle and he'd glide on the last
            // pose, so restart them. (Looping clips have normalizedTime > 1 forever - never restart those.)
            if (anim != null && !anim.IsInTransition(0))
            {
                AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
                if (st.IsName(clip) && !st.loop && st.normalizedTime >= 1f) anim.Play(clip, 0, 0f);
            }
        }
        else
        {
            if (debugAI && wasMovingLogged)
            { }
            wasMovingLogged = false;
            moveVelX = 0f;
            // Stopped after walking: go back to Idle (the walk clips loop, so without this he'd
            // keep walking in place). Only if a walk clip was actually playing.
            if (currentMovementAnim != null && anim != null) anim.Play(idleAnimationName);
            currentMovementAnim = null;
        }
    }

    /// <summary>
    /// Animator.Play(stateName) unconditionally restarts that state at normalized time 0 on
    /// EVERY call - it does NOT check whether that state is already the one playing. Calling
    /// it every single frame (the old HandleMovement did exactly that) meant the walk/dash
    /// clip was being reset back to its first frame every frame instead of ever being allowed
    /// to loop, which is what read as stutter/"playing every other frame" rather than a
    /// smooth walk cycle. This only calls Play() on the actual transition into a clip, so once
    /// it starts, the Animator's own loop setting on that clip is what carries it from there.
    /// </summary>
    private void PlayMovementAnim(string clipName)
    {
        if (currentMovementAnim == clipName) return;
        anim.Play(clipName);
        currentMovementAnim = clipName;
    }
}