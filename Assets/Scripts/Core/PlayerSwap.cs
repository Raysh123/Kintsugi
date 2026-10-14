using System.Collections;
using UnityEngine;

/// <summary>
/// The SWAP: press Block and Dodge together (they don't have to be on the same frame - see Leeway)
/// while the enemy is close AND throwing a PUNCH (never a kick, never when he isn't attacking) and you grab him and trade places: you slide to where he stood, he is carried
/// to where you stood, passing each other on opposite sides so the models don't clip.
///
/// Block runs exactly as normal the moment you press it, so a swap that coincides with an incoming attack is
/// still a normal block, or a PERFECT block if the timing is right. Only the DODGE is held back, for at most
/// 'Swap Leeway' seconds, and only when the enemy is within 'Swap Range' (a dodge with nobody near is
/// instant, as before). If Block doesn't arrive in that time the held dodge simply happens.
///
/// Setup: put this on the player (same object as PlayerStateMachine / PlayerController). Assign nothing if
/// the enemy is already set on PlayerStateMachine. The two animations are optional - if a state name doesn't
/// exist on that Animator the swap still happens, just without an animation.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerSwap : MonoBehaviour
{
    /// <summary>True while the swap is playing. Walking, attacking and dodging are locked meanwhile.</summary>
    public static bool Swapping { get; private set; }

    [Header("Trigger")]
    [SerializeField] private bool swapEnabled = true;
    [Tooltip("Keyboard key that also triggers the swap (handy for testing before the Input Action is set up). " +
             "None = only the Input Action. Needs 'Active Input Handling: Both'.")]
    [SerializeField] private KeyCode fallbackKey = KeyCode.F;
    [Tooltip("OFF (default) = swap is its own button. ON = pressing Block and Dodge together ALSO swaps.")]
    [SerializeField] private bool alsoAllowBlockDodgeCombo = false;
    [Tooltip("How close (units, along X) the enemy must be for the swap to be possible.")]
    [SerializeField] private float swapRange = 1.8f;
    [Tooltip("Block and Dodge may be pressed up to this many seconds apart and still count as 'together'. " +
             "This is also the most a dodge is delayed when the enemy is within Swap Range.")]
    [SerializeField] private float swapLeeway = 0.1f;
    [Tooltip("Seconds before another swap is allowed (counted from the end of one).")]
    [SerializeField] private float swapCooldown = 1.0f;

    [Header("Phases (the swap is three parts, in this order)")]
    [Tooltip("1) SWAP READY - the grab. Both fighters stay where they are and face as they were; only the ready " +
             "animations play. Make it as long as your 'ready' clip.")]
    [SerializeField] private float readyDuration = 0.2f;
    [Tooltip("2) SWAP - the fighters trade places and both turn 180 degrees, over this whole phase. " +
             "Make it as long as your 'swap' clip.")]
    [SerializeField] private float swapPhaseDuration = 0.3f;
    [Tooltip("3) PUSH - the enemy is shoved away from you, over this whole phase. Make it as long as your " +
             "'push / pushed' clips. Total swap time = Ready + Swap + Push.")]
    [SerializeField] private float pushDuration = 0.2f;
    [Tooltip("Inside the PUSH phase: seconds the push animations play BEFORE the enemy actually starts sliding away " +
             "(the moment his body takes the hit - set it to when the hands connect in your push clip). " +
             "He then slides for the rest of the push phase. Capped just under Push Duration.")]
    [SerializeField] private float pushImpactDelay = 0.08f;

    [Header("Movement")]
    [Tooltip("How far each fighter sidesteps while passing, so they don't overlap. Positive = the ENEMY is " +
             "dragged toward the PLAYER'S RIGHT (and the player passes on his own left). Negative = the enemy is " +
             "dragged toward the player's LEFT. 'Right' is the player's own body right, so it is the same " +
             "whichever side of the screen he is on.")]
    [SerializeField] private float passSideOffset = 0.4f;
    [Tooltip("Both fighters turn 180 degrees during the SWAP phase, ALWAYS in this direction (seen from above), " +
             "and the enemy is dragged to the player's own right (flip Pass Side Offset's sign for left). So the swap " +
             "looks identical from either side and ONE set of animations works everywhere.")]
    [SerializeField] private bool turnClockwise = true;
    [Tooltip("ON = both fighters are turned 180 degrees by the swap itself. OFF = the swap doesn't touch " +
             "rotation at all (the normal 'face the opponent' logic turns them afterwards).")]
    [SerializeField] private bool turnDuringSwap = true;
    [Tooltip("After the swap ends he does nothing for this many seconds. 0 = he can attack again straight away " +
             "(the swap is only for escaping a corner, not an advantage).")]
    [SerializeField] private float enemyRecoveryAfterSwap = 0f;

    [Header("Push (knockback on the enemy)")]
    [Tooltip("How much further away from you the enemy ends up than where you stood, shoved during the PUSH phase. " +
             "0 = lands exactly where you were.")]
    [SerializeField] private float enemyKnockbackDist = 0.5f;

    [Header("Animations (Animator state names, optional - missing ones are skipped)")]
    [SerializeField] private string playerReadyAnim = "Swap_Ready_Player";
    [SerializeField] private string playerSwapAnim = "Swap_Player";
    [SerializeField] private string playerPushAnim = "Push_Player";
    [SerializeField] private string enemyReadyAnim = "Swap_Ready_Enemy";
    [SerializeField] private string enemySwapAnim = "Swap_Enemy";
    [SerializeField] private string enemyPushedAnim = "Pushed_Enemy";
    [SerializeField] private string idleAnim = "Idle";

    [Header("References (auto-found if empty)")]
    [SerializeField] private EnemyStateMachine enemy;
    [SerializeField] private MatchTimer matchTimer;

    [Header("Debug")]
    [SerializeField] private bool logSwap = true;

    private PlayerStateMachine psm;
    private PlayerController pc;
    private bool dodgeHeldPrev;
    private bool swapHeldPrev;
    private bool fallbackBroken;

    private bool FallbackKeyDown()
    {
        if (fallbackKey == KeyCode.None || fallbackBroken) return false;
        try { return Input.GetKey(fallbackKey); }
        catch { fallbackBroken = true; return false; }   // old Input Manager is disabled
    }

    private void TrySwapButton()
    {
        if (!CanSwapNow(out string why))
        {
            if (logSwap) Debug.Log($"[Swap] Swap button pressed - no swap, because: {why}.");
            if (logSwap && enemy != null)
            {
                enemy.IncomingIsPunch(out string info);
                Debug.Log($"[Swap] (enemy detail: {info})");
            }
            return;
        }
        // The swap is its own move (no block involved). Its cost is a dodge's injury amount.
        if (InjuryMeter.instance != null) InjuryMeter.instance.AddDodgeInjury();
        if (logSwap) Debug.Log($"[Swap] SWAP (button). Enemy state={enemy.currentState}, gap={GapToEnemy():F2}.");
        StartCoroutine(SwapRoutine());
    }
    private int freshDodgeFrame = -1;
    private bool pending;
    private float pendingStart, pendingUntil;
    private float nextSwapTime;
    private bool suppressDodgeUntilRelease;

    private void Awake() { Swapping = false; }
    private void OnDisable() { Swapping = false; }

    private void Start()
    {
        psm = GetComponent<PlayerStateMachine>();
        if (psm == null) psm = PlayerStateMachine.instance;
        pc = psm != null ? psm.player : GetComponent<PlayerController>();
        if (enemy == null && psm != null) enemy = psm.enemy;
        if (enemy == null) enemy = FindFirstObjectByType<EnemyStateMachine>();
        if (matchTimer == null) matchTimer = FindFirstObjectByType<MatchTimer>();
        if (psm != null) { psm.RegisterSwap(this); if (logSwap) Debug.Log($"[Swap] Ready. Enemy='{(enemy != null ? enemy.name : "NONE")}', player controller='{(pc != null ? pc.name : "NONE")}'."); }
        else Debug.LogWarning("[Swap] No PlayerStateMachine found - the swap can't work.");
    }

    private void Update()
    {
        var ih = InputHandler.instance;
        if (ih == null) return;

        // ---- the swap button
        bool swapDown = ih.swap || FallbackKeyDown();
        if (swapDown && !swapHeldPrev) TrySwapButton();
        swapHeldPrev = swapDown;

        bool dodgeNow = ih.dodge;
        if (dodgeNow && !dodgeHeldPrev) freshDodgeFrame = Time.frameCount;
        dodgeHeldPrev = dodgeNow;
        if (!dodgeNow) suppressDodgeUntilRelease = false;

        if (!pending) return;

        // A held dodge is waiting for a block.
        if (Swapping || (matchTimer != null && matchTimer.matchStopped)) { pending = false; return; }

        if (psm != null && psm.LastBlockPressTime >= pendingStart - 0.001f && CanSwapNow())
        {
            pending = false;
            StartSwap("block arrived within the leeway");
        }
        else if (Time.time >= pendingUntil)
        {
            pending = false;
            if (logSwap) Debug.Log("[Swap] No block within the leeway - doing the normal dodge.");
            if (psm != null) psm.BeginDodgeNow();
        }
    }

    // ------------------------------------------------------------------ called by PlayerStateMachine.TryConsumeDodge
    /// <summary>Returns true if this dodge request should NOT start a normal dodge right now (the swap
    /// has it, is waiting on it, or is locking input). Safe to call many times a frame.</summary>
    public bool InterceptDodgeRequest()
    {
        if (!swapEnabled || !alsoAllowBlockDodgeCombo) return false;
        if (Swapping || pending) return true;
        if (suppressDodgeUntilRelease && InputHandler.instance != null && InputHandler.instance.dodge) return true;

        bool fresh = freshDodgeFrame == Time.frameCount;
        if (!fresh) return false;                    // a held button, not a new press: normal rules
        if (!CanSwapNow(out string why))             // enemy not close / on cooldown / wrong state: normal dodge
        {
            if (logSwap) Debug.Log($"[Swap] Dodge pressed - no swap, because: {why}.");
            return false;
        }

        bool blockRecent = psm != null && Time.time - psm.LastBlockPressTime <= swapLeeway;
        if (blockRecent)
        {
            StartSwap("dodge pressed right after block");
            return true;
        }

        pending = true;
        pendingStart = Time.time;
        pendingUntil = Time.time + swapLeeway;
        return true;
    }

    private bool CanSwapNow() => CanSwapNow(out _);

    // Measured from the PLAYER'S body (PlayerController's transform), not from whatever object this script sits on -
    // that object can be a static one that stays at the scene start position.
    private float GapToEnemy()
    {
        Transform body = pc != null ? pc.transform : transform;
        return Mathf.Abs(enemy.transform.position.x - body.position.x);
    }

    private bool CanSwapNow(out string why)
    {
        why = "";
        if (!swapEnabled) { why = "Swap Enabled is off"; return false; }
        if (Swapping) { why = "already swapping"; return false; }
        if (psm == null || pc == null) { why = "no PlayerStateMachine / PlayerController found"; return false; }
        if (enemy == null || enemy.Player == null) { why = "no enemy assigned"; return false; }
        if (Time.time < nextSwapTime) { why = $"swap cooldown ({nextSwapTime - Time.time:F1}s left)"; return false; }
        if (matchTimer != null && matchTimer.matchStopped) { why = "match is stopped"; return false; }
        float gap = GapToEnemy();
        if (gap > swapRange) { why = $"enemy too far (gap {gap:F2} > Swap Range {swapRange:F2})"; return false; }

        var ps = psm.currentState;
        if (ps != PlayerStateMachine.PlayerStates.Idle && ps != PlayerStateMachine.PlayerStates.Move &&
            ps != PlayerStateMachine.PlayerStates.Block) { why = $"player state is {ps}"; return false; }
        // Only against an incoming PUNCH: he must be mid-attack and his next hit a punch (not a kick).
        if (enemy.currentState != EnemyStateMachine.EnemyState.Attack) { why = $"enemy is not attacking (state {enemy.currentState})"; return false; }
        if (!enemy.IncomingIsPunch(out string punchInfo)) { why = "his incoming hit is not a punch (or no hit event found ahead in his clip) [" + punchInfo + "]"; return false; }
        return true;
    }

    private void StartSwap(string why)
    {
        if (!psm.ConsumeDodgeForSwap())
        {
            if (logSwap) Debug.Log("[Swap] Dodge cooldown is still running - no swap.");
            return;
        }
        if (logSwap) Debug.Log($"[Swap] SWAP ({why}). Enemy state={enemy.currentState}, gap={GapToEnemy():F2}.");
        suppressDodgeUntilRelease = true;
        StartCoroutine(SwapRoutine());
    }

    // ------------------------------------------------------------------ the swap itself
    private IEnumerator SwapRoutine()
    {
        Swapping = true;
        Transform pt = pc.transform;
        Transform et = enemy.transform;
        Vector3 p0 = pt.position, e0 = et.position;
        // The enemy ends up where the player stood, pushed further away from where the player ends up.
        float awayDir = Mathf.Sign(p0.x - e0.x);
        if (awayDir == 0f) awayDir = 1f;
        Vector3 eEnd = new Vector3(p0.x + awayDir * enemyKnockbackDist, e0.y, p0.z);

        // Sidestep along the PLAYER'S OWN local right axis (not world Z), so it's the same whichever side he is on.
        Vector3 playerRight = pt.right;
        playerRight.y = 0f;
        playerRight = playerRight.sqrMagnitude > 0.0001f ? playerRight.normalized : Vector3.back;
        Quaternion pRot0 = pt.rotation, eRot0 = et.rotation;
        float turnSign = turnClockwise ? 1f : -1f;
        float ready = Mathf.Max(0f, readyDuration);
        float swapT = Mathf.Max(0.05f, swapPhaseDuration);
        float push = Mathf.Max(0f, pushDuration);

        // His attack is cancelled and he enters the Swap state: he can't attack or move until the whole swap ends.
        enemy.HoldForSwap(ready + swapT + push);

        bool ctrlWas = pc.controller != null && pc.controller.enabled;
        if (pc.controller != null) pc.controller.enabled = false;   // so we can place the player directly

        // ---- 1) SWAP READY: the grab. Nobody moves or turns.
        FightSounds.SwapReady();
        PlayIfExists(psm.animator, playerReadyAnim);
        PlayIfExists(enemy.anim, enemyReadyAnim);
        for (float t = 0f; t < ready; t += Time.deltaTime) yield return null;

        // ---- 2) SWAP: trade places and turn, over this phase.
        FightSounds.Swap();
        PlayIfExists(psm.animator, playerSwapAnim);
        PlayIfExists(enemy.anim, enemySwapAnim);
        for (float t = 0f; t < swapT;)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / swapT);
            float k = Mathf.SmoothStep(0f, 1f, u);
            float arc = Mathf.Sin(k * Mathf.PI) * passSideOffset;
            // The ENEMY is dragged around the player's right side (positive offset); the player passes on the other.
            pt.position = new Vector3(Mathf.Lerp(p0.x, e0.x, k), p0.y, Mathf.Lerp(p0.z, e0.z, k)) - playerRight * arc;
            et.position = new Vector3(Mathf.Lerp(e0.x, p0.x, k), e0.y, Mathf.Lerp(e0.z, p0.z, k)) + playerRight * arc;
            if (turnDuringSwap)
            {
                Quaternion turn = Quaternion.AngleAxis(180f * k * turnSign, Vector3.up);
                pt.rotation = turn * pRot0;
                et.rotation = turn * eRot0;
            }
            yield return null;
        }
        // Land exactly (the enemy on the spot where the player stood).
        pt.position = new Vector3(e0.x, p0.y, e0.z);
        et.position = new Vector3(p0.x, e0.y, p0.z);
        if (turnDuringSwap)
        {
            Quaternion fullTurn = Quaternion.AngleAxis(180f * turnSign, Vector3.up);
            pt.rotation = fullTurn * pRot0;
            et.rotation = fullTurn * eRot0;
        }

        // ---- 3) PUSH: the enemy is shoved away from the player.
        PlayIfExists(psm.animator, playerPushAnim);
        PlayIfExists(enemy.anim, enemyPushedAnim);
        Vector3 pushFrom = et.position;
        float delay = Mathf.Clamp(pushImpactDelay, 0f, Mathf.Max(0f, push - 0.05f));
        float slide = Mathf.Max(0.0001f, push - delay);
        bool pushSfxPlayed = false;
        for (float t = 0f; t < push;)
        {
            t += Time.deltaTime;
            if (!pushSfxPlayed && t >= delay) { pushSfxPlayed = true; FightSounds.SwapPush(); }   // the hands connect
            // Anims are already playing; the body only starts moving once the hands have connected.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - delay) / slide));
            et.position = Vector3.Lerp(pushFrom, eEnd, k);
            yield return null;
        }
        et.position = eEnd;
        if (!pushSfxPlayed) FightSounds.SwapPush();   // very short push: still play it once

        if (pc.controller != null) pc.controller.enabled = ctrlWas;

        // Back to normal.
        var ps = psm.currentState;
        if (ps == PlayerStateMachine.PlayerStates.Block || ps == PlayerStateMachine.PlayerStates.Move ||
            ps == PlayerStateMachine.PlayerStates.Dodge)
        {
            psm.ChangeState(PlayerStateMachine.PlayerStates.Idle);
            if (psm.animator != null) psm.animator.Play(idleAnim);
        }
        enemy.EndSwap(enemyRecoveryAfterSwap);

        nextSwapTime = Time.time + swapCooldown;
        Swapping = false;
    }

    private void PlayIfExists(Animator a, string state)
    {
        if (string.IsNullOrEmpty(state)) return;
        if (a == null) { if (logSwap) Debug.LogWarning($"[Swap] Can't play '{state}': that character's Animator is null."); return; }
        int hash = Animator.StringToHash(state);
        // HasState covers top-level states; the full-path attempt covers states inside a sub-state machine.
        bool found = a.HasState(0, hash);
        if (found)
        {
            a.Play(hash, 0, 0f);
            if (logSwap) { Debug.Log($"[Swap] {a.gameObject.name}: playing '{state}'."); StartCoroutine(VerifyPlaying(a, state, hash)); }
        }
        else if (logSwap) Debug.LogWarning($"[Swap] {a.gameObject.name}: Animator has no state named '{state}' on layer 0 - skipped. " +
                                            "(Check the exact state name, and that it's on the Animator Controller this object actually uses.)");
    }

    // Diagnostic: two frames after Play(), report what the Animator is REALLY doing.
    private IEnumerator VerifyPlaying(Animator a, string state, int hash)
    {
        yield return null;
        yield return null;
        if (a == null) yield break;
        var st = a.GetCurrentAnimatorStateInfo(0);
        bool inState = st.shortNameHash == hash || st.fullPathHash == hash || st.IsName(state);
        string clip = "none";
        var infos = a.GetCurrentAnimatorClipInfo(0);
        if (infos.Length > 0) clip = infos[0].clip != null ? infos[0].clip.name + $" ({infos[0].clip.length:F2}s)" : "null clip";
        Debug.Log($"[Swap] CHECK {a.gameObject.name} '{state}': inState={inState}, inTransition={a.IsInTransition(0)}, " +
                  $"normalizedTime={st.normalizedTime:F2}, clip={clip}, animator.enabled={a.enabled}, speed={a.speed:F2}, " +
                  $"controller={(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NONE")}, " +
                  $"applyRootMotion={a.applyRootMotion}, avatarValid={(a.avatar != null && a.avatar.isValid)}.");
    }
}