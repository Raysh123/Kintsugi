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
        Stun
    }
    public EnemyState currentState;
    public Transform Player;
    public bool shouldApproach;
    public bool shouldRetreat;
    [SerializeField]
    float moveSpeed = 10f;
    float decisionInterval = 0.5f;
    float decisionTimer;
    float minDist = 0.5f;
    float maxDist = 2f;

    [Header("Approach / Retreat AI")]
    [Tooltip("Only allow a retreat to start if the enemy is at least this close - no point " +
             "'retreating' when he isn't even engaged yet. Approach itself is unconditional " +
             "now (he always closes the distance rather than rolling dice on it), so this is " +
             "really the only approach-side range that still matters here.")]
    [SerializeField] private float retreatEngageDistance = 4f;
    [Tooltip("How long a single retreat burst lasts before he goes back to chasing, assuming " +
             "he hasn't already backed out past maxDist first.")]
    [SerializeField] private float retreatDuration = 0.6f;
    [Tooltip("Average seconds between retreats when posture is empty (0).")]
    [SerializeField] private float retreatCooldownAtZeroPosture = 6f;
    [Tooltip("Average seconds between retreats when posture is maxed (near stun) - lower than " +
             "the zero-posture cooldown so he backs off more often the more rattled he is.")]
    [SerializeField] private float retreatCooldownAtMaxPosture = 1.5f;
    [Tooltip("Random +/- applied around whichever average cooldown posture currently gives - " +
             "this is the 'randomly' part now that approach itself is no longer a coin flip.")]
    [SerializeField] private float retreatCooldownJitter = 1.5f;
    private float retreatTimer;
    private float retreatElapsed;

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
    public Animator anim;
    float decisionRoll;
    bool isBlocking;
    [SerializeField] private float facingRotationSpeed = 50f;
    public Transform opponent;
    private float attackTimer;
    private float blockTimer;
    private float attackCooldown = 3f;
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
    public void OnPerfectBlocked()
    {
        shouldApproach = false;
        shouldRetreat = false;
        isIdlePausing = false;
        decisionLockTimer = parryRecoveryDuration;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StopAllCoroutines();
        ChangeState(EnemyState.Idle);
    }
    public void ChangeState(EnemyState state)
    {
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
                anim.Play(idleAnimationName);
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
            if (anim != null)
            {
                Debug.Log($"[EnemyStateMachine] Forcing Animator to \"{stunAnimationName}\" (entering Stun from {currentState}).");
                anim.Play(stunAnimationName);
            }
            else
            {
                Debug.LogWarning("[EnemyStateMachine] Anim is not assigned - can't force the stun animation.");
            }
        }

        if (state != EnemyState.Attack && telegraph != null)
            telegraph.Hide();

        currentState = state;
    }
    // Update is called once per frame
    void Update()
    {
        if (matchTimer.matchStopped) return;

        HandleFacing();
        HandleStates();
        decisionTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        blockTimer -= Time.deltaTime;
        if (decisionLockTimer > 0f) decisionLockTimer -= Time.deltaTime;
        if (currentState != EnemyState.Attack &&
            currentState != EnemyState.Block &&
            currentState != EnemyState.Hurt &&
            decisionLockTimer <= 0f &&
            decisionTimer <= 0)
        {
            decisionRoll = Random.Range(0, 100);
            DistanceDecisionMaker();
            decisionTimer = decisionInterval;
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
        isAttacking = false;
        isBlocking = false;

        shouldApproach = false;
        shouldRetreat = false;

        anim.Play("Idle");
        ChangeState(EnemyState.Idle);
    }
    private void HandleFacing()
    {
        if (opponent == null) return;

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
        yield return new WaitForSeconds(10);
        stunRoutine = null;
        ChangeState(EnemyState.Idle);
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
    public void OpenEnemyHitWindow(int attackType)
    {
        // The flash around this exact instant is scheduled ahead of time by
        // ScheduleParryFlash (started in AttackRoutine/CloseEnemyHitWindow) using the real
        // time read straight from the clip's own Animation Event - so there's nothing to
        // start here. This SetColor is just a safety net: if that schedule's prediction was
        // ever off (fallback case in GetNextOpenHitWindowTime), the hitbox going live for
        // real still forces solid red right now regardless.
        if (telegraph != null) telegraph.SetColor(telegraphPerfectColor);
        requiredTag = attackType == 0 ? "ePunch" : "eKick";
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
        // Back to blue (full size again) rather than hiding - this fires between EVERY hit
        // in a combo, not just the last one, and there's no way to know from here whether
        // another hit is still coming. Reusing attackLength as the shrink duration here is
        // an approximation (a combo's individual sub-hits likely each have a shorter real
        // gap than the whole clip's length) - good enough for a consistent visual, but if a
        // later hit's shrink feels too slow/fast, this is the number to adjust. The true
        // hide only happens once AttackRoutine fully finishes.
        if (telegraph != null)
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
    private Coroutine moveBetComboRoutine;
    public void MoveBetCombo()
    {
        if (moveBetComboRoutine != null) StopCoroutine(moveBetComboRoutine);
        moveBetComboRoutine = StartCoroutine(MoveTowardsPlayer());
    }
    #endregion
    private void HandleIdle()
    {
        if (playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            if (!hasBlockedCurrentPlayerAttack)
            {
                hasBlockedCurrentPlayerAttack = true;
                incomingAttackTypes = playerStateMachine.currentAttackType;
                shouldApproach = false;
                shouldRetreat = false;
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
        if (distance < attackRange)
        {
            if (decisionRoll > 50 && attackTimer <= 0)
            {
                shouldApproach = false;
                shouldRetreat = false;
                attackTimer = attackCooldown;
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
                shouldRetreat = false;
                shouldApproach = false;
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
        if (distance < attackRange && !isAttacking)
        {
            attackRoutine = StartCoroutine(AttackRoutine());
        }
        //else if (distance > 2)
        //{
        //    ChangeState(EnemyState.Idle);
        //}
    }

    public void StopCombo()
    {
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
        if (wasRunning && anim != null)
        {
            anim.Play(idleAnimationName);
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
        anim.Play("InPlaceCombo1");
        yield return null; // one frame so GetCurrentAnimatorStateInfo reflects the new clip, not the previous one
        attackLength = anim.GetCurrentAnimatorStateInfo(0).length;
        if (telegraph != null)
        {
            telegraph.Show(telegraphSafeColor, attackLength);
            flashRoutine = StartCoroutine(ScheduleParryFlash(GetNextOpenHitWindowTime(attackLength)));
        }

        yield return new WaitForSeconds(attackLength);
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
                if (evt.functionName != "OpenEnemyHitWindow" || evt.time <= currentTime)
                    continue;
                if (best < 0f || evt.time < best)
                    best = evt.time;
            }
            if (best >= 0f) return best;
        }
        return fallbackTime;
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
            anim.Play("Middle_Block");
        }
        else if (incomingAttackTypes == PlayerStateMachine.AttackTypes.Kick)
        {
            yield return new WaitForSeconds(0.1f);
            anim.Play("Lower_Block");
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
    /// Called once per decisionInterval while not Attack/Block/Hurt. Approach is no longer a
    /// dice roll - he always closes the distance by default, which reads as purposeful chasing
    /// instead of randomly drifting back and forth. Retreating is the exception: an occasional,
    /// randomly-timed burst that interrupts the chase, gets more frequent the higher his
    /// posture climbs (ComputeNextRetreatCooldown), and ends on its own after retreatDuration
    /// or once he's backed out past maxDist, whichever comes first.
    /// </summary>
    public void DistanceDecisionMaker()
    {
        float distance = Vector3.Distance(transform.position, Player.position);

        if (isIdlePausing)
        {
            idlePauseElapsed += decisionInterval;
            if (idlePauseElapsed >= idlePauseDurationCurrent)
            {
                isIdlePausing = false;
                shouldApproach = true;
                idlePauseTimer = ComputeNextIdlePauseCooldown();
            }
            return;
        }

        if (shouldRetreat)
        {
            retreatElapsed += decisionInterval;
            if (retreatElapsed >= retreatDuration || distance >= maxDist)
            {
                shouldRetreat = false;
                shouldApproach = true;
                retreatTimer = ComputeNextRetreatCooldown();
            }
            return;
        }

        shouldApproach = true;

        retreatTimer -= decisionInterval;
        idlePauseTimer -= decisionInterval;

        if (retreatTimer <= 0f && distance <= retreatEngageDistance)
        {
            shouldApproach = false;
            shouldRetreat = true;
            retreatElapsed = 0f;
            return;
        }

        // Checked after retreat so the two bursts can't trigger on the same tick - retreat
        // wins if both happen to come due at once.
        if (idlePauseTimer <= 0f)
        {
            shouldApproach = false;
            isIdlePausing = true;
            idlePauseElapsed = 0f;
            idlePauseDurationCurrent = Random.Range(idlePauseDurationMin, idlePauseDurationMax);
        }
    }

    /// <summary>Lower posture -> longer, more spread-out cooldown (rarer retreats). Higher
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

    public void HandleMovement()
    {
        if (currentState == EnemyState.Attack ||
        currentState == EnemyState.Block ||
        currentState == EnemyState.Hurt ||
        currentState == EnemyState.Stun)
        {
            currentMovementAnim = null; // so the clip restarts cleanly next time he moves, instead of HandleMovement thinking it's still mid-loop from before
            return;
        }
        float direction = Mathf.Sign(Player.position.x - transform.position.x);
        float distance = Vector3.Distance(transform.position, Player.position);
        bool moved = false;
        if (shouldApproach)
        {
            if (distance > minDist)
            {
                Vector3 newPos = transform.position + new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
                PlayMovementAnim("Dash_Forward");
                transform.position = newPos;
                moved = true;
            }
        }
        else if (shouldRetreat)
        {
            if (distance < maxDist)
            {
                Vector3 newPos = transform.position - new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
                PlayMovementAnim("Dash_Backward");
                transform.position = newPos;
                moved = true;
            }
        }

        if (!moved) currentMovementAnim = null; // standing still (e.g. an idle breather) - next movement should trigger fresh, not be skipped as a false "already playing" match
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