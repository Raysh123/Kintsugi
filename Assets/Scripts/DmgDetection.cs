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
    [Header("Enemy hurt reactions (when the player's attack lands)")]
    [Tooltip("Animator state names on the enemy's animator. If a state doesn't exist, it falls " +
             "back to the other one (and logs a warning) instead of playing nothing.")]
    [SerializeField] private string enemyHurtOnPunchAnim = "Hurt_Face";
    [SerializeField] private string enemyHurtOnKickAnim = "Hurt_Balls";
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
        yield return new WaitForSeconds(playerStateMachine != null ? playerStateMachine.stunDuration : 2f);
        InputHandler.instance.SetInput(true);
    }
    void CheckHit(Collider other)
    {
        if (hitRegistered)
        {
            return;
        }
        // Nothing scores while the match is stopped (intro, or the pause after a point) -
        // otherwise a combo still in flight kept landing hits and adding points through the pause.
        if (matchTimer != null && matchTimer.matchStopped)
        {
            return;
        }
        if (canDealDamage && other.transform.tag == "Body" && gameObject.tag == "Punch" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
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
            if (enemyStateMachine != null)
            {
                // Same cut the perfect-block branch uses - ends the combo on any point. Only
                // leave the Attack state, so a stunned/blocking enemy isn't knocked out of it.
                enemyStateMachine.StopCombo();
                if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
                    enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Idle);
            }
            Debug.Log("Player hit");
            PlayEnemyHurt(enemyHurtOnPunchAnim, enemyHurtOnKickAnim);
            hitRegistered = true;
            if (matchScore != null) matchScore.AddPlayerPoint(PlayerMoveForScoring(PlayerStateMachine.AttackTypes.Punch));
            matchTimer.RegisterHit();
        }
        else if (canDealDamage && other.transform.tag == "Body" && gameObject.tag == "Kick" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
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
            if (enemyStateMachine != null)
            {
                // Same cut the perfect-block branch uses - ends the combo on any point. Only
                // leave the Attack state, so a stunned/blocking enemy isn't knocked out of it.
                enemyStateMachine.StopCombo();
                if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
                    enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Idle);
            }
            Debug.Log("Player hit");
            PlayEnemyHurt(enemyHurtOnKickAnim, enemyHurtOnPunchAnim);
            hitRegistered = true;
            if (matchScore != null) matchScore.AddPlayerPoint(PlayerMoveForScoring(PlayerStateMachine.AttackTypes.Kick));
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
                    // A perfect block always stops the enemy's current attack outright - on
                    // top of that, if posture is maxed, it stuns rather than just interrupting.
                    // ChangeState handles actually cancelling the attack coroutine itself.
                    if (enemyStateMachine != null)
                    {
                        if (enemyPosture.postureSlider.value >= postureMaxVal)
                        {
                            Debug.Log("Enemy stun");
                            enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Stun);
                        }
                        else
                        {
                            enemyStateMachine.StopCombo();
                            enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Idle);
                            // Brief freeze so he doesn't immediately resume chasing as if the
                            // block never happened - see OnPerfectBlocked's own comment.
                            enemyStateMachine.OnPerfectBlocked();
                        }
                    }
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
                if (playerStateMachine.IsNormalBlockActive())
                {
                    Debug.Log("normal block");
                    PushBackPlayer(enemyStateMachine.transform, knockbackDist);
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
            if (enemyStateMachine != null)
            {
                // Same cut the perfect-block branch uses - ends the combo on any point. Only
                // leave the Attack state, so a stunned/blocking enemy isn't knocked out of it.
                enemyStateMachine.StopCombo();
                if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
                    enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Idle);
            }
            Debug.Log("Enemy hit");
            if (playerStateMachine != null) playerStateMachine.PlayHurt(gameObject.tag == "ePunch");
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
                    enemyPosture.posDmg(0.5f);
                    // Same as the punch branch above - a perfect block always stops the
                    // enemy's current attack outright, and stuns instead if posture is maxed.
                    if (enemyStateMachine != null)
                    {
                        if (enemyPosture.postureSlider.value >= postureMaxVal)
                        {
                            Debug.Log("Enemy stun");
                            enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Stun);
                        }
                        else
                        {
                            enemyStateMachine.StopCombo();
                            enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Idle);
                            enemyStateMachine.OnPerfectBlocked();
                        }
                    }
                    hitRegistered = true;
                    canDealDamage = false;
                    return;
                }
                if (playerStateMachine.IsNormalBlockActive())
                {
                    Debug.Log("normal Block");
                    PushBackPlayer(enemyStateMachine.transform, knockbackDist);
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
            if (enemyStateMachine != null)
            {
                // Same cut the perfect-block branch uses - ends the combo on any point. Only
                // leave the Attack state, so a stunned/blocking enemy isn't knocked out of it.
                enemyStateMachine.StopCombo();
                if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
                    enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Idle);
            }
            Debug.Log("Enemy hit");
            if (playerStateMachine != null) playerStateMachine.PlayHurt(gameObject.tag == "ePunch");
            PushBackPlayer(enemyStateMachine.transform, knockbackDist);
            hitRegistered = true;
            if (matchScore != null) matchScore.AddEnemyPoint(PlayerStateMachine.AttackTypes.Kick);
            matchTimer.RegisterHit();
        }
    }

    private void PlayEnemyHurt(string preferred, string fallback)
    {
        if (anim == null) return;

        if (anim.HasState(0, Animator.StringToHash(preferred)))
        {
            anim.Play(preferred);
        }
        else
        {
            Debug.LogWarning($"[DmgDetection] Enemy animator has no state '{preferred}' - " +
                             $"playing '{fallback}' instead. Add the state, or change the name in the Inspector.");
            anim.Play(fallback);
        }
    }

    // Score by the move the player actually performed (set when they pressed punch/kick),
    // not just by which hitbox's tag happened to connect - falls back to the tag's type.
    private PlayerStateMachine.AttackTypes PlayerMoveForScoring(PlayerStateMachine.AttackTypes fromTag)
    {
        if (playerStateMachine != null &&
            playerStateMachine.currentAttackType != PlayerStateMachine.AttackTypes.None)
            return playerStateMachine.currentAttackType;
        return fromTag;
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