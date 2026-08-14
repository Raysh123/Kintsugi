using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DmgDetection : MonoBehaviour
{
    public Animator anim;
    [SerializeField] private MatchTimer matchTimer;
    [SerializeField] private PlayerStateMachine playerStateMachine;
    [SerializeField] private EnemyStateMachine enemyStateMachine;
    public bool canDealDamage;
    bool hitRegistered = false;
    bool isperfectBlock;
    [SerializeField] private float perfectBlockWindow;
    [SerializeField] private Posture playerPosture; 
    [SerializeField] private Posture enemyPosture; 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void ResetHit()
    {
        hitRegistered = false;
    }
    private void OnTriggerEnter(Collider other)
    {
        CheckHit(other);
    }
    private void OnTriggerStay(Collider other)
    {
        CheckHit(other);
    }
    void CheckHit(Collider other)
    {
        //Debug.Log(
        //    $"Attack={gameObject.tag}, Target={other.tag}, " +
        //    $"PlayerState={playerStateMachine.currentState}, " +
        //    $"AlreadyHit={hitRegistered}"
        //);
        if (hitRegistered)
        {
            return;
        }
        if (other.transform.tag == "Body" && gameObject.tag == "Punch" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Block)
            {
                Debug.Log("blocked by enemy");
                playerPosture.posDmg(0.5f);
                hitRegistered = true;
                return;
            }
            //ResetHit();
            Debug.Log("Player hit");
            anim.Play("Hurt_Balls");
            hitRegistered = true;
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        }
        else if (other.transform.tag == "Body" && gameObject.tag == "Kick" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Block)
            {
                Debug.Log("blocked by enemy");
                playerPosture.posDmg(0.5f);
                hitRegistered = true;
                return;
            }
            //ResetHit();
            Debug.Log("Player hit");
            anim.Play("Hurt_Balls");
            hitRegistered = true;
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        }
        else if (canDealDamage && other.transform.tag == "pBody" && gameObject.tag == "ePunch" && enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
        {
            //ResetHit();
            if (playerStateMachine != null)
            {
                if (playerStateMachine.TryPerfectBlock())
                {
                    Debug.Log("Blocked");
                    enemyPosture.posDmg(0.5f);
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
                if (playerStateMachine.IsNormalBlockActive())
                {
                    Debug.Log("normal block");
                    playerPosture.posDmg(0.5f);
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
            }
            Debug.Log("Enemy hit");
            hitRegistered = true;
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        }
        else if (canDealDamage && other.transform.tag == "pBody" && gameObject.tag == "ekick" && enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
        {
            //ResetHit();
            if (playerStateMachine != null)
            {
                if (playerStateMachine.TryPerfectBlock())
                {
                    Debug.Log("Blocked");
                    enemyPosture.posDmg(0.5f);
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
                if (playerStateMachine.IsNormalBlockActive())
                {
                    Debug.Log("normal Block");
                    playerPosture.posDmg(0.5f);
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
            }
            Debug.Log("Enemy hit");
            hitRegistered = true;
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        }
    }
    //IEnumerator BlockedState()
    //{
    //    isperfectBlock = true;
    //    yield return new WaitForSeconds(perfectBlockWindow);
    //    isperfectBlock = false;
    //}
}
