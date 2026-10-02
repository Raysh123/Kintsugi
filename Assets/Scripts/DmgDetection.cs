using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
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
    [SerializeField] private float postureMaxVal = 0.8f;
    [SerializeField] private PlayerController player;
    [SerializeField] private float knockbackDist = 0.15f;
    [SerializeField] private MatchScore matchScore;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
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
    IEnumerator InputLockWindow()
    {
        yield return new WaitForSeconds(2);
        InputHandler.instance.SetInput(true);
    }
    void CheckHit(Collider other)
    {
        if (hitRegistered)
        {
            return;
        }
        if (other.transform.tag == "Body" && gameObject.tag == "Punch" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Block)
            {
                Debug.Log("blocked by enemy");
                PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                if (playerPosture.postureSlider.value >= postureMaxVal)
                {
                    InputHandler.instance.SetInput(false);
                    Debug.Log("stun");
                }
                StartCoroutine(InputLockWindow());
                playerPosture.posDmg(0.5f);
                hitRegistered = true;
                return;
            }
            Debug.Log("Player hit");
            anim.Play("Hurt_Balls");
            hitRegistered = true;
            if (matchScore != null) matchScore.AddPlayerPoint(PlayerStateMachine.AttackTypes.Punch);
            matchTimer.RegisterHit();
        }
        else if (other.transform.tag == "Body" && gameObject.tag == "Kick" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Block)
            {
                Debug.Log("blocked by enemy");
                PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                if (playerPosture.postureSlider.value >= postureMaxVal)
                {
                    InputHandler.instance.SetInput(false);
                    Debug.Log("stun");
                }
                StartCoroutine(InputLockWindow());
                playerPosture.posDmg(0.5f);
                hitRegistered = true;
                return;
            }
            Debug.Log("Player hit");
            anim.Play("Hurt_Balls");
            hitRegistered = true;
            if (matchScore != null) matchScore.AddPlayerPoint(PlayerStateMachine.AttackTypes.Kick);
            matchTimer.RegisterHit();
        }
        else if (canDealDamage && other.transform.tag == "pBody" && gameObject.tag == "ePunch" && enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
        {
            if (playerStateMachine != null)
            {
                if (playerStateMachine.TryPerfectBlock())
                {
                    Debug.Log("Blocked");
                    PushBackEnemy(player.transform, knockbackDist);
                    enemyPosture.posDmg(0.5f);
                    //Enemy stun is dealt in EnemyStateMachine
                    if (enemyPosture.postureSlider.value >= postureMaxVal)
                    {
                        Debug.Log("Enemy stun");
                    }
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
                if (playerStateMachine.IsNormalBlockActive())
                {
                    //Need to discuss on this block knock back
                    Debug.Log("normal block");
                    //PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                    if (playerPosture.postureSlider.value >= postureMaxVal)
                    {
                        InputHandler.instance.SetInput(false);
                        Debug.Log("stun");
                    }
                    StartCoroutine(InputLockWindow());
                    playerPosture.posDmg(0.5f);
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
            }
            Debug.Log("Enemy hit");
            hitRegistered = true;
            canDealDamage = false;
            if (matchScore != null) matchScore.AddEnemyPoint(PlayerStateMachine.AttackTypes.Punch);
            matchTimer.RegisterHit();
        }
        else if (canDealDamage && other.transform.tag == "pBody" && gameObject.tag == "eKick" && enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
        {
            if (playerStateMachine != null)
            {
                if (playerStateMachine.TryPerfectBlock())
                {
                    Debug.Log("Blocked");
                    PushBackEnemy(player.transform, knockbackDist);
                    //Enemy stun is dealt in EnemyStateMachine
                    if (enemyPosture.postureSlider.value >= postureMaxVal)
                    {
                        Debug.Log("Enemy stun");
                    }
                    enemyPosture.posDmg(0.5f);
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
                if (playerStateMachine.IsNormalBlockActive())
                {
                    //Need to discuss on this block knock back
                    Debug.Log("normal Block");
                    //PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                    if (playerPosture.postureSlider.value >= postureMaxVal)
                    {
                        InputHandler.instance.SetInput(false);
                        Debug.Log("stun");
                    }
                    StartCoroutine(InputLockWindow());
                    playerPosture.posDmg(0.5f);
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
            }
            Debug.Log("Enemy hit");
            PushBackPlayer(enemyStateMachine.transform, knockbackDist);
            hitRegistered = true;
            if (matchScore != null) matchScore.AddEnemyPoint(PlayerStateMachine.AttackTypes.Kick);
            matchTimer.RegisterHit();
        }
    }

    private void PushBackPlayer(Transform source, float distance)
    {
        Vector3 direction = player.transform.position - source.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }
        player.controller.Move(direction.normalized * distance);
    }
    void PushBackEnemy(Transform source, float distance)
    {
        Vector3 direction = enemyStateMachine.transform.position - source.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        enemyStateMachine.transform.position += direction.normalized * distance;
    }
}