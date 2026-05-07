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
    bool shouldApproach;
    bool shouldRetreat;
    [SerializeField]
    float moveSpeed = 10f;
    float decisionInterval = 2f;
    float decisionTimer;
    int chance;
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
        Debug.Log(Vector3.Distance(Player.position,transform.position));
        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0)
        {
            DecisionMaker();
            decisionTimer = decisionInterval;
        }
        HandleMovement();
    }
    //public void HandleStates()
    //{
    //    switch (currentState)
    //    {
    //        case EnemyState.Idle:
    //            if (Vector3.Distance(transform.position, Player.position) < 1.5)
    //            {

    //            }
    //    }
    //}
    public void DecisionMaker()
    {
        float distance = Vector3.Distance(transform.position,Player.position);
        chance = Random.Range(0, 100);
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
        else if(distance < 5)
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
        Vector3 direction = (Player.position - transform.position).normalized;

        if (shouldApproach)
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
        }
        else if (shouldRetreat)
        {
            transform.position -= direction * moveSpeed * Time.deltaTime;
        }
    }
}
