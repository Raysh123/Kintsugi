using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class EnemyStateMachine : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Approach,
        Attack,
        Block,
        Retreat,
        Hurt
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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ChangeState(EnemyState.Idle);
    }
    public void ChangeState(EnemyState state)
    {
        enemyInitPos = transform.position;
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
        }
    }
    private void HandleIdle()
    {
        if (playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            shouldApproach = false;
            shouldRetreat = false;
            ChangeState(EnemyState.Block);
            return;
        }
        float distance = Vector3.Distance(transform.position, Player.position);
        if (distance < 2)
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
        if (isAttacking)
        {
            return;
        }
        float distance = Vector3.Distance(transform.position, Player.position);
        if (distance < 2 && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }
        //else if (distance > 2)
        //{
        //    ChangeState(EnemyState.Idle);
        //}
    }
    IEnumerator AttackRoutine()
    {
        foreach(var hitbox in hitBoxes)
        {
            hitbox.canDealDamage = false;
        }
        float hitstart = 0.20f;
        float hitDuration = 0.12f;
        isAttacking = true;

        //Debug.Log("attack");
        //anim.Play("combo 1");
        //yield return null;
        //yield return new WaitForSeconds(anim.GetCurrentAnimatorStateInfo(0).length /*0.6f*/);
        anim.Play("Kizami_Tsuki");
        yield return null;
        //yield return new WaitForSeconds(anim.GetCurrentAnimatorStateInfo(0).length /*0.6f*/);
        float attackLength = anim.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(hitstart);
        foreach (var hitbox in hitBoxes)
        {
            hitbox.canDealDamage = true;
        }
        yield return new WaitForSeconds(hitDuration);
        foreach (var hitbox in hitBoxes)
        {
            hitbox.canDealDamage = false;
            hitbox.ResetHit();
        }
        yield return new WaitForSeconds(Mathf.Max(0f, attackLength - hitstart - hitDuration));
        //anim.Play("Kizami_Tsuki");
        //yield return null;
        //yield return new WaitForSeconds(anim.GetCurrentAnimatorStateInfo(0).length /*0.6f*/);
        isAttacking = false;
        ChangeState(EnemyState.Idle);
    }
    IEnumerator BlockRoutine()
    {
        isBlocking = true;
        if (InputHandler.instance.punch)
        {
            anim.Play("Middle_Block");
        }
        else if(InputHandler.instance.kick)
        {
            yield return new WaitForSeconds(0.2f);
            anim.Play("Lower_Block");
        }
        yield return new WaitForSeconds(0.5f);
        isBlocking = false;
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
        currentState == EnemyState.Hurt)
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
