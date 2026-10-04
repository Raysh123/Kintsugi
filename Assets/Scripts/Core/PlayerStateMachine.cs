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

    public enum AttackTypes
    {
        None,
        Punch,
        Kick
    };

    public AttackTypes currentAttackType {  get; private set; }
    public PlayerStates currentState;
    public static PlayerStateMachine instance;
    public PlayerController player;
    [SerializeField]
    public Animator animator;

    [SerializeField] 
    private MatchTimer matchTimer;
    [SerializeField] private float perfectBlockWindow = 0.15f;
    [SerializeField] private float normalBlockWindow = 0.50f;
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
        if(currentState == PlayerStates.Block)
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
        yield return new WaitForSeconds(duration);

        isAttacking = false;
        nextAttackTime = Time.time + attackRecovery;
        ChangeState(PlayerStates.Idle);
    }

    private void SetInjuryMeter()
    {
        if (InjuryMeter.instance == null)
        {
            return;
        }
        else if(!TryPerfectBlock())
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
