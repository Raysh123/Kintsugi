using System.Collections;
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
    public enum DecisionStates
    {
        None,
        Attack,
        Block,
        Approach,
        Retreat
    }
    public EnemyState currentState;
    public DecisionStates currentDecisionState;
    public Transform player;
    //public bool shouldApproach;
    //public bool shouldRetreat;
    [SerializeField]
    float moveSpeed = 10f;
    float decisionInterval = 2f;
    float decisionTimer;
    float minDist = 1.5f;
    float maxDist = 2f;
    public Animator anim;
    //float decisionRoll;
    bool isBlocking;
    bool isCommitted;
    //bool canAttack = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ChangeState(EnemyState.Idle);
        decisionTimer = decisionInterval;
    }
    public void ChangeState(EnemyState state)
    {
        currentState = state;
    }
    // Update is called once per frame
    void Update()
    {
        HandleStates();
        Think();
        HandleMovement();
    }

    private void Think()
    {
        if (isCommitted)
        {
            return;
        }
        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0)
        {
            DistanceDecisionMaker();
            decisionTimer = decisionInterval;
        }
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

    private void HandleBlock()
    {
        if (!isBlocking)
        {
            if (InputHandler.instance.kick)
            {
                anim.Play("Lower_Block");
            }
            else if(InputHandler.instance.punch)
            {
                StartCoroutine(BlockRoutine());
            }
        }
    }

    private void HandleHurt()
    {
        
    }

    private void HandleRetreat()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance >= maxDist)
        {
            ChangeState(EnemyState.Idle);
        }
    }

    private void HandleAttack()
    {
        if (!isCommitted)
        {
            StartCoroutine(AttackRoutine());
        }
        //float distance = Vector3.Distance(transform.position, player.position);
        //if (decisionRoll > 80)
        //{
        //    anim.Play("Kizami_Tsuki");
        //}
        //else if(decisionRoll < 80 && distance > 2)
        //{
        //    ChangeState(EnemyState.Idle);
        //}
    }
    IEnumerator AttackRoutine()
    {
        isCommitted = true;
        ChangeStateInstant(EnemyState.Attack);
        anim.CrossFade("Kizami_Tsuki", 0.1f);
        yield return new WaitForSeconds(1f);
        //StartCoroutine(AttackCooldown());
        currentDecisionState = DecisionStates.None;
        isCommitted = false;
        ChangeState(EnemyState.Idle);
    }

    //IEnumerator AttackCooldown()
    //{
    //    canAttack = false;
    //    yield return new WaitForSeconds(1f);
    //    canAttack = true;
    //}

    private void ChangeStateInstant(EnemyState state)
    {
        currentState = state;
    }

    IEnumerator BlockRoutine()
    {
        isCommitted = true;
        isBlocking = true;
        anim.Play("Dodge_2");
        yield return new WaitForSeconds(0.5f);
        isBlocking = false;
        currentDecisionState = DecisionStates.None;
        isCommitted = false;
        ChangeState(EnemyState.Idle);
    }
    private void HandleApproach()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= minDist)
        {
            ChangeState(EnemyState.Idle);
        }
    }

    private void HandleIdle()
    {
        switch (currentDecisionState)
        {
            case DecisionStates.Approach:
                ChangeState(EnemyState.Approach);
                break;
            case DecisionStates.Attack:
                ChangeState(EnemyState.Attack);
                break;

            case DecisionStates.Block:
                ChangeState(EnemyState.Block);
                break;

            case DecisionStates.Retreat:
                ChangeState(EnemyState.Retreat);
                break;
        }
        //float distance = Vector3.Distance(transform.position, player.position);
        //if (distance < 2)
        //{
        //    if (decisionRoll > 80)
        //    {
        //        ChangeState(EnemyState.Attack);
        //    }
        //}
        //if (InputHandler.instance.punch)
        //{
        //    ChangeState(EnemyState.Block);
        //}
        //if (InputHandler.instance.kick)
        //{
        //    ChangeState(EnemyState.Block);
        //}
    }

    public void DistanceDecisionMaker()
    {
        float distance = Vector3.Distance(transform.position,player.position);
        float chance = Random.Range(0, 100);
        if (distance > 5)
        {
            if (chance < 80)
            {
                currentDecisionState = DecisionStates.Approach;
                //shouldApproach = true;
                //shouldRetreat = false;
            }
            else
            {
                currentDecisionState = DecisionStates.Retreat;
                //shouldApproach = false;
                //shouldRetreat = true;
            }
        }
        else if(distance < 5 && distance != 1)
        {
            if(chance < 70)
            {
                currentDecisionState = DecisionStates.Retreat;
                //shouldApproach = false;
                //shouldRetreat = true;
            }
            else
            {
                currentDecisionState = DecisionStates.Approach;
                //shouldRetreat = false;
                //shouldApproach = true;
            }
        }
    }
    public void HandleMovement()
    {
        if (isCommitted)
        {
            return;
        }
        float  direction = Mathf.Sign(player.position.x - transform.position.x);
        float distance = Vector3.Distance(transform.position, player.position);
        switch (currentState)
        {
            case EnemyState.Approach:

                if (distance > minDist)
                {
                    Vector3 newPos = transform.position + new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
                    transform.position = newPos;
                    anim.CrossFade("Dash_Forward", 0.1f);
                }

                break;

            case EnemyState.Retreat:

                if (distance < maxDist)
                {
                    Vector3 newPos = transform.position - new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
                    transform.position = newPos;
                    anim.CrossFade("Dash_Backward", 0.1f);
                }

                break;
        }
        //if (shouldApproach)
        //{
        //    if (distance > minDist)
        //    {
        //        Vector3 newPos = transform.position + new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
        //        anim.Play("Dash_Forward");
        //        transform.position = newPos;
        //    }
        //}
        //else if (shouldRetreat)
        //{
        //    if (distance < maxDist)
        //    {
        //        Vector3 newPos = transform.position - new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
        //        anim.Play("Dash_Backward");
        //        transform.position = newPos;
        //    }
        //}
    }
}
