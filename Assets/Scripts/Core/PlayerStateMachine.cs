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
        Hurt
    };

    [Header("Stun")]
    [Tooltip("How long the player's input is locked when he's stunned (posture maxed while his " +
             "attack is blocked). Read by DmgDetection - this used to be hardcoded at 2 seconds " +
             "there, per hitbox.")]
    public float stunDuration = 2f;

    public enum AttackTypes
    {
        None,
        Punch,
        Kick
    };

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
    [Header("Player Hit Window (fraction of the attack animation's length)")]
    [Tooltip("Your hitboxes only count a hit between these two points of the animation, the same " +
             "way the enemy's do. Before the start there's wind-up and can't score, so pressing " +
             "attack right next to a stunned enemy no longer scores on the very first frame.")]
    [Range(0f, 1f)][SerializeField] private float punchHitStart = 0.3f;
    [Range(0f, 1f)][SerializeField] private float punchHitEnd = 0.65f;
    [Range(0f, 1f)][SerializeField] private float kickHitStart = 0.35f;
    [Range(0f, 1f)][SerializeField] private float kickHitEnd = 0.7f;

    [Header("Hurt reactions (which animation plays depends on the incoming attack)")]
    [SerializeField] private string hurtOnPunchAnim = "Hurt_Face";
    [SerializeField] private string hurtOnKickAnim = "Hurt_Balls";
    [Tooltip("How long the player stays in the Hurt state after being hit.")]
    [SerializeField] private float hurtDuration = 0.5f;
    private Coroutine hurtRoutine;

    [Header("Action Limits")]
    [SerializeField] private float attackRecovery = 0.25f;
    [SerializeField] private float blockCooldown = 0.6f;

    private float lastBlockPressTime = -999f;
    private float nextAttackTime;
    private float nextBlockTime;
    private bool blockWasHeld;
    private bool punchWasHeld;
    private bool kickWasHeld;
    public List<DmgDetection> pHitBoxes;
    private bool isAttacking;
    private bool perfectBlockUsed;

    public EnemyStateMachine enemy;
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
        if (matchTimer.matchStopped) return;

        bool punchPressed = InputHandler.instance.punch && !punchWasHeld;
        bool kickPressed = InputHandler.instance.kick && !kickWasHeld;
        bool blockPressed = InputHandler.instance.block && !blockWasHeld;

        punchWasHeld = InputHandler.instance.punch;
        kickWasHeld = InputHandler.instance.kick;
        blockWasHeld = InputHandler.instance.block;

        HandleStates();
        if (player.isHurt)
        { currentState = PlayerStates.Hurt; }

        if (punchPressed && Time.time >= nextAttackTime && currentState == PlayerStates.Idle)
        {
            currentAttackType = AttackTypes.Punch;
            StartCoroutine(PlayerAttackRoutine("Kizami_Tsuki"));
        }
        else if (kickPressed && Time.time >= nextAttackTime && currentState == PlayerStates.Idle)
        {
            currentAttackType = AttackTypes.Kick;
            StartCoroutine(PlayerAttackRoutine("YokoGeri"));
        }

        if (blockPressed &&
            Time.time >= nextBlockTime &&
            (currentState == PlayerStates.Idle || currentState == PlayerStates.Move))
        {
            nextBlockTime = Time.time + blockCooldown;
            lastBlockPressTime = Time.time;
            perfectBlockUsed = false;

            if (enemy != null && enemy.requiredTag == "ePunch")
            {
                animator.Play("Upper_Block");
            }
            else
            {
                animator.Play("Lower_Block");
            }

            StartCoroutine(BlockRoutine());
        }
    }
    IEnumerator BlockRoutine()
    {
        ChangeState(PlayerStates.Block);
        yield return new WaitForSeconds(normalBlockWindow);
        if (currentState == PlayerStates.Block)
        {
            ChangeState(PlayerStates.Idle);
        }
    }
    public bool TryPerfectBlock()
    {
        bool insidePerfectWindow = Time.time - lastBlockPressTime <= perfectBlockWindow;
        if (!insidePerfectWindow || perfectBlockUsed)
            return false;

        perfectBlockUsed = true;
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
        animator.Play("Hurt_Face");
        player.hurtFace = false;
        yield return new WaitForSeconds(.5f);
        player.isHurt = false;
        ChangeState(PlayerStates.Idle);
    }
    IEnumerator BodyHurt()
    {
        animator.Play("Hurt_Balls");
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

    IEnumerator PlayerAttackRoutine(string animName)
    {
        isAttacking = true;
        ChangeState(PlayerStates.Attack);
        SetInjuryMeter();
        animator.Play(animName);
        yield return null;

        float duration = animator.GetCurrentAnimatorStateInfo(0).length;

        bool isPunch = currentAttackType == AttackTypes.Punch;
        float start = Mathf.Clamp01(isPunch ? punchHitStart : kickHitStart);
        float end = Mathf.Clamp(isPunch ? punchHitEnd : kickHitEnd, start, 1f);

        SetPlayerHitWindow(false);
        yield return new WaitForSeconds(duration * start);
        SetPlayerHitWindow(true);
        yield return new WaitForSeconds(duration * (end - start));
        SetPlayerHitWindow(false);
        yield return new WaitForSeconds(duration * (1f - end));

        isAttacking = false;
        nextAttackTime = Time.time + attackRecovery;
        ChangeState(PlayerStates.Idle);
    }

    /// <summary>Called by DmgDetection when an enemy attack lands unblocked. Plays the hurt
    /// reaction matching the incoming attack (punch = high/face, kick = low/body), the same way
    /// blocks pick Upper_Block / Lower_Block. Runs on its own coroutine, because Update() returns
    /// early while the match is stopped - which is exactly when a point has just landed.</summary>
    public void PlayHurt(bool incomingIsPunch)
    {
        // Cancel whatever the player was doing (attack/block routines) so it can't overwrite this.
        StopAllCoroutines();
        isAttacking = false;
        SetPlayerHitWindow(false);

        string preferred = incomingIsPunch ? hurtOnPunchAnim : hurtOnKickAnim;
        string other = incomingIsPunch ? hurtOnKickAnim : hurtOnPunchAnim;
        if (animator != null && !animator.HasState(0, Animator.StringToHash(preferred)))
        {
            Debug.LogWarning($"[PlayerStateMachine] Player animator has no state '{preferred}' - playing '{other}' instead.");
            preferred = other;
        }

        hurtRoutine = StartCoroutine(HurtRoutine(preferred));
    }

    private IEnumerator HurtRoutine(string animName)
    {
        ChangeState(PlayerStates.Hurt);
        if (animator != null) animator.Play(animName);

        yield return new WaitForSecondsRealtime(hurtDuration);

        hurtRoutine = null;
        if (currentState == PlayerStates.Hurt)
        {
            nextAttackTime = Time.time + attackRecovery;
            ChangeState(PlayerStates.Idle);
            if (animator != null) animator.Play("Idle");
        }
    }

    private void SetPlayerHitWindow(bool open)
    {
        foreach (var hitbox in pHitBoxes)
            if (hitbox != null) hitbox.canDealDamage = open;
    }

    private void SetInjuryMeter()
    {
        if (InjuryMeter.instance == null)
        {
            return;
        }
        else if (!TryPerfectBlock())
        {
            InjuryMeter.instance.injuryMeterValue.value += 0.1f;
        }
    }

    private void HandleMove()
    {
        if (!player.isHurt)
        {
            if (player.move.magnitude == 0)
            {
                ChangeState(PlayerStates.Idle);
                animator.Play("Idle");
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
                        animator.Play("Walk_Forward");
                    }
                    else if (player.move.x < 0)
                    {
                        animator.Play("Walk_Backward");
                    }
                }
                else if (!player.facingRight)
                {
                    if (player.move.x > 0)
                    {
                        animator.Play("Walk_Backward");
                    }
                    else if (player.move.x < 0)
                    {
                        animator.Play("Walk_Forward");
                    }
                }

            }
            if (InputHandler.instance.dodge)
            {
                ChangeState(PlayerStates.Dodge);
                animator.Play("Dodge_2");
            }

        }

    }

}