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
    [Header("Leg hits (posture damage only, no points)")]
    [Tooltip("Tag on the PLAYER's leg collider(s). An enemy punch/kick that lands on it adds " +
             "posture damage to the player and scores NOTHING. Create this tag in Project " +
             "Settings > Tags and Layers.")]
    [SerializeField] private string playerLegTag = "Leg";
    [Tooltip("Tag on the ENEMY's leg collider(s). Leave EMPTY to switch enemy leg hits off " +
             "(not set up yet) - a player attack on the enemy then just hits whatever its " +
             "normal Body collider does.")]
    [SerializeField] private string enemyLegTag = "";
    [Tooltip("Posture damage dealt by a leg hit. (A normal block adds 0.5; a Posture max of 0.8 stuns.)")]
    [SerializeField] private float legHitPostureDamage = 0.3f;
    [Header("Hit knockback (so the models don't clip into each other)")]
    [Tooltip("How far the victim is pushed back when a hit LANDS (player hitting the enemy, or the enemy " +
             "hitting the player). Blocks keep using Knockback Dist above. 0 = off.")]
    [SerializeField] private float hitKnockbackDist = 0.6f;
    [Tooltip("The push is a quick slide over this many seconds instead of a teleport.")]
    [SerializeField] private float hitKnockbackTime = 0.15f;
    [Header("Debug")]
    [Tooltip("Logs every scoring hit with the states involved, and warns when both sides score within " +
             "a fraction of a second of each other.")]
    [SerializeField] private bool logHits = true;
    private static float lastPlayerScoreTime = -99f, lastEnemyScoreTime = -99f;

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
    /// <summary>Stuns the player (state, animation, input lock, stars). Plays the stun sound once
    /// per stun - hits that land while he's already stunned don't restart it.</summary>
    void StunPlayer()
    {
        if (playerStateMachine != null && playerStateMachine.EnterStun(playerPosture))
        {
            Debug.Log("Player stun");
            FightSounds.Stun();
        }
    }
    /// <summary>A hit on a leg collider: posture damage only - no points, no hit pause, no hurt
    /// reaction. Returns true if this collision was a leg hit (so the normal scoring is skipped).</summary>
    bool TryResolveLegHit(Collider other)
    {
        string hitTag = other.transform.tag;
        string myTag = gameObject.tag;

        bool playerAttacking = canDealDamage && (myTag == "Punch" || myTag == "Kick") &&
                               playerStateMachine != null &&
                               playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack;
        if (playerAttacking && !string.IsNullOrEmpty(enemyLegTag) && hitTag == enemyLegTag)
        {
            hitRegistered = true;
            Debug.Log($"Leg hit on enemy (+{legHitPostureDamage} posture, no points).");
            FightSounds.LegHit();
            if (enemyPosture != null) enemyPosture.posDmg(legHitPostureDamage);
            if (enemyStateMachine != null && enemyPosture != null &&
                enemyPosture.postureSlider.value >= postureMaxVal &&
                enemyStateMachine.currentState != EnemyStateMachine.EnemyState.Stun)
            {
                enemyStateMachine.StopCombo();
                enemyStateMachine.ChangeState(EnemyStateMachine.EnemyState.Stun);
            }
            return true;
        }

        bool enemyAttacking = canDealDamage && (myTag == "ePunch" || myTag == "eKick") &&
                              enemyStateMachine != null &&
                              enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack;
        if (enemyAttacking && !string.IsNullOrEmpty(playerLegTag) && hitTag == playerLegTag)
        {
            hitRegistered = true;
            Debug.Log($"Leg hit on player (+{legHitPostureDamage} posture, no points).");
            FightSounds.LegHit();
            if (playerPosture != null) playerPosture.posDmg(legHitPostureDamage);
            if (playerPosture != null && playerPosture.postureSlider.value >= postureMaxVal)
            {
                StunPlayer();
            }
            else if (playerStateMachine != null)
            {
                playerStateMachine.PlayLegHurt();   // a small flinch (the stun, if it just triggered, plays its own animation)
            }
            return true;
        }
        return false;
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
        // The swap is an evasion: while it plays, enemy hitboxes can't touch the player.
        if (PlayerSwap.Swapping && (gameObject.CompareTag("ePunch") || gameObject.CompareTag("eKick"))) return;
        if (TryResolveLegHit(other)) return;
        if (canDealDamage && other.transform.tag == "Body" && gameObject.tag == "Punch" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            if (enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Block)
            {
                Debug.Log("blocked by enemy");
                FightSounds.EnemyBlocked();
                PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                if (playerPosture.postureSlider.value >= postureMaxVal)
                {
                    StunPlayer();
                }
                playerPosture.posDmg(0.5f);
                hitRegistered = true;
                return;
            }
            if (enemyStateMachine != null && enemyStateMachine.TryCounterDodge(false))
            {
                hitRegistered = true;   // this swing is spent - he slipped it
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
            LogScore(true);
            FightSounds.EnemyHit();
            KnockbackEnemy();
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
                FightSounds.EnemyBlocked();
                PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                if (playerPosture.postureSlider.value >= postureMaxVal)
                {
                    StunPlayer();
                }
                playerPosture.posDmg(0.5f);
                hitRegistered = true;
                return;
            }
            if (enemyStateMachine != null && enemyStateMachine.TryCounterDodge(true))
            {
                hitRegistered = true;   // this swing is spent - he slipped it
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
            LogScore(true);
            FightSounds.EnemyHit();
            KnockbackEnemy();
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
                    FightSounds.PlayerBlocked(true);
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
                    FightSounds.PlayerBlocked(false);
                    PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                    if (playerPosture.postureSlider.value >= postureMaxVal)
                    {
                        StunPlayer();
                    }
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
            LogScore(false);
            FightSounds.PlayerHit();
            KnockbackPlayer();
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
                    FightSounds.PlayerBlocked(true);
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
                    FightSounds.PlayerBlocked(false);
                    PushBackPlayer(enemyStateMachine.transform, knockbackDist);
                    if (playerPosture.postureSlider.value >= postureMaxVal)
                    {
                        StunPlayer();
                    }
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
            LogScore(false);
            FightSounds.PlayerHit();
            KnockbackPlayer();
            if (playerStateMachine != null) playerStateMachine.PlayHurt(gameObject.tag == "ePunch");
            hitRegistered = true;
            if (matchScore != null) matchScore.AddEnemyPoint(PlayerStateMachine.AttackTypes.Kick);
            matchTimer.RegisterHit();
        }
    }

    private void LogScore(bool playerScored)
    {
        float now = Time.time;
        if (playerScored) lastPlayerScoreTime = now; else lastEnemyScoreTime = now;
        if (!logHits) return;
        Debug.Log($"[Hit] {(playerScored ? "PLAYER scores" : "ENEMY scores")} with '{gameObject.name}' ({gameObject.tag}) | " +
                  $"enemyState={enemyStateMachine.currentState} counterWindow={enemyStateMachine.InCounterWindow} " +
                  $"playerState={(playerStateMachine != null ? playerStateMachine.currentState.ToString() : "?")} " +
                  $"playerAttackType={(playerStateMachine != null ? playerStateMachine.currentAttackType.ToString() : "?")}");
        if (Mathf.Abs(lastPlayerScoreTime - lastEnemyScoreTime) < 0.3f)
            Debug.LogWarning("[Hit] DOUBLE HIT: both sides scored within 0.3s of each other (see the two [Hit] lines above).");
    }

    private void KnockbackEnemy()
    {
        if (hitKnockbackDist <= 0f || enemyStateMachine == null || player == null) return;
        StartCoroutine(SlideRoutine(enemyStateMachine.transform, player.transform, false));
    }

    private void KnockbackPlayer()
    {
        if (hitKnockbackDist <= 0f || enemyStateMachine == null || player == null) return;
        StartCoroutine(SlideRoutine(player.transform, enemyStateMachine.transform, true));
    }

    /// <summary>Slides 'victim' directly away from 'source' along X over hitKnockbackTime.</summary>
    private IEnumerator SlideRoutine(Transform victim, Transform source, bool usesController)
    {
        float dir = Mathf.Sign(victim.position.x - source.position.x);
        if (dir == 0f) dir = 1f;
        float t = 0f, moved = 0f, total = hitKnockbackDist, dur = Mathf.Max(0.02f, hitKnockbackTime);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float target = total * Mathf.Clamp01(t / dur);
            float step = target - moved;
            moved = target;
            Vector3 delta = new Vector3(dir * step, 0f, 0f);
            if (usesController && player != null && player.controller != null && player.controller.enabled)
                player.controller.Move(delta);
            else
                victim.position += delta;
            yield return null;
        }
    }

    private void PlayEnemyHurt(string preferred, string fallback)
    {
        // Always the ENEMY's own animator (his state machine knows it), not whatever this hitbox's own
        // 'Anim' field happens to point at - if that field was ever assigned to the player's animator,
        // the player would flinch every time he lands a hit.
        Animator target = enemyStateMachine != null && enemyStateMachine.anim != null ? enemyStateMachine.anim : anim;
        if (target == null) return;
        if (logHits && anim != null && anim != target)
            Debug.LogWarning($"[Hit] '{gameObject.name}' has its Anim field set to '{anim.gameObject.name}', which is NOT the " +
                             $"enemy's animator ('{target.gameObject.name}'). Playing the hurt animation on the enemy anyway - " +
                             $"you can clear or fix that field.");

        string clip = target.HasState(0, Animator.StringToHash(preferred)) ? preferred : fallback;
        if (clip != preferred)
            Debug.LogWarning($"[DmgDetection] Enemy animator has no state '{preferred}' - " +
                             $"playing '{fallback}' instead. Add the state, or change the name in the Inspector.");
        if (logHits) Debug.Log($"[Hit] Enemy hurt animation '{clip}' played on '{target.gameObject.name}'.");
        target.Play(clip);
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