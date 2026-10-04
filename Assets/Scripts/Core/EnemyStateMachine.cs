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
    private void Awake()
    {
        enemyInitPos = transform.position;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StopAllCoroutines();
        ChangeState(EnemyState.Idle);
    }
    public void ChangeState(EnemyState state)
    {
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
        if (currentState != EnemyState.Attack &&
            currentState != EnemyState.Block &&
            currentState != EnemyState.Hurt && 
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
    IEnumerator StunRoutine()
    {
        yield return new WaitForSeconds(10);
        ChangeState(EnemyState.Idle);
    }
    private void HandleStun()
    {
        StartCoroutine(StunRoutine());
    }
    #region ComboTriggers
    public void OpenEnemyHitWindow(int attackType)
    {
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
    public void MoveBetCombo()
    {
        StartCoroutine(MoveTowardsPlayer());
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
        float distance = Vector3.Distance(transform.position, Player.position);
        if (distance < 1)
        {
            if (decisionRoll > 50 && attackTimer <= 0)
            {
                shouldApproach = true;
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
                shouldRetreat = true;
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

        if (distance > 2f)
        {
            if (attackRoutine != null)
            {
                StopCombo();
            }

            CloseEnemyHitWindow();
            isAttacking = false;
            ChangeState(EnemyState.Idle);
            return;
        }
        if (playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            CloseEnemyHitWindow();
            isAttacking = false;
            incomingAttackTypes = playerStateMachine.currentAttackType;
            hasBlockedCurrentPlayerAttack = true;

            ChangeState(EnemyState.Block);
            return;
        }
        if (isAttacking || currentState == EnemyState.Stun)
        {
            return;
        }
        if (distance < 2 && !isAttacking)
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
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        CloseEnemyHitWindow();
        isAttacking = false;
    }

    IEnumerator AttackRoutine()
    {
        foreach(var hitbox in hitBoxes)
        {
            hitbox.canDealDamage = false;
        }
        isAttacking = true;
        anim.Play("InPlaceCombo1");
        yield return null;
        float attackLength = anim.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(attackLength);
        isAttacking = false;
        attackRoutine = null;
        ChangeState(EnemyState.Idle);
    }
    IEnumerator BlockRoutine()
    {
        isBlocking = true;
        if (incomingAttackTypes == PlayerStateMachine.AttackTypes.Punch)
        {
            anim.Play("Middle_Block");
        }
        else if(incomingAttackTypes == PlayerStateMachine.AttackTypes.Kick)
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

    public void DistanceDecisionMaker()
    {
        float distance = Vector3.Distance(transform.position,Player.position);
        float chance = Random.Range(0, 100);
        if (distance > 5)
        {
            if (chance < 80)
            {
                shouldApproach = true;
                shouldRetreat = false;
            }
            else
            {
                shouldApproach = false;
                shouldRetreat = true;
            }
        }
        else if(distance < 5 && distance != 1)
        {
            if(chance < 25)
            {
                shouldApproach = false;
                shouldRetreat = true;
            }
            else
            {
                shouldRetreat = false;
                shouldApproach = true;
            }
        }
    }
    public void HandleMovement()
    {
        if (currentState == EnemyState.Attack ||
        currentState == EnemyState.Block ||
        currentState == EnemyState.Hurt || 
        currentState == EnemyState.Stun)
            return;
        float  direction = Mathf.Sign(Player.position.x - transform.position.x);
        float distance = Vector3.Distance(transform.position, Player.position);
        if (shouldApproach)
        {
            if (distance > minDist)
            {
                Vector3 newPos = transform.position + new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
                anim.Play("Dash_Forward");
                transform.position = newPos;
            }
        }
        else if (shouldRetreat)
        {
            if (distance < maxDist)
            {
                Vector3 newPos = transform.position - new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
                anim.Play("Dash_Backward");
                transform.position = newPos;
            }
        }
    }
}
