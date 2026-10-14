using System.Collections;
using UnityEngine;

/// <summary>
/// What the fighters do when play is stopped, and how they are put back afterwards. The order is:
/// point / warning -> hit anims (untouched) -> referee says Yame -> reset (walk back, face each other, Idle)
/// -> THEN the reactions below, while the referee gives out the point. Restrained on purpose:
/// this is a disciplined sport, so no cheering.
///
///   POINT        the scorer quickly turns to face the referee and bows (his "point gained" animation); the
///                other fighter plays his "point lost" (hurt) reaction where he stands. Then the scorer turns back
///                to the opponent and plays a quick "ready" bow.
///   WARNING      both fighters stand still. The one warned plays a "warned" animation, the other a slight
///                "other was warned" reaction (also on the deduction). Nobody turns or moves.
///   RESET        when the referee fires On Ready To Reset (after Yame) both fighters turn to face each other
///                (toward their START spots, since that is where they are walking back to), and once the walk
///                back is over both go back to Idle.
///
/// All animations are Animator STATE names (blank = skipped, missing = skipped quietly), played with
/// Animator.Play, so no triggers or parameters are needed. It listens to the referee / OutOfBounds itself.
///
/// It never touches the existing hit / score animations - it only adds the reactions after the reset.
///
/// Setup: one empty object in the fight scene with this component. Everything is auto-found; type the state names.
/// Make sure the states exist on each fighter's Animator and have an exit back to Idle or are simply left to
/// this script (it returns both to Idle after the reset).
/// </summary>
public class FighterReactions : MonoBehaviour
{
    [Header("Player animations (Animator state names)")]
    [Tooltip("Played right after he has turned to the referee (a small acknowledgement). Optional.")]
    [SerializeField] private string playerPointGainedAnim = "";
    [Tooltip("The BOW to the referee, played Bow Delay seconds after the 'point gained' animation starts - time it to the " +
             "moment the referee awards the point. Blank = no bow.")]
    [SerializeField] private string playerPointBowAnim = "";
    [Tooltip("After the bow he turns back to face the opponent; this ready animation (a quick bow) plays AT THE SAME TIME as that turn. Blank = none.")]
    [SerializeField] private string playerReadyAnim = "";
    [Tooltip("Played when the OPPONENT scores. Blank = leave the normal hit reaction alone.")]
    [SerializeField] private string playerPointLostAnim = "";
    [Tooltip("Played when the player is the one WARNED.")]
    [SerializeField] private string playerWarnedAnim = "";
    [Tooltip("Slight reaction played when the OPPONENT is warned.")]
    [SerializeField] private string playerOtherWarnedAnim = "";
    [Tooltip("Played when the match ends and the PLAYER won / lost.")]
    [SerializeField] private string playerWinAnim = "";
    [SerializeField] private string playerLoseAnim = "";

    [Header("Opponent animations (Animator state names)")]
    [SerializeField] private string enemyPointGainedAnim = "";
    [SerializeField] private string enemyPointBowAnim = "";
    [SerializeField] private string enemyReadyAnim = "";
    [SerializeField] private string enemyPointLostAnim = "";
    [SerializeField] private string enemyWarnedAnim = "";
    [SerializeField] private string enemyOtherWarnedAnim = "";
    [Tooltip("Played when the match ends and the OPPONENT won / lost.")]
    [SerializeField] private string enemyWinAnim = "";
    [SerializeField] private string enemyLoseAnim = "";

    [Header("Injury knockout (the player's injury meter fills)")]
    [Tooltip("Played on the PLAYER the moment the meter fills, blending in from whatever he is doing. Blank = none.")]
    [SerializeField] private string playerInjuredAnim = "";
    [Tooltip("The OPPONENT's reaction to the player being injured (e.g. stepping back, concerned). Blank = none.")]
    [SerializeField] private string enemyInjuryReactionAnim = "";
    [Tooltip("Seconds to blend from the current animation into the injury animations (0 = snap at once).")]
    [SerializeField] private float injuryBlendTime = 0f;
    [Tooltip("After an injury the normal win / lose animations below are skipped (the injury animations stay on).")]
    [SerializeField] private bool skipResultAnimsAfterInjury = true;

    [Header("Match result")]
    [Tooltip("Seconds after the result is announced before the win / lose animations play (lets the last hit finish).")]
    [SerializeField] private float resultDelay = 0.6f;

    [Header("Timing")]
    [Tooltip("Seconds after the fighters are back on their start spots (and Idle) before the reaction starts.")]
    [SerializeField] private float reactionDelay = 0.25f;
    [Tooltip("Seconds from the scorer's 'point gained' animation starting (or from finishing the turn if that is blank) " +
             "until his bow starts. Set it so the bow lands when the referee awards the point.")]
    [SerializeField] private float bowDelay = 0.5f;
    [Tooltip("Seconds from the bow STARTING until the ready animation + turn start. -1 = automatic: exactly the length of " +
             "the bow clip (they start the moment the bow ends). 0 = at once, right as the bow starts.")]
    [SerializeField] private float bowHold = -1f;
    [Tooltip("Only used when Bow Hold is -1: the ready animation starts after this fraction of the bow clip (0.8 = cut the bow 20% short).")]
    [Range(0.1f, 1f)][SerializeField] private float bowAutoFraction = 0.8f;
    [Tooltip("The turn back to the opponent starts this far into the ready animation (0.4 = it turns during the last 60%) " +
             "and takes the rest of the clip. Ignored if there is no ready animation.")]
    [Range(0f, 0.95f)][SerializeField] private float turnStartFraction = 0.4f;
    [Tooltip("Safety limit: most seconds to wait for the ready animation to finish before going to Idle anyway.")]
    [SerializeField] private float readyMaxSeconds = 3f;
    [Tooltip("How quickly (seconds) he turns back to face the opponent.")]
    [SerializeField] private float faceOpponentTime = 0.3f;
    [Tooltip("The scorer turns to face the referee this quickly (seconds), then his animations play.")]
    [SerializeField] private float faceRefereeTime = 0.3f;
    [SerializeField] private Transform refereeTransform;

    [Header("Reset (after Yame)")]
    [SerializeField] private string idleState = "Idle";
    [Tooltip("Turn both fighters to face each other (toward their start spots) when the reset begins.")]
    [SerializeField] private bool resetFacing = true;
    [SerializeField] private float resetTurnDuration = 0.3f;
    [Tooltip("How long the walk back lasts. Set it to GameManager's Reset Duration + a little (e.g. 0.7). " +
             "After this both go to Idle.")]
    [SerializeField] private float resetWalkDuration = 0.7f;
    [Tooltip("Also put the player's state machine back to Idle if he was left mid-action (attack / block / dodge).")]
    [SerializeField] private bool resetPlayerState = true;

    [Header("References (auto-found)")]
    [SerializeField] private RefereeController referee;
    [SerializeField] private OutOfBounds outOfBounds;
    [SerializeField] private PlayerStateMachine playerSm;
    [SerializeField] private EnemyStateMachine enemy;
    [SerializeField] private InjuryMeter injuryMeter;
    private bool injured;

    [SerializeField] private bool logReactions = true;

    private Transform playerRoot, enemyRoot;
    private Animator playerAnim, enemyAnim;
    private Vector3 playerStart, enemyStart;
    private Coroutine playerRoutine, enemyRoutine, resetRoutine;

    private void Start()
    {
        if (referee == null) referee = FindFirstObjectByType<RefereeController>();
        if (outOfBounds == null) outOfBounds = FindFirstObjectByType<OutOfBounds>();
        if (playerSm == null) playerSm = PlayerStateMachine.instance != null ? PlayerStateMachine.instance : FindFirstObjectByType<PlayerStateMachine>();
        if (enemy == null) enemy = FindFirstObjectByType<EnemyStateMachine>();

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        playerRoot = pc != null ? pc.transform : (playerSm != null ? playerSm.transform : null);
        enemyRoot = enemy != null ? enemy.transform : null;
        playerAnim = playerSm != null ? playerSm.animator : null;
        enemyAnim = enemy != null ? enemy.anim : null;

        if (refereeTransform == null && referee != null) refereeTransform = referee.RefereeTransform;
        if (refereeTransform == null)
            Debug.LogWarning("[FighterReactions] No referee transform - the scorer won't turn to the referee. Assign 'Referee Transform' or the Referee Transform on the RefereeController.");
        if (playerRoot != null) playerStart = playerRoot.position;
        if (enemyRoot != null) enemyStart = enemyRoot.position;

        if (referee != null)
        {
            referee.onPlayerPoint.AddListener(OnPlayerPoint);
            referee.onEnemyPoint.AddListener(OnEnemyPoint);
            referee.onReadyToReset.AddListener(OnReadyToReset);
            referee.OnResultAnnounced += OnResult;
        }
        if (injuryMeter == null) injuryMeter = InjuryMeter.instance != null ? InjuryMeter.instance : FindFirstObjectByType<InjuryMeter>();
        if (injuryMeter != null) injuryMeter.OnInjured += OnInjured;
        if (referee == null) Debug.LogWarning("[FighterReactions] No RefereeController found - nothing will trigger.");
        if (outOfBounds != null) outOfBounds.OnPenaltyCalled += OnPenalty;

        if (logReactions)
            Debug.Log($"[FighterReactions] Ready. referee={(referee != null ? referee.name : "NONE")}, " +
                      $"player animator={(playerAnim != null ? playerAnim.gameObject.name : "NONE")}, " +
                      $"enemy animator={(enemyAnim != null ? enemyAnim.gameObject.name : "NONE")}, " +
                      $"referee transform={(refereeTransform != null ? refereeTransform.name : "NONE")}, " +
                      $"outOfBounds={(outOfBounds != null ? "yes" : "none")}.");
    }

    private void OnDestroy()
    {
        if (referee != null)
        {
            referee.onPlayerPoint.RemoveListener(OnPlayerPoint);
            referee.onEnemyPoint.RemoveListener(OnEnemyPoint);
            referee.onReadyToReset.RemoveListener(OnReadyToReset);
            referee.OnResultAnnounced -= OnResult;
        }
        if (outOfBounds != null) outOfBounds.OnPenaltyCalled -= OnPenalty;
        if (injuryMeter != null) injuryMeter.OnInjured -= OnInjured;
    }

    // Match decided: drop anything pending / running, then the winner and loser play their result animations.
    private void OnResult(bool playerWon)
    {
        if (injured && skipResultAnimsAfterInjury) return;   // the injury animations are already playing
        pending = Pending.None;
        if (resetRoutine != null) { StopCoroutine(resetRoutine); resetRoutine = null; }
        if (logReactions) Debug.Log($"[FighterReactions] Match result: {(playerWon ? "player" : "opponent")} won.");
        StartRoutine(true, ResultRoutine(true, playerWon ? playerWinAnim : playerLoseAnim));
        StartRoutine(false, ResultRoutine(false, playerWon ? enemyLoseAnim : enemyWinAnim));
    }

    // The meter filled: the match doesn't pause, it just ends - so go straight into the injury animations from
    // whatever the fighters are doing right now.
    private void OnInjured()
    {
        injured = true;
        pending = Pending.None;
        if (resetRoutine != null) { StopCoroutine(resetRoutine); resetRoutine = null; }
        if (playerRoutine != null) { StopCoroutine(playerRoutine); playerRoutine = null; }
        if (enemyRoutine != null) { StopCoroutine(enemyRoutine); enemyRoutine = null; }
        if (logReactions) Debug.Log("[FighterReactions] Injury knockout - playing the injury animations now.");
        // Cancel what each fighter's own scripts are doing (attack routines, hit windows, movement), THEN play the animation.
        if (playerSm != null) playerSm.EnterInjured(playerInjuredAnim, injuryBlendTime);
        else PlayStateBlend(playerAnim, playerInjuredAnim);

        if (enemy != null) enemy.StopForRoundReset();
        PlayStateBlend(enemyAnim, enemyInjuryReactionAnim);
        StartCoroutine(VerifyInjury());
    }

    // Diagnostics: a few frames after the injury, log what the player's Animator is really doing.
    private IEnumerator VerifyInjury()
    {
        if (playerAnim == null) { Debug.LogWarning("[FighterReactions] CHECK injury: no player Animator found."); yield break; }
        if (string.IsNullOrEmpty(playerInjuredAnim))
        {
            Debug.LogWarning("[FighterReactions] CHECK injury: 'Player Injured Anim' is BLANK in the Inspector - type the state name there.");
            yield break;
        }
        int hash = Animator.StringToHash(playerInjuredAnim);
        for (int i = 0; i < 3; i++) yield return null;
        AnimatorStateInfo s = playerAnim.GetCurrentAnimatorStateInfo(0);
        var clips = playerAnim.GetCurrentAnimatorClipInfo(0);
        Debug.Log($"[FighterReactions] CHECK injury: animator '{playerAnim.gameObject.name}' hasState={playerAnim.HasState(0, hash)} " +
                  $"inInjuredState={s.shortNameHash == hash} inTransition={playerAnim.IsInTransition(0)} " +
                  $"clip={(clips.Length > 0 ? clips[0].clip.name : "none")} controller={(playerAnim.runtimeAnimatorController != null ? playerAnim.runtimeAnimatorController.name : "NONE")} " +
                  $"psmState={(playerSm != null ? playerSm.currentState.ToString() : "n/a")}.");
    }

    private void PlayStateBlend(Animator a, string state)
    {
        if (a == null || string.IsNullOrEmpty(state)) return;
        int hash = Animator.StringToHash(state);
        if (!a.HasState(0, hash))
        {
            Debug.LogWarning($"[FighterReactions] {a.gameObject.name}: Animator has no state '{state}' on layer 0 - skipped.");
            return;
        }
        if (injuryBlendTime > 0f) a.CrossFadeInFixedTime(hash, injuryBlendTime, 0, 0f);
        else a.Play(hash, 0, 0f);
        if (logReactions) Debug.Log($"[FighterReactions] {a.gameObject.name}: blending into '{state}'.");
    }

    private IEnumerator ResultRoutine(bool isPlayer, string state)
    {
        if (resultDelay > 0f) yield return new WaitForSecondsRealtime(resultDelay);
        if (string.IsNullOrEmpty(state)) yield break;

        // Cancel whatever the fighter's own scripts are still doing (a hurt / stun / attack routine would otherwise
        // put him back in his old pose), THEN play the animation.
        if (isPlayer)
        {
            if (playerSm != null) { playerSm.EnterFinalPose(state, 0f); yield break; }
        }
        else if (enemy != null) enemy.StopForRoundReset();
        PlayState(isPlayer ? playerAnim : enemyAnim, state);
    }

    // ------------------------------------------------------------------ reactions
    // The point / warning is only REMEMBERED here. The reactions play after the reset, once both fighters are
    // back on their start spots (see ResetRoutine) - the referee only says Yame until then.
    private enum Pending { None, PlayerScored, EnemyScored, PlayerWarned, EnemyWarned }
    private Pending pending = Pending.None;

    private void OnPlayerPoint() { pending = Pending.PlayerScored; if (logReactions) Debug.Log("[FighterReactions] Player point noted - reaction plays after the reset."); }
    private void OnEnemyPoint() { pending = Pending.EnemyScored; if (logReactions) Debug.Log("[FighterReactions] Enemy point noted - reaction plays after the reset."); }
    private void OnPenalty(bool offenderIsPlayer, bool deduction)
    {
        pending = offenderIsPlayer ? Pending.PlayerWarned : Pending.EnemyWarned;
        if (logReactions) Debug.Log($"[FighterReactions] {(offenderIsPlayer ? "Player" : "Enemy")} warning noted - reaction plays after the reset.");
    }

    private IEnumerator RunPending()
    {
        Pending p = pending;
        pending = Pending.None;
        if (p == Pending.None) { if (logReactions) Debug.Log("[FighterReactions] Nothing pending after the reset - no reaction."); yield break; }
        if (logReactions) Debug.Log($"[FighterReactions] Playing reactions for: {p}.");
        if (reactionDelay > 0f) yield return new WaitForSecondsRealtime(reactionDelay);

        switch (p)
        {
            case Pending.PlayerScored:
                Scorer(true, playerPointGainedAnim, playerPointBowAnim, playerReadyAnim);
                Plain(false, enemyPointLostAnim);
                break;
            case Pending.EnemyScored:
                Scorer(false, enemyPointGainedAnim, enemyPointBowAnim, enemyReadyAnim);
                Plain(true, playerPointLostAnim);
                break;
            // Warning: both stand still; the offender and the other fighter each play a slight reaction.
            case Pending.PlayerWarned:
                Plain(true, playerWarnedAnim);
                Plain(false, enemyOtherWarnedAnim);
                break;
            case Pending.EnemyWarned:
                Plain(true, playerOtherWarnedAnim);
                Plain(false, enemyWarnedAnim);
                break;
        }
    }

    // The scorer turns to the referee, then bows.
    private void Scorer(bool isPlayer, string state, string bowState, string readyState)
    {
        StartRoutine(isPlayer, ScorerRoutine(isPlayer, state, bowState, readyState));
    }

    // Plays a reaction where the fighter stands.
    private void Plain(bool isPlayer, string state)
    {
        if (string.IsNullOrEmpty(state)) return;
        StartRoutine(isPlayer, PlainRoutine(isPlayer, state));
    }

    private void StartRoutine(bool isPlayer, IEnumerator routine)
    {
        if (isPlayer) { if (playerRoutine != null) StopCoroutine(playerRoutine); playerRoutine = StartCoroutine(routine); }
        else { if (enemyRoutine != null) StopCoroutine(enemyRoutine); enemyRoutine = StartCoroutine(routine); }
    }

    private IEnumerator PlainRoutine(bool isPlayer, string state)
    {
        yield return null;
        PlayState(isPlayer ? playerAnim : enemyAnim, state);
    }

    private IEnumerator ScorerRoutine(bool isPlayer, string state, string bowState, string readyState)
    {
        Transform who = isPlayer ? playerRoot : enemyRoot;
        if (who != null && refereeTransform != null)
        {
            Vector3 dir = refereeTransform.position - who.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                yield return TurnTo(who, Quaternion.LookRotation(dir.normalized, Vector3.up), faceRefereeTime);
        }
        Animator a = isPlayer ? playerAnim : enemyAnim;
        PlayState(a, state);
        if (!string.IsNullOrEmpty(bowState))
        {
            if (bowDelay > 0f) yield return new WaitForSecondsRealtime(bowDelay);
            PlayState(a, bowState);
            if (bowHold < 0f)
            {
                // Auto: wait exactly as long as the bow clip, so the ready anim + turn start the moment the bow ends.
                yield return null;
                float len = a != null ? a.GetCurrentAnimatorStateInfo(0).length : 0f;
                if (len > 0f) yield return new WaitForSecondsRealtime(len * bowAutoFraction);
            }
            else if (bowHold > 0f) yield return new WaitForSecondsRealtime(bowHold);
        }

        // Then turn back to the opponent and show he is ready (a quick bow). The ready animation plays WHILE he turns.
        Transform other = isPlayer ? enemyRoot : playerRoot;
        bool turning = false;
        PlayState(a, readyState);

        // Turn during the last part of the ready animation: wait turnStartFraction of its length, then turn over the rest.
        float turnDuration = faceOpponentTime;
        if (!string.IsNullOrEmpty(readyState) && a != null)
        {
            yield return null;   // let Play() take effect so the clip length is the ready clip's
            float len = a.GetCurrentAnimatorStateInfo(0).length;
            if (len > 0f)
            {
                float wait = len * turnStartFraction;
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                turnDuration = Mathf.Max(0.05f, len - wait);
            }
        }
        if (who != null && other != null)
        {
            Vector3 back = other.position - who.position;
            back.y = 0f;
            if (back.sqrMagnitude > 0.0001f)
            {
                turning = true;
                StartCoroutine(TurnThen(who, Quaternion.LookRotation(back.normalized, Vector3.up), turnDuration, () => turning = false));
            }
        }

        // Wait for the turn and for the ready animation to finish, then go back to Idle.
        if (!string.IsNullOrEmpty(readyState)) yield return WaitForStateEnd(a, readyState, readyMaxSeconds);
        while (turning) yield return null;
        PlayState(a, idleState);
    }

    private IEnumerator TurnThen(Transform who, Quaternion to, float duration, System.Action done)
    {
        yield return TurnTo(who, to, duration);
        done?.Invoke();
    }

    // Waits until the Animator has finished 'state' (non-looping clip played through, or it left the state), up to maxSeconds.
    private IEnumerator WaitForStateEnd(Animator a, string state, float maxSeconds)
    {
        if (a == null) yield break;
        int hash = Animator.StringToHash(state);
        yield return null;   // let Play() take effect
        for (float t = 0f; t < maxSeconds; t += Time.unscaledDeltaTime)
        {
            AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
            bool inState = st.shortNameHash == hash || st.fullPathHash == hash;
            if (!inState) yield break;                           // transitioned out by itself
            if (!st.loop && st.normalizedTime >= 1f) yield break; // played through
            yield return null;
        }
    }

    // ------------------------------------------------------------------ reset
    private void OnReadyToReset()
    {
        if (logReactions) Debug.Log($"[FighterReactions] Reset began (pending reaction: {pending}).");
        if (playerRoutine != null) { StopCoroutine(playerRoutine); playerRoutine = null; }
        if (enemyRoutine != null) { StopCoroutine(enemyRoutine); enemyRoutine = null; }
        if (resetRoutine != null) StopCoroutine(resetRoutine);
        resetRoutine = StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        if (resetPlayerState && playerSm != null)
        {
            var st = playerSm.currentState;
            if (st == PlayerStateMachine.PlayerStates.Attack || st == PlayerStateMachine.PlayerStates.Block ||
                st == PlayerStateMachine.PlayerStates.Dodge || st == PlayerStateMachine.PlayerStates.Move)
                playerSm.ChangeState(PlayerStateMachine.PlayerStates.Idle);
        }

        if (resetFacing && playerRoot != null && enemyRoot != null)
        {
            // They walk back to their START spots, so face each other along that line.
            Vector3 line = enemyStart - playerStart;
            line.y = 0f;
            if (line.sqrMagnitude > 0.0001f)
            {
                StartCoroutine(TurnTo(playerRoot, Quaternion.LookRotation(line.normalized, Vector3.up), resetTurnDuration));
                StartCoroutine(TurnTo(enemyRoot, Quaternion.LookRotation(-line.normalized, Vector3.up), resetTurnDuration));
            }
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, resetWalkDuration));

        PlayState(playerAnim, idleState);
        PlayState(enemyAnim, idleState);
        if (logReactions) Debug.Log("[FighterReactions] Reset done: both fighters back to Idle.");

        // Everyone is back on their start spots: now the point / warning reactions.
        yield return RunPending();
        resetRoutine = null;
    }

    private IEnumerator TurnTo(Transform who, Quaternion to, float duration)
    {
        Quaternion from = who.rotation;
        float dur = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < dur;)
        {
            t += Time.unscaledDeltaTime;
            who.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        who.rotation = to;
    }

    private void PlayState(Animator a, string state)
    {
        if (a == null || string.IsNullOrEmpty(state)) return;
        int hash = Animator.StringToHash(state);
        if (a.HasState(0, hash))
        {
            a.Play(hash, 0, 0f);
            if (logReactions) Debug.Log($"[FighterReactions] {a.gameObject.name}: '{state}'.");
        }
        else if (logReactions) Debug.LogWarning($"[FighterReactions] {a.gameObject.name} has no state '{state}' - skipped.");
    }
}