using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerStates
    {
        Idle,
        Move,
        Attack,
        Dodge,
        Block,
        Hurt,
        Stun,
        Injured
    };

    [Header("Stun")]
    [Tooltip("How long the player's input is locked when he's stunned (posture maxed while his " +
             "attack is blocked). Read by DmgDetection - this used to be hardcoded at 2 seconds " +
             "there, per hitbox.")]
    public float stunDuration = 2f;
    [Tooltip("Animator state played during a stun. If it doesn't exist, nothing plays (and a " +
             "warning is logged).")]
    [SerializeField] private string stunAnim = "Stun";
    [Range(0f, 1f)]
    [Tooltip("Posture value he is set to when the stun ends. 0 = empty, so the stun can't " +
             "immediately trigger again.")]
    [SerializeField] private float postureAfterStun = 0f;

    public bool IsStunned => currentState == PlayerStates.Stun;
    private Coroutine stunRoutine;

    

    public enum AttackTypes
    {
        None,
        Punch,
        Kick
    };

    

    /// <summary>The match is over (win / lose): same as EnterInjured - cancels hurt / attack / stun routines that
    /// would otherwise overwrite the final animation, then plays it and locks the pose.</summary>
    public void EnterFinalPose(string animName, float blendTime = 0f) => EnterInjured(animName, blendTime);

    public AttackTypes currentAttackType { get; private set; }
    public PlayerStates currentState;
    public static PlayerStateMachine instance;
    public PlayerController player;
    [SerializeField]
    public Animator animator;

    [SerializeField]
    private MatchTimer matchTimer;
    [SerializeField] private float perfectBlockWindow = 0.15f;
    [SerializeField] private float normalBlockWindow = 0.50f;

    [Header("Perfect block by DISTANCE")]
    [Tooltip("ON = a block is PERFECT when, at the moment you press it, the striking hand / foot is within 'Perfect Block Distance' " +
             "of your body (how far the attack still is from landing). OFF = the old rule: pressed within 'Perfect Block Window' " +
             "seconds before the hit. Distance doesn't care how fast the attack is, so a slow punch and a fast kick both feel fair.")]
    [SerializeField] private bool perfectByDistance = true;
    [Tooltip("World units between his striking hand / foot and your body (collider surface) at the moment you press block. " +
             "Bigger = more forgiving. Watch the [Block] line in the Console to tune it.")]
    [SerializeField] private float perfectBlockDistance = 0.9f;
    public bool PerfectByDistance => perfectByDistance;
    public float PerfectBlockDistance => perfectBlockDistance;

    private bool distancePerfect;
    private Collider[] bodyColliders;
    private DmgDetection[] enemyAttackBoxes;

    
    [Header("Player Hit Window (fraction of the attack animation's length)")]
    [Tooltip("Your hitboxes only count a hit between these two points of the animation, the same " +
             "way the enemy's do. Before the start there's wind-up and can't score, so pressing " +
             "attack right next to a stunned enemy no longer scores on the very first frame.")]
    [Range(0f, 1f)][SerializeField] private float punchHitStart = 0.3f;
    [Range(0f, 1f)][SerializeField] private float punchHitEnd = 0.65f;
    [Range(0f, 1f)][SerializeField] private float kickHitStart = 0.35f;
    [Range(0f, 1f)][SerializeField] private float kickHitEnd = 0.7f;

    [Header("Moving punch (punch while walking toward the opponent)")]
    [Tooltip("Press punch while walking FORWARD (toward the opponent) and this animator state plays instead " +
             "of the standing punch. Leave blank, or if the state doesn't exist, the normal punch is used " +
             "(still thrown while walking).")]
    [SerializeField] private string movingPunchAnim = "Moving_Punch";
    [Range(0f, 1f)]
    [Tooltip("How fast you keep walking DURING a moving punch / kick, as a fraction of normal walking speed. " +
             "This was the main reason it felt too fast: you were walking at full speed through the whole " +
             "animation. 0.3 = a gentle drift forward; 0 = no walking at all, only the lunge below.")]
    [SerializeField] private float movingAttackWalkSpeed = 0.3f;
    [Tooltip("Extra step forward during the punch, in units, on top of normal walking. 0 = just keep walking.")]
    [SerializeField] private float movingPunchLunge = 0.25f;
    [Tooltip("The lunge never carries you closer than this to the opponent.")]
    [SerializeField] private float movingPunchMinGap = 0.9f;
    [Range(0f, 1f)][SerializeField] private float movingPunchHitStart = 0.3f;
    [Range(0f, 1f)][SerializeField] private float movingPunchHitEnd = 0.65f;

    [Header("Moving kick (kick while walking toward the opponent)")]
    [Tooltip("Same idea as the moving punch: kick while pushing toward the opponent and this state plays " +
             "instead of the standing kick. Blank / missing state = the normal kick.")]
    [SerializeField] private string movingKickAnim = "Moving_Kick";
    [Tooltip("The forward (moving) kick's effect goes on the OTHER leg: the 2nd hitbox tagged Kick in P Hit Boxes. Swap their order in the list if it lands on the wrong leg.")]
    [SerializeField] private bool movingKickUsesOtherLimb = true;
    [SerializeField] private float movingKickLunge = 0.25f;
    [Range(0f, 1f)][SerializeField] private float movingKickHitStart = 0.35f;
    [Range(0f, 1f)][SerializeField] private float movingKickHitEnd = 0.7f;

    [Header("Hurt reactions (which animation plays depends on the incoming attack)")]
    [SerializeField] private string hurtOnPunchAnim = "Hurt_Face";
    [SerializeField] private string hurtOnKickAnim = "Hurt_Balls";
    [Tooltip("How long the player stays in the Hurt state after being hit.")]
    [SerializeField] private float hurtDuration = 0.5f;
    private Coroutine hurtRoutine;

    [Header("Leg hit (a small flinch, no point)")]
    [Tooltip("Animator state played when the enemy lands a leg hit on the player. Blank / missing state = nothing plays.")]
    [SerializeField] private string legHurtAnim = "Hurt_Leg";
    [Tooltip("How long (seconds) the flinch holds him before he can act again. Keep it short.")]
    [SerializeField] private float legHurtDuration = 0.3f;

    [Header("Action Limits")]
    [SerializeField] private float attackRecovery = 0.25f;
    [Tooltip("A punch / kick pressed up to this many seconds BEFORE he is able to attack (still in the previous " +
             "attack, a block, a hurt...) is remembered and fires the moment he can, instead of being dropped. " +
             "This is the main fix for punches that 'take a while'. 0 = off.")]
    [SerializeField] private float inputBufferTime = 0.25f;
    [Range(0.3f, 1f)]
    [Tooltip("How much of the attack's follow-through (the part after the hit window) he must wait out before " +
             "he can act again. 1 = the whole animation (old behaviour), 0.6 = he can punch again 40% sooner.")]
    [SerializeField] private float attackRecoverFraction = 0.7f;
    [Header("Action cancels (act from almost any state)")]
    [Tooltip("Block can interrupt an attack at ANY point, including the wind-up and the swing. Off = only once the hit window has closed (recovery).")]
    [SerializeField] private bool blockCancelsWindup = true;
    [Tooltip("A block press made up to this many seconds early is remembered and fires the moment a block is possible.")]
    [SerializeField] private float blockBufferTime = 0.2f;
    private float blockBufferedUntil = -1f;
    private Coroutine attackCo, blockCo, lungeCo;
    private bool attackRecoveryPhase;   // the hit window of the current attack has closed
    private float punchBufferedUntil = -1f;
    private float kickBufferedUntil = -1f;
    [SerializeField] private float blockCooldown = 0.6f;

    private float lastBlockPressTime = -999f;
    private float nextAttackTime;
    private float nextBlockTime;
    private bool blockWasHeld;
    private bool punchWasHeld;
    private bool kickWasHeld;
    public List<DmgDetection> pHitBoxes;
    private bool isAttacking = false;
    private bool perfectBlockUsed;

    public EnemyStateMachine enemy;
    [Header("Dodge")]
    [Tooltip("Pause (seconds) after a dodge finishes before he can dodge again. Counted from the " +
             "END of the dash (Dodge Time on PlayerController), so it's a true gap.")]
    [SerializeField] private float dodgeCooldown = 0.4f;
    private float nextDodgeTime;
    private int dodgeGrantedFrame = -1;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        ChangeState(PlayerStates.Idle);
    }
    private void Update()
    {
        if (matchTimer == null || matchTimer.matchStopped) return;

        bool punchPressed = InputHandler.instance.punch && !punchWasHeld;
        bool kickPressed = InputHandler.instance.kick && !kickWasHeld;
        bool blockPressed = InputHandler.instance.block && !blockWasHeld;

        punchWasHeld = InputHandler.instance.punch;
        kickWasHeld = InputHandler.instance.kick;
        blockWasHeld = InputHandler.instance.block;

        HandleStates();
        if (player.isHurt)
        { currentState = PlayerStates.Hurt; }

        // Remember a press for a moment, so one made slightly too early still fires.
        if (punchPressed) { punchBufferedUntil = Time.time + inputBufferTime; kickBufferedUntil = -1f; }
        if (kickPressed) { kickBufferedUntil = Time.time + inputBufferTime; punchBufferedUntil = -1f; }
        bool wantPunch = punchPressed || Time.time <= punchBufferedUntil;
        bool wantKick = kickPressed || Time.time <= kickBufferedUntil;

        // Actions cancel into each other: attack / block can start from Idle, Move, a Block, or an Attack
        // (attack-from-attack once its hit window has closed). Dodge, hurt and stun still lock you out.
        bool inFreeState = currentState == PlayerStates.Idle || currentState == PlayerStates.Move;
        bool canStartAttack = !PlayerSwap.Swapping && Time.time >= nextAttackTime &&
                              (inFreeState || currentState == PlayerStates.Block ||
                               (currentState == PlayerStates.Attack && attackRecoveryPhase));
        if (canStartAttack && (wantPunch || wantKick)) { punchBufferedUntil = kickBufferedUntil = -1f; }
        if ((wantPunch || wantKick) && canStartAttack)
        {
            CancelBlock();
            CancelAttack();
            foreach (var hitbox in pHitBoxes) if (hitbox != null) hitbox.ResetHit();
        }
        if (wantPunch && canStartAttack)
        {
            currentAttackType = AttackTypes.Punch;
            bool movingForward = IsMovingForward();
            string anim = "Kizami_Tsuki";
            if (movingForward && !string.IsNullOrEmpty(movingPunchAnim) &&
                animator.HasState(0, Animator.StringToHash(movingPunchAnim)))
                anim = movingPunchAnim;
            attackCo = StartCoroutine(PlayerAttackRoutine(anim, movingForward));
        }
        else if (wantKick && canStartAttack)
        {
            currentAttackType = AttackTypes.Kick;
            bool movingForward = IsMovingForward();
            string anim = "YokoGeri";
            if (movingForward && !string.IsNullOrEmpty(movingKickAnim) &&
                animator.HasState(0, Animator.StringToHash(movingKickAnim)))
                anim = movingKickAnim;
            attackCo = StartCoroutine(PlayerAttackRoutine(anim, movingForward));
        }

        if (blockPressed) blockBufferedUntil = Time.time + blockBufferTime;
        bool canBlock = !PlayerSwap.Swapping && Time.time >= nextBlockTime &&
                        (inFreeState || currentState == PlayerStates.Block ||
                         (currentState == PlayerStates.Attack && (blockCancelsWindup || attackRecoveryPhase)));
        if (canBlock && Time.time <= blockBufferedUntil)
        {
            blockBufferedUntil = -1f;
            StartBlock();
        }
    }
    #region Stun
    /// <summary>Stuns the player for Stun Duration: cancels whatever he was doing, plays the
    /// stun animation, locks input and shows the stars. Returns false if he's already stunned.
    /// Pass his Posture so it can be emptied when the stun ends.</summary>
    public bool EnterStun(Posture posture)
    {
        if (currentState == PlayerStates.Stun) return false;

        StopAllCoroutines();   // attack / block / hurt routines must not carry on during a stun
        isAttacking = false;
        SetPlayerHitWindow(false);
        stunRoutine = StartCoroutine(StunRoutine(posture));
        return true;
    }

    private IEnumerator StunRoutine(Posture posture)
    {
        ChangeState(PlayerStates.Stun);
        if (InputHandler.instance != null) InputHandler.instance.SetInput(false);
        StunHalo.Begin(true, stunDuration);

        if (animator != null)
        {
            if (animator.HasState(0, Animator.StringToHash(stunAnim))) animator.PlayBlend(stunAnim);
        }

        yield return new WaitForSeconds(stunDuration);
        EndStun(posture);
    }

    private void EndStun(Posture posture)
    {
        stunRoutine = null;
        if (posture != null) posture.postureSlider.value = postureAfterStun;
        if (InputHandler.instance != null) InputHandler.instance.SetInput(true);
        StunHalo.Stop(true);
        nextAttackTime = Time.time + attackRecovery;
        if (currentState == PlayerStates.Stun)
        {
            ChangeState(PlayerStates.Idle);
            if (animator != null) animator.PlayBlend("Idle");
        }
    }
    #endregion
    /// <summary>The injury meter filled: cancel whatever the player is doing (attack, block, dodge, hurt, stun) at
    /// once and play ONE animation. He stays locked in the Injured state - nothing else may change his pose.
    /// blendTime 0 = snap straight into the animation; &gt;0 = cross-fade from the current pose.</summary>
    public void EnterInjured(string animName, float blendTime)
    {
        bool wasStunned = currentState == PlayerStates.Stun;
        StopAllCoroutines();   // attack / block / hurt / stun / lunge routines must not carry on
        stunRoutine = null;
        hurtRoutine = null;
        isAttacking = false;
        SetPlayerHitWindow(false);
        if (wasStunned) StunHalo.Stop(true);
        ChangeState(PlayerStates.Injured);   // also resets the walking speed multiplier
        if (player != null) { player.isHurt = false; player.hurtFace = false; player.hurtBody = false; }
        if (InputHandler.instance != null) InputHandler.instance.SetInput(false);

        if (animator == null || string.IsNullOrEmpty(animName)) return;
        int hash = Animator.StringToHash(animName);
        if (!animator.HasState(0, hash))
        {
            return;
        }
        if (blendTime > 0f) animator.CrossFadeInFixedTime(hash, blendTime, 0, 0f);
        else animator.Play(hash, 0, 0f);
    }
    /// <summary>Stops the running attack (hit window shut, walking speed restored) without changing state.</summary>
    private void CancelAttack()
    {
        if (attackCo != null) { StopCoroutine(attackCo); attackCo = null; }
        if (lungeCo != null) { StopCoroutine(lungeCo); lungeCo = null; }
        isAttacking = false;
        attackRecoveryPhase = false;
        SetPlayerHitWindow(false);
        if (player != null) player.moveSpeedMultiplier = 1f;
    }
    #region Block
    /// <summary>World-unit gap between the enemy's striking limb (the hitbox of the strike that is coming - hands for a punch,
    /// feet for a kick) and the player's body. -1 = nothing is incoming (he is not attacking / no hitbox found).</summary>
    public float IncomingLimbGap()
    {
        if (enemy == null || enemy.currentState != EnemyStateMachine.EnemyState.Attack) return -1f;

        if (enemyAttackBoxes == null || enemyAttackBoxes.Length == 0)
        {
            var all = enemy.GetComponentsInChildren<DmgDetection>(true);
            var keep = new List<DmgDetection>();
            foreach (var d in all) if (d.CompareTag("ePunch") || d.CompareTag("eKick")) keep.Add(d);
            enemyAttackBoxes = keep.ToArray();
        }
        if (bodyColliders == null || bodyColliders.Length == 0)
        {
            Transform root = player != null ? player.transform : transform;
            var all = root.GetComponentsInChildren<Collider>(true);
            var keep = new List<Collider>();
            foreach (var c in all) if (c.CompareTag("pBody")) keep.Add(c);
            bodyColliders = keep.ToArray();
        }
        if (enemyAttackBoxes.Length == 0) return -1f;

        string tag = enemy.HitWindowOpen ? enemy.requiredTag : (enemy.IncomingIsPunch() ? "ePunch" : "eKick");

        float best = float.MaxValue;
        foreach (var box in enemyAttackBoxes)
        {
            if (box == null || !box.CompareTag(tag)) continue;
            Vector3 p = box.transform.position;
            float gap;
            if (bodyColliders.Length > 0)
            {
                gap = float.MaxValue;
                foreach (var bc in bodyColliders)
                {
                    if (bc == null || !bc.enabled) continue;
                    gap = Mathf.Min(gap, Vector3.Distance(p, bc.ClosestPoint(p)));
                }
                if (gap == float.MaxValue) gap = Mathf.Abs(p.x - (player != null ? player.transform.position.x : transform.position.x));
            }
            else gap = Mathf.Abs(p.x - (player != null ? player.transform.position.x : transform.position.x));
            best = Mathf.Min(best, gap);
        }
        return best == float.MaxValue ? -1f : best;
    }
    /// <summary>Ends a block early (an attack or a new block takes over). Its protection ends with it.</summary>
    private void CancelBlock()
    {
        if (blockCo != null) { StopCoroutine(blockCo); blockCo = null; }
        if (currentState == PlayerStates.Block) lastBlockPressTime = -999f;
    }

    /// <summary>Starts a block right now (perfect-block timing counts from this moment). Also used by
    /// the swap, so a swap is a block too.</summary>
    public void StartBlock()
    {
        if (currentState == PlayerStates.Attack) CancelAttack();
        if (blockCo != null) { StopCoroutine(blockCo); blockCo = null; }
        nextBlockTime = Time.time + blockCooldown;
        lastBlockPressTime = Time.time;
        perfectBlockUsed = false;
        if (perfectByDistance)
        {
            float gap = IncomingLimbGap();
            distancePerfect = gap >= 0f && gap <= perfectBlockDistance;
        }
        if (InjuryMeter.instance != null) InjuryMeter.instance.AddBlockInjury();

        if (enemy != null && enemy.requiredTag == "ePunch")
        {
            animator.PlayBlend("Upper_Block");
        }
        else
        {
            animator.PlayBlend("Lower_Block");
        }

        blockCo = StartCoroutine(BlockRoutine());
    }
    IEnumerator BlockRoutine()
    {
        ChangeState(PlayerStates.Block);
        yield return new WaitForSeconds(normalBlockWindow);
        blockCo = null;
        if (currentState == PlayerStates.Block)
        {
            ChangeState(PlayerStates.Idle);
        }
    }
    #endregion
    public float PerfectBlockWindow => perfectBlockWindow;

    

    /// <summary>Single gate for every dodge trigger (this script's state change AND
    /// PlayerController's dash both ask here). Returns true if a dodge may start now; also
    /// charges the injury meter once. Both callers in the same frame get the same answer, so
    /// the animation and the dash can't get out of step.</summary>
    public bool TryConsumeDodge()
    {
        if (dodgeGrantedFrame == Time.frameCount) return true;
        // The swap (block + dodge together) may hold this dodge back for a moment, or take it over.
        if (swap != null && swap.InterceptDodgeRequest()) return false;
        return GrantDodge();
    }

    private bool GrantDodge()
    {
        if (dodgeGrantedFrame == Time.frameCount) return true;
        if (Time.time < nextDodgeTime) return false;

        float dashTime = player != null ? player.dodgeTime : 0f;
        nextDodgeTime = Time.time + dashTime + dodgeCooldown;
        dodgeGrantedFrame = Time.frameCount;
        if (InjuryMeter.instance != null) InjuryMeter.instance.AddDodgeInjury();
        return true;
    }
    /// <summary>For PlayerSwap: charges the dodge (cooldown + injury meter) for a swap.</summary>
    public bool ConsumeDodgeForSwap() => GrantDodge();

    /// <summary>For PlayerSwap: the leeway ran out with no block, so do the normal dodge now.</summary>
    public void BeginDodgeNow()
    {
        if (!GrantDodge()) return;
        if (player != null) player.StartDodgeDash();
        if (currentState == PlayerStates.Idle || currentState == PlayerStates.Move)
        {
            ChangeState(PlayerStates.Dodge);
            animator.PlayBlend("Dodge_2");
        }
    }

    private PlayerSwap swap;
    public void RegisterSwap(PlayerSwap s) { swap = s; }
    public float LastBlockPressTime => lastBlockPressTime;

    public bool TryPerfectBlock()
    {
        bool insidePerfectWindow = perfectByDistance
            ? (distancePerfect && Time.time - lastBlockPressTime <= normalBlockWindow)
            : Time.time - lastBlockPressTime <= perfectBlockWindow;
        if (!insidePerfectWindow || perfectBlockUsed)
            return false;

        perfectBlockUsed = true;
        // A perfect block costs nothing - refund what pressing block just added.
        if (InjuryMeter.instance != null) InjuryMeter.instance.RefundLastBlock();
        return true;
    }
    public bool IsNormalBlockActive()
    {
        return Time.time - lastBlockPressTime <= normalBlockWindow;
    }
    public void ChangeState(PlayerStates newState)
    {
        if (newState == PlayerStates.Attack &&
       currentState != PlayerStates.Attack)
        {
            foreach (var hitbox in pHitBoxes)
            {
                hitbox.ResetHit();
            }
        }
        currentState = newState;
        if (newState != PlayerStates.Attack && player != null) player.moveSpeedMultiplier = 1f;
    }

    private void HandleStates()
    {
        switch (currentState)
        {
            case PlayerStates.Idle:
                HandleIdle();
                break;
            case PlayerStates.Move:
                HandleMove();
                break;
            case PlayerStates.Attack:
                HandleAttacks();
                break;
            case PlayerStates.Dodge:
                HandleDodge();
                break;
            case PlayerStates.Block:
                HandleBlock();
                break;
            case PlayerStates.Hurt:
                HandleHurt();
                break;
        }
    }

    private void HandleHurt()
    {
        if (player.hurtFace)
        {
            StartCoroutine(FaceHurt());
        }
        else if (player.hurtBody)
        {
            StartCoroutine(BodyHurt());
        }
    }
    IEnumerator FaceHurt()
    {
        animator.PlayBlend("Hurt_Face");
        player.hurtFace = false;
        yield return new WaitForSeconds(.5f);
        player.isHurt = false;
        ChangeState(PlayerStates.Idle);
    }
    IEnumerator BodyHurt()
    {
        animator.PlayBlend("Hurt_Balls");
        player.hurtBody = false;
        yield return new WaitForSeconds(.5f);
        player.isHurt = false;
        ChangeState(PlayerStates.Idle);
    }

    private void HandleBlock()
    {
        //if (!player.isHurt)
        //{
        //    if (InputHandler.instance.block == false)
        //    {
        //        ChangeState(PlayerStates.Idle);
        //    }
        //}
    }

    private void HandleDodge()
    {
        if (!player.isHurt)
        {
            if (InputHandler.instance.dodge == false)
            {
                ChangeState(PlayerStates.Idle);
            }
        }
    }

    private void HandleAttacks()
    {
        // Attacks begin only from a fresh press in Update.
    }

    /// <summary>True when the stick is pushed toward the opponent.</summary>
    private bool IsMovingForward()
    {
        if (player == null || player.opponent == null) return false;
        float toward = Mathf.Sign(player.opponent.position.x - player.transform.position.x);
        float stick = InputHandler.instance != null ? InputHandler.instance.move : player.move.x;
        return stick * toward > 0.01f;
    }

    IEnumerator PlayerAttackRoutine(string animName, bool moving = false)
    {
        isAttacking = true;
        attackRecoveryPhase = false;
        ChangeState(PlayerStates.Attack);
        SetInjuryMeter();
        animator.Play(animName);
        yield return null;

        float duration = animator.GetCurrentAnimatorStateInfo(0).length;

        bool isPunch = currentAttackType == AttackTypes.Punch;
        float start = Mathf.Clamp01(moving ? (isPunch ? movingPunchHitStart : movingKickHitStart) : isPunch ? punchHitStart : kickHitStart);
        float end = Mathf.Clamp(moving ? (isPunch ? movingPunchHitEnd : movingKickHitEnd) : isPunch ? punchHitEnd : kickHitEnd, start, 1f);

        if (moving && player != null) player.moveSpeedMultiplier = movingAttackWalkSpeed;

        // Moving attack: step forward over the wind-up, on top of (slowed) walking.
        float lunge = isPunch ? movingPunchLunge : movingKickLunge;
        if (moving && lunge > 0f && player != null && player.controller != null && player.opponent != null)
            lungeCo = StartCoroutine(MovingPunchLunge(duration * Mathf.Max(0.05f, start), lunge));

        SetPlayerHitWindow(false);
        yield return new WaitForSeconds(duration * start);
        SetPlayerHitWindow(true);
        FightSounds.PlayerSwing(currentAttackType == AttackTypes.Kick);
        {
            bool kick = currentAttackType == AttackTypes.Kick;
            Vector3 at = transform.position;
            Transform limb = transform;
            // the forward (moving) kick uses the other leg: the 2nd Kick-tagged hitbox in the list
            int skip = (kick && moving && movingKickUsesOtherLimb) ? 1 : 0;
            Transform first = null;
            foreach (var hb in pHitBoxes)
            {
                if (hb == null || !hb.CompareTag(kick ? "Kick" : "Punch")) continue;
                if (first == null) first = hb.transform;
                if (skip-- > 0) continue;
                limb = hb.transform; at = limb.position; break;
            }
            if (limb == transform && first != null) { limb = first; at = first.position; }
            FightVFX.Play(kick ? FightVFX.Kind.PlayerKick : FightVFX.Kind.PlayerPunch, at, limb);
        }
        yield return new WaitForSeconds(duration * (end - start));
        SetPlayerHitWindow(false);
        attackRecoveryPhase = true;   // from here an attack or block may cancel the follow-through
        yield return new WaitForSeconds(duration * (1f - end) * attackRecoverFraction);

        attackCo = null;
        attackRecoveryPhase = false;
        isAttacking = false;
        nextAttackTime = Time.time + attackRecovery;
        ChangeState(PlayerStates.Idle);
    }

    private IEnumerator MovingPunchLunge(float time, float distance)
    {
        float dir = Mathf.Sign(player.opponent.position.x - player.transform.position.x);
        float perSecond = distance / Mathf.Max(0.05f, time);
        float t = 0f;
        while (t < time && currentState == PlayerStates.Attack)
        {
            t += Time.deltaTime;
            float gap = Mathf.Abs(player.opponent.position.x - player.transform.position.x);
            if (gap <= movingPunchMinGap) break;
            player.controller.Move(new Vector3(dir * perSecond * Time.deltaTime, 0f, 0f));
            yield return null;
        }
    }

    /// <summary>Called by DmgDetection when an enemy attack lands unblocked. Plays the hurt
    /// reaction matching the incoming attack (punch = high/face, kick = low/body), the same way
    /// blocks pick Upper_Block / Lower_Block. Runs on its own coroutine, because Update() returns
    /// early while the match is stopped - which is exactly when a point has just landed.</summary>
    public void PlayHurt(bool incomingIsPunch)
    {
        // A hit that lands during a stun ends it cleanly (unlocks input, removes the stars).
        if (currentState == PlayerStates.Stun)
        {
            if (stunRoutine != null) StopCoroutine(stunRoutine);
            EndStun(null);
        }

        // Cancel whatever the player was doing (attack/block routines) so it can't overwrite this.
        StopAllCoroutines();
        isAttacking = false;
        punchBufferedUntil = kickBufferedUntil = -1f;   // a press made before the hit shouldn't fire right after it
        SetPlayerHitWindow(false);

        string preferred = incomingIsPunch ? hurtOnPunchAnim : hurtOnKickAnim;
        string other = incomingIsPunch ? hurtOnKickAnim : hurtOnPunchAnim;
        if (animator != null && !animator.HasState(0, Animator.StringToHash(preferred)))
        {
            preferred = other;
        }

        hurtRoutine = StartCoroutine(HurtRoutine(preferred));
    }

    /// <summary>A leg hit: plays a small flinch. Only while he is standing or walking - it never cancels an attack,
    /// block, dodge, stun or a real hurt, and a point or stun that follows simply takes over.</summary>
    public void PlayLegHurt()
    {
        if (animator == null || string.IsNullOrEmpty(legHurtAnim)) return;
        if (currentState != PlayerStates.Idle && currentState != PlayerStates.Move) return;
        if (PlayerSwap.Swapping) return;
        int hash = Animator.StringToHash(legHurtAnim);
        if (!animator.HasState(0, hash))
        {
            return;
        }
        if (legHurtRoutine != null) StopCoroutine(legHurtRoutine);
        legHurtRoutine = StartCoroutine(LegHurtRoutine(hash));
    }

    private Coroutine legHurtRoutine;
    private IEnumerator LegHurtRoutine(int hash)
    {
        ChangeState(PlayerStates.Hurt);
        animator.Play(hash, 0, 0f);
        FightSounds.PlayerLegHurt();
        yield return new WaitForSecondsRealtime(legHurtDuration);
        legHurtRoutine = null;
        if (currentState == PlayerStates.Hurt && !player.isHurt)
        {
            ChangeState(PlayerStates.Idle);
            animator.PlayBlend("Idle");
        }
    }

    private IEnumerator HurtRoutine(string animName)
    {
        ChangeState(PlayerStates.Hurt);
        if (animator != null) animator.PlayBlend(animName);

        yield return new WaitForSecondsRealtime(hurtDuration);

        hurtRoutine = null;
        if (currentState == PlayerStates.Hurt)
        {
            nextAttackTime = Time.time + attackRecovery;
            ChangeState(PlayerStates.Idle);
            if (animator != null) animator.PlayBlend("Idle");
        }
    }

    private void SetPlayerHitWindow(bool open)
    {
        foreach (var hitbox in pHitBoxes)
            if (hitbox != null) hitbox.canDealDamage = open;
    }

    private void SetInjuryMeter()
    {
        // Every attack fills the injury meter by an HP-dependent amount (see InjuryMeter).
        if (InjuryMeter.instance != null)
            InjuryMeter.instance.AddAttackInjury(currentAttackType == AttackTypes.Kick);
    }

    private void HandleMove()
    {
        if (!player.isHurt)
        {
            if (player.move.magnitude == 0)
            {
                ChangeState(PlayerStates.Idle);
                animator.PlayBlend("Idle");
            }
        }

    }

    private void HandleIdle()
    {
        if (!player.isHurt)
        {

            if (player.move.magnitude > 0)
            {
                ChangeState(PlayerStates.Move);
                if (player.facingRight)
                {
                    if (player.move.x > 0)
                    {
                        animator.PlayBlend("Walk_Forward");
                    }
                    else if (player.move.x < 0)
                    {
                        animator.PlayBlend("Walk_Backward");
                    }
                }
                else if (!player.facingRight)
                {
                    if (player.move.x > 0)
                    {
                        animator.PlayBlend("Walk_Backward");
                    }
                    else if (player.move.x < 0)
                    {
                        animator.PlayBlend("Walk_Forward");
                    }
                }

            }
            if (InputHandler.instance.dodge && TryConsumeDodge())
            {
                ChangeState(PlayerStates.Dodge);
                animator.PlayBlend("Dodge_2");
            }

        }

    }

}