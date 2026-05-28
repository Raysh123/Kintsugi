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
    public EnemyState currentState;
    public Transform Player;
    public bool shouldApproach;
    public bool shouldRetreat;
    [SerializeField]
    float moveSpeed = 10f;
    float decisionInterval = 2f;
    float decisionTimer;
    float minDist = 1.5f;
    float maxDist = 2f;
    public Animator anim;
    float decisionRoll;
    bool isBlocking;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ChangeState(EnemyState.Idle);
    }
    public void ChangeState(EnemyState state)
    {
        currentState = state;
    }
    // Update is called once per frame
    void Update()
    {
        decisionRoll = Random.Range(0, 100);            
        HandleStates();
        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0)
        {
            DistanceDecisionMaker();
            decisionTimer = decisionInterval;
        }
        HandleMovement();
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
        if (decisionRoll > 80)
        {
            anim.Play("Kizami_Tsuki");
        }
        else if(decisionRoll < 80 && distance > 2)
        {
            ChangeState(EnemyState.Idle);
        }
    }
    IEnumerator BlockRoutine()
    {
        isBlocking = true;
        anim.Play("Dodge_2");
        yield return new WaitForSeconds(0.5f);
        isBlocking = false;
        ChangeState(EnemyState.Idle);
    }
    private void HandleApproach()
    {

    }

    private void HandleIdle()
    {
        float distance = Vector3.Distance(transform.position, Player.position);
        if (distance < 2)
        {
            if (decisionRoll > 80)
            {
                ChangeState(EnemyState.Attack);
            }
        }
        if (InputHandler.instance.punch)
        {
            ChangeState(EnemyState.Block);
        }
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
            if(chance < 70)
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
