using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the choreographed tutorial fight. Lives in the TUTORIAL fight scene (a copy of your fight scene).
/// It does NOT need any change to your existing scripts: it drives the normal EnemyStateMachine from
/// outside.
///
/// HOW THE ENEMY IS CONTROLLED
///   In the tutorial scene, make a second, empty MatchTimer object ("EnemyGate", Auto Start OFF) and put it
///   in the ENEMY's "Match Timer" field. That timer never starts, so the enemy's own Update (his AI) does
///   nothing at all. This director then runs his state handling itself each frame, and only does what a step
///   says: walk up, attack. (The player keeps using the real MatchTimer.)
///
/// STEP TYPES
///   Comic          play a ComicCutscene and wait for it (the opening comic panel).
///   StartMatch     starts the real match clock so the player can move / fight.
///   Panel          a UI panel cutting in (title, text, controls, image). Pauses the game until the player
///                  presses Enter / A (or after 'Seconds' if > 0). Or use your own Custom Panel object.
///   Hint           small non-blocking line at the bottom of the screen. ClearHint removes it.
///   Wait           wait 'Seconds'.
///   EnemyApproach  the enemy walks up to the player.
///   EnemyReposition  the enemy slides back to a given distance (to reset between lessons).
///   EnemyAttack    the enemy walks up (if needed) and attacks. His attack trails play as normal. THE TEACHING
///                  MOMENT: pick 'Teach At' = TrailsAppear (attack starts) or PerfectWindow (trail turns red). At that instant the game
///                  freezes (clock, enemy, everything), a panel shows what to do and which buttons, and the
///                  game resumes the moment the player does it (Require Response = Block / Dodge). Afterwards
///                  an optional 'After' panel explains what happened. Hit or no response = it repeats.
///   WaitForPlayer  wait until the player moves / punches / kicks / blocks / dodges (N times).
///   Event          just fires 'On Start' (music, sounds, enabling objects...).
///   Finale         the injury: slow-motion, injury animation, flash, optional comic, fade, loads the next scene.
///
/// Every step fires its 'On Start' event first.
///
/// Scene setup reminders: remove/disable RefereeController, MatchScore and the scoreboard; real MatchTimer =
/// long duration + Auto Start OFF. If a hit pauses the clock the director restarts it ('Resume After Hit').
/// </summary>
[DefaultExecutionOrder(-200)]   // runs before the player's scripts so the input filter below is applied before they read input
public class TutorialDirector : MonoBehaviour
{
    public enum StepType { Comic, StartMatch, Panel, Hint, ClearHint, Wait, EnemyApproach, EnemyReposition, EnemyAttack, WaitForPlayer, Event, Finale, EnemyCombo, WaitForMatchStart, EnemyAction, SetInput, FreePlay, PushEnemyOut, CounterLesson, StunLesson, SetEnemyPosture, ParryUntilStun, GuidedFight }
    public enum PlayerAction { Move, Punch, Kick, Block, Dodge, Swap, MovingPunch, MovingKick }
    public enum EnemyActionType { Attack, Retreat, Dash, Approach, ReactBlock, ReactDodge, ReactNone, SetKnockback }
    public enum RequiredResponse { None, Dodge, Block, DodgeOrBlock, Swap, Punch, Kick }
    public enum TeachAt { None, TrailsAppear, PerfectWindow }

    /// <summary>One teaching moment inside an EnemyCombo step: it fires on the Nth attack of the combo.</summary>
    [System.Serializable]
    public class ComboHitLesson
    {
        public string title;
        [TextArea(2, 5)] public string body;
        [Tooltip("What to press, e.g. \"Keyboard: L     Gamepad: B\".")]
        public string controls;
        public Sprite image;
        [Tooltip("The game stays frozen until the player does this (None = just press Enter / A).")]
        public RequiredResponse requireResponse = RequiredResponse.Block;
        [Tooltip("ON = do NOT freeze at the cue and do not ask for a response: the attack just lands (use for a leg kick, " +
                 "which scores nothing). When it connects the game freezes and this Title / Body is shown; Enter / A continues. " +
                 "The hit is NOT counted as a failure, so the combo is not repeated.")]
        public bool letItPlay = false;
        [Tooltip("When to freeze, in seconds BEFORE this strike would land. 0 = at the trail's red cue (about 0.2s before). " +
                 "A NORMAL block / the swap needs the strike to be 0.15 - 0.5s away, so use ~0.3 ('yellow'). " +
                 "A PERFECT block needs it under 0.15s away, so use ~0.10.")]
        public float freezeSecondsBeforeHit = 0f;
    }

    [System.Serializable]
    public class Step
    {
        public string label = "Step";
        public StepType type = StepType.Panel;

        [Header("Text (Panel / Hint / WaitForPlayer / EnemyAttack teaching)")]
        public string title;
        [TextArea(2, 6)] public string body;
        [Tooltip("What to press, e.g. \"Keyboard: L     Gamepad: B\". Shown on the panel.")]
        public string controls;
        [Tooltip("Optional picture of the controls / the move.")]
        public Sprite image;
        [Tooltip("Optional: use your own panel object instead of the shared one.")]
        public GameObject customPanel;
        [Tooltip("Panel only: freeze the game while it is open.")]
        public bool pauseGame = true;

        [Header("Timing / distance")]
        [Tooltip("Wait: how long. Panel: closes by itself after this long (0 = waits for a press). " +
                 "EnemyAttack: pause before the attack starts. EnemyReposition: slide time.")]
        public float seconds = 1f;
        [Tooltip("EnemyReposition: distance (world units) from the player to slide back to. " +
                 "WaitForPlayer/Move: how far they must walk.")]
        public float distance = 2.5f;

        [Header("Comic (Comic / Finale)")]
        public ComicCutscene comic;

        [Header("WaitForPlayer")]
        public PlayerAction action = PlayerAction.Move;
        [Tooltip("How many times they must do it (not for Move).")]
        public int times = 1;
        [Tooltip("Give up and carry on after this many seconds. 0 = wait forever.")]
        public float timeout = 0f;

        [Header("EnemyAttack - what the player must do")]
        public RequiredResponse requireResponse = RequiredResponse.None;

        [Header("EnemyAttack - teaching moment (freezes the game)")]
        public TeachAt teachAt = TeachAt.None;
        [Tooltip("Use Title / Body / Controls / Image above for the teaching panel. If Require Response is None, " +
                 "the player just presses Enter / A to continue; otherwise the game resumes when they press the " +
                 "required button.")]
        public bool teachUsesPanelText = true;
        [Tooltip("EnemyAttack: once the player has done the required response, cut the enemy's combo short so he stops " +
                 "attacking and goes back to idle (the next step can then bring him in again).")]
        public bool stopComboAfterResponse = false;

        [Header("EnemyCombo - one combo, a lesson on each attack")]
        [Tooltip("Element 0 = the 1st attack of the combo, element 1 = the 2nd, and so on. At each one the game freezes " +
                 "the moment it can be blocked (the cue turns), shows the panel, and resumes when the player does the " +
                 "required move. Attacks beyond the list just play out.")]
        public List<ComboHitLesson> hitLessons = new List<ComboHitLesson>();

        [Header("EnemyAction - tell the enemy exactly what to do")]
        [Tooltip("Attack = he attacks now (waits for play to be running).  Retreat = slides back to 'Distance' from the player over 'Seconds'.  " +
                 "Dash = a quick move AWAY from the player by 'Distance' units over 'Seconds'.  Approach = walks up to the player.  " +
                 "ReactBlock / ReactDodge = from now on he blocks / dodges EVERY incoming player attack until told otherwise.  " +
                 "ReactNone = he stops reacting.  SetKnockback = see Knockback Distance.")]
        public EnemyActionType enemyAction = EnemyActionType.Attack;
        [Tooltip("Attack / Retreat / Dash: wait for it to finish before the next step. Off = carry on straight away.")]
        public bool waitUntilDone = true;
        [Tooltip("SetKnockback: when the player lands a hit on him, he is moved so he ends up this far from the player (world units). " +
                 "It stays in force until the next SetKnockback step. -1 = the game's normal knockback.")]
        public float knockbackDistance = -1f;

        [Header("EnemyAttack / EnemyCombo - after a successful response")]
        [Tooltip("Optional panel shown after they got it right (e.g. what a perfect block does, what to do next). " +
                 "Leave all three empty for none.")]
        public string afterTitle;
        [TextArea(2, 6)] public string afterBody;
        public string afterControls;
        public Sprite afterImage;

        [Header("FreePlay / GuidedFight")]
        [Tooltip("FreePlay: ends when either fighter reaches this many points (0 = ends after 'Seconds' of play instead).")]
        public int pointsToEnd = 0;

        [Header("EnemyAttack - skip rule")]
        [Tooltip("Skip this step if the previous EnemyCombo step already taught ALL of its lessons (use it for a back-up lesson " +
                 "that is only needed when his combo was too short to reach it).")]
        public bool skipIfComboFinished = false;

        [Header("EnemyAttack - when to freeze")]
        [Tooltip("Teach At = PerfectWindow: freeze this many seconds BEFORE the strike lands. 0 = at the trail's red cue (~0.2s). " +
                 "Perfect block: ~0.10 (it must be under 0.15). Normal block / swap: ~0.3.")]
        public float freezeSecondsBeforeHit = 0f;

        [Header("EnemyCombo / CounterLesson / StunLesson / SetEnemyPosture")]
        [Tooltip("SetEnemyPosture: also empty the PLAYER's posture bar.")]
        public bool resetPlayerPosture = false;
        [Tooltip("EnemyCombo: after the player's response to the LAST lesson, cut the enemy's combo short.")]
        public bool stopComboAfterLastLesson = false;
        [Tooltip("SetEnemyPosture: the enemy's posture bar is set to this (0-1). 0.5 = half; the stun line is 0.8.")]
        [Range(0f, 1f)] public float posture = 0f;
        [Tooltip("CounterLesson: ON = the enemy is not allowed to slip the punch (the point is guaranteed). OFF = leave it to the ReactDodge / his own roll.")]
        public bool guaranteeHit = true;
        [Tooltip("CounterLesson / StunLesson: after the player's response, wait for the point + the referee's reset (ON), or just wait 'Seconds' (OFF, e.g. when the enemy dodges and no point happens).")]
        public bool waitForPoint = true;

        [Header("SetInput - what the player is ALLOWED to do from this step on (FreePlay allows everything)")]
        public bool allowMove = false;
        public bool allowPunch = false;
        public bool allowKick = false;
        public bool allowBlock = false;
        public bool allowDodge = true;
        public bool allowSwap = true;

        public UnityEvent onStart;
    }

    [Header("References")]
    [SerializeField] private EnemyStateMachine enemy;
    [Tooltip("The REAL match timer (the player's). Started by a StartMatch step.")]
    [SerializeField] private MatchTimer matchTimer;
    [Tooltip("The AttackTrails component on the enemy. Used to know the exact moment his trails appear (attack " +
             "starts) and the moment they turn red (the perfect-block window).")]
    [SerializeField] private AttackTrails attackTrails;

    [Header("Fight mode")]
    [Tooltip("ON = the tutorial happens INSIDE a real fight: the referee, MatchScore, scoreboard and the real MatchTimer all " +
             "stay and run normally (intro, Hajime, points, Yame, resets), and the enemy keeps his own AI on the REAL " +
             "timer - no 'EnemyGate' timer. The director only watches, freezes the game at the right cue and starts the " +
             "scripted attacks when a step asks. Use a WaitForMatchStart step first. OFF = the old standalone scene.")]
    [SerializeField] private bool liveFight = true;

    [Tooltip("ON = the DIRECTOR drives the enemy (walking, attacking, blocking, dodging, retreating) so every move happens exactly " +
             "when a step says. Put an empty MatchTimer ('EnemyGate', Auto Start OFF) in the ENEMY's Match Timer field so his own AI " +
             "stays off. The referee, scoring and resets still work normally. OFF = he runs his own AI.")]
    [SerializeField] private bool directorDrivesEnemy = true;

    [Header("Enemy reactions (EnemyAction: ReactBlock / ReactDodge)")]
    [SerializeField] private string enemyBlockPunchAnim = "Middle_Block";
    [SerializeField] private string enemyBlockKickAnim = "Lower_Block";
    [Tooltip("ReactDodge: how far he steps away from an incoming attack.")]
    [SerializeField] private float reactionDodgeDistance = 1.2f;
    [SerializeField] private float reactionDodgeSeconds = 0.25f;
    [Tooltip("SetKnockback: seconds after the hit before the director moves him (lets the game's own knockback finish first).")]
    [SerializeField] private float knockbackSettleDelay = 0.35f;
    [SerializeField] private float knockbackSlideSeconds = 0.2f;

    [Header("Moving punch / kick detection (WaitForPlayer)")]
    [Tooltip("Animator state names the player's moving punch / kick play (PlayerStateMachine's Moving Punch Anim / Moving Kick Anim).")]
    [SerializeField] private string movingPunchState = "Moving_Punch";
    [SerializeField] private string movingKickState = "Moving_Kick";

    [Header("Enemy behaviour")]
    [Tooltip("ON = the enemy blocks when you attack him (like the real fight). OFF = he just stands there.")]
    [SerializeField] private bool enemyBlocksPlayer = false;
    [Tooltip("He stops walking up when this close. Should be a bit bigger than his own stopping distance.")]
    [SerializeField] private float attackStartDistance = 1.4f;
    [SerializeField] private string repositionAnim = "Walk_Backward";
    [SerializeField] private string enemyIdleAnim = "Idle";

    [Header("Steps (played top to bottom)")]
    [SerializeField] private List<Step> steps = new List<Step>();
    [Tooltip("Testing only: start from this step number (0 = the first).")]
    [SerializeField] private int startAtStep = 0;

    [Header("Panel UI (shared)")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TMP_Text panelTitle;
    [SerializeField] private TMP_Text panelBody;
    [Tooltip("The line showing which buttons to press.")]
    [SerializeField] private TMP_Text panelControls;
    [SerializeField] private Image panelImage;
    [SerializeField] private TMP_Text panelPrompt;
    [SerializeField] private string promptContinue = "Press Enter / A to continue";
    [SerializeField] private string promptDoIt = "Do it now!";
    [SerializeField] private float panelFade = 0.2f;
    [SerializeField] private float panelMinShow = 0.4f;

    [Header("Hint UI (shared)")]
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private TMP_Text hintText;

    [Header("Try again")]
    [SerializeField] private string hitRetryText = "Careful! Let's try that again.";
    [SerializeField] private string missRetryText = "Not quite - try again.";
    [SerializeField] private float retryHintSeconds = 1.6f;

    [Header("If a hit pauses the clock")]
    [SerializeField] private float resumeAfterHit = 0.8f;

    [Header("Finale (the injury)")]
    [SerializeField] private string nextSceneName = "RPG";
    [Tooltip("Animator state on the player played at the injury. Falls back to 'Stun' if missing.")]
    [SerializeField] private string injuryAnim = "Injured";
    [SerializeField] private string injurySfxId;
    [SerializeField] private CanvasGroup flashOverlay;
    [SerializeField] private float finaleSlowMoScale = 0.2f;
    [SerializeField] private float finaleSlowMoSeconds = 0.8f;
    [SerializeField] private float finaleHoldSeconds = 1.5f;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeTime = 1.2f;
    [Tooltip("Health lost at the injury. Needs a HealthAndMotivation in this scene - it usually lives in the RPG " +
             "scene, so set your post-injury starting values there and leave this 0.")]
    [SerializeField] private float healthLoss = 0f;

    [Header("Default lesson plan (right-click this component > 'Fill default lesson plan')")]
    [Tooltip("Type your button names once; they are used in the panels the default lesson plan creates. " +
             "e.g. \"Keyboard: A / D     Gamepad: Left stick\". Blank = no controls line shown.")]
    [SerializeField] private string controlsMove = "";
    [SerializeField] private string controlsPunch = "";
    [SerializeField] private string controlsKick = "";
    [SerializeField] private string controlsBlock = "";
    [SerializeField] private string controlsDodge = "";
    [SerializeField] private string controlsSwap = "";

    private bool enemyDriven;
    private bool running;
    private bool matchStartedByDirector;
    private float stoppedFor;

    private void Start()
    {
        // No steps authored in the Inspector: build the built-in lesson plan automatically.
        if (steps == null || steps.Count == 0)
        {
            Debug.Log("[Tutorial] Steps list is empty - using the built-in lesson plan.");
            FillDefaultLesson();
        }
        AssignRealTimerToScripts();
        if (enemy != null && useOnlyFirstCombo) LimitToFirstCombo();
        if (enemy == null) Debug.LogWarning("[Tutorial] No enemy assigned.");
        else if (directorDrivesEnemy)
        {
            enemy.shouldApproach = false;
            enemy.shouldRetreat = false;
            enemyDriven = true;
        }   // otherwise the enemy runs his own AI on the real timer
        if (panelGroup != null) { panelGroup.alpha = 0f; panelGroup.blocksRaycasts = false; }
        if (hintGroup != null) hintGroup.alpha = 0f;
        if (flashOverlay != null) flashOverlay.alpha = 0f;
        if (fadeOverlay != null) fadeOverlay.alpha = 0f;
        foreach (Step s in steps) if (s.customPanel != null) s.customPanel.SetActive(false);

        // Knockback override: watch for the player's points (a hit landing on the enemy).
        matchScore = FindFirstObjectByType<MatchScore>();
        if (matchScore != null)
        {
            lastPlayerScore = matchScore.PlayerScore;
            matchScore.OnScoreChanged.AddListener(OnScoreChangedForKnockback);
        }
        StartCoroutine(Run());
    }

    /// <summary>PlayerSwap, OutOfBounds, HeadLookAt and FightCameraZoom pick "the first MatchTimer in the scene" when theirs is
    /// empty - which can be the empty EnemyGate timer (always stopped), so the swap, bounds, head-look and zoom all think the
    /// match is stopped. Point them at the REAL timer.</summary>
    private void AssignRealTimerToScripts()
    {
        if (matchTimer == null) { Debug.LogWarning("[Tutorial] No real Match Timer assigned on the director."); return; }
        Fix(FindFirstObjectByType<PlayerSwap>());
        Fix(FindFirstObjectByType<OutOfBounds>());
        Fix(FindFirstObjectByType<HeadLookAt>());
        Fix(FindFirstObjectByType<FightCameraZoom>());

        void Fix(Component c)
        {
            if (c == null) return;
            var f = c.GetType().GetField("matchTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (f == null) return;
            var cur = f.GetValue(c) as MatchTimer;
            if (cur != matchTimer)
            {
                f.SetValue(c, matchTimer);
                Debug.Log($"[Tutorial] {c.GetType().Name}: Match Timer was {(cur != null ? "'" + cur.name + "'" : "empty")} - set to the real timer '{matchTimer.name}'.");
            }
        }
    }

    /// <summary>After a swap lesson: if the player pressed swap but no swap started, log exactly why (PlayerSwap's own reason).</summary>
    private IEnumerator DiagnoseSwap()
    {
        float w = 0f;
        while (w < 0.4f && !PlayerSwap.Swapping) { w += Time.deltaTime; yield return null; }
        if (PlayerSwap.Swapping) yield break;
        var ps = FindFirstObjectByType<PlayerSwap>();
        if (ps == null) { Debug.LogWarning("[Tutorial] Swap pressed but there is no PlayerSwap in the scene."); yield break; }
        var m = typeof(PlayerSwap).GetMethod("CanSwapNow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                                              null, new[] { typeof(string).MakeByRefType() }, null);
        if (m == null) yield break;
        var args = new object[] { "" };
        bool ok = (bool)m.Invoke(ps, args);
        Debug.LogWarning($"[Tutorial] Swap was pressed but did not start. PlayerSwap says: {(ok ? "it could swap now - the press came too early or was eaten (check the Swap action is wired to InputHandler.onSwap)" : (string)args[0])}");
    }

    [Header("Combo choice")]
    [Tooltip("ON = the enemy only ever throws Element 0 of his Combo Animations list in this scene (the leg kick, then the punch). " +
             "Done from here by trimming his list at runtime; EnemyStateMachine itself is untouched.")]
    [SerializeField] private bool useOnlyFirstCombo = true;

    private void LimitToFirstCombo()
    {
        var f = typeof(EnemyStateMachine).GetField("comboAnimations",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        var list = f != null ? f.GetValue(enemy) as List<string> : null;
        if (list == null || list.Count == 0) { Debug.LogWarning("[Tutorial] Couldn't find the enemy's Combo Animations list to limit."); return; }
        string first = list[0];
        list.Clear();
        list.Add(first);
        Debug.Log($"[Tutorial] The enemy will only throw combo '{first}' (Element 0).");
    }

    // ------------------------------------------------------------------ input lock (SetInput steps)
    private bool inputFilterOn;
    private bool aMove, aPunch, aKick, aBlock, aDodge = true, aSwap = true;
    private bool inLesson;   // a freeze-frame lesson owns the input while it is open

    /// <summary>From now on the player can only do what is ticked. Everything else is cleared before the player's scripts read it.</summary>
    private void SetAllowedInput(bool move, bool punch, bool kick, bool block, bool dodge, bool swap)
    {
        aMove = move; aPunch = punch; aKick = kick; aBlock = block; aDodge = dodge; aSwap = swap;
        inputFilterOn = !(move && punch && kick && block && dodge && swap);
        Debug.Log($"[Tutorial] Player input: move={move} punch={punch} kick={kick} block={block} dodge={dodge} swap={swap}");
    }

    private float grantUntil;
    private RequiredResponse grantAction = RequiredResponse.None;

    private void ApplyInputFilter()
    {
        if (!inputFilterOn || inLesson) return;
        var ih = InputHandler.instance;
        if (ih == null) return;
        bool g = Time.unscaledTime < grantUntil;
        if (!aMove) { ih.move = 0f; ih.speed = 0f; }
        if (!aPunch && !(g && grantAction == RequiredResponse.Punch)) ih.punch = false;
        if (!aKick && !(g && grantAction == RequiredResponse.Kick)) ih.kick = false;
        if (!aBlock && !(g && (grantAction == RequiredResponse.Block || grantAction == RequiredResponse.DodgeOrBlock))) ih.block = false;
        if (!aDodge && !(g && (grantAction == RequiredResponse.Dodge || grantAction == RequiredResponse.DodgeOrBlock))) ih.dodge = false;
        if (!aSwap && !(g && grantAction == RequiredResponse.Swap)) ih.swap = false;
    }

    /// <summary>The fight is the player's again (for a while): input fully back, the enemy goes back to his own AI on the REAL
    /// timer, then the next step (normally the Finale) comes in after 'Seconds' of actual play.</summary>
    private IEnumerator DoFreePlay(Step s)
    {
        SetAllowedInput(true, true, true, true, true, true);
        HideHint();
        reaction = Reaction.None;
        knockbackTarget = -1f;
        yield return WaitForPlay();
        HandBackEnemy();
        if (!string.IsNullOrEmpty(s.body)) { ShowHint(s.body); }

        if (s.pointsToEnd > 0 && matchScore != null)
        {
            if (matchScore.PointsToWin > 0 && matchScore.PointsToWin <= s.pointsToEnd)
                Debug.LogWarning($"[Tutorial] MatchScore Points To Win is {matchScore.PointsToWin} - the match will END before the {s.pointsToEnd}-point mark. Raise it.");
            while (matchScore.PlayerScore < s.pointsToEnd && matchScore.EnemyScore < s.pointsToEnd) yield return null;
            Debug.Log($"[Tutorial] {s.pointsToEnd} points reached ({matchScore.PlayerScore}-{matchScore.EnemyScore}).");
        }
        else
        {
            float played = 0f;
            while (played < s.seconds)
            {
                if (matchTimer == null || !matchTimer.matchStopped) played += Time.deltaTime;
                yield return null;
            }
        }
        HideHint();
    }

    [Header("After the lessons")]
    [Tooltip("The enemy's own Aggression once he is handed back to his normal AI (his default is 1.5). Lower = calmer.")]
    [SerializeField] private float calmAggression = 0.8f;
    private bool handedBack;

    /// <summary>Gives the enemy back to his own AI on the real timer, a little calmer. Safe to call more than once.</summary>
    private void HandBackEnemy()
    {
        if (enemy == null || handedBack) return;
        handedBack = true;
        const System.Reflection.BindingFlags fl = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var f = typeof(EnemyStateMachine).GetField("matchTimer", fl);
        if (f != null && matchTimer != null) f.SetValue(enemy, matchTimer);
        else Debug.LogWarning("[Tutorial] Couldn't hand the enemy back to the real timer - he may stay passive.");
        var ag = typeof(EnemyStateMachine).GetField("aggression", fl);
        if (ag != null && calmAggression > 0f) ag.SetValue(enemy, calmAggression);
        enemyDriven = false;
        reaction = Reaction.None;
        enemy.ChangeState(EnemyStateMachine.EnemyState.Idle);
        Debug.Log($"[Tutorial] The enemy is back on his own AI (aggression {calmAggression:F1}).");
    }

    /// <summary>Approach, get perfect-blocked, approach, get perfect-blocked ... until his posture breaks and he is stunned.</summary>
    private IEnumerator DoParryUntilStun(Step s)
    {
        if (enemy == null) yield break;
        for (int i = 0; i < 8; i++)
        {
            yield return DoAttack(s);   // he walks up, attacks, the game freezes at the perfect-block moment
            if (enemy.currentState == EnemyStateMachine.EnemyState.Stun) yield break;
            yield return new WaitForSeconds(Mathf.Max(0.3f, comboCutDelay));   // room to breathe before the next one
        }
        Debug.LogWarning("[Tutorial] ParryUntilStun: he never got stunned after 8 parries.");
    }

    /// <summary>The real fight, with a guide: the enemy attacks on his own (calmer); at EVERY perfect-block moment the game freezes
    /// so the player can parry, and when he is open it freezes again so the player can punch. Ends when the player scores.</summary>
    private IEnumerator DoGuidedFight(Step s)
    {
        if (enemy == null) yield break;
        SetAllowedInput(true, true, true, true, true, true);
        HideHint();
        yield return WaitForPlay();
        HandBackEnemy();
        if (attackTrails == null) attackTrails = FindFirstObjectByType<AttackTrails>();

        int startScore = matchScore != null ? matchScore.PlayerScore : 0;
        float lead = s.freezeSecondsBeforeHit > 0f ? s.freezeSecondsBeforeHit : 0.10f;
        bool armed = true, counterTaught = false;
        float lastS = float.MaxValue, elapsed = 0f;

        while (elapsed < 240f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (matchScore != null && matchScore.PlayerScore > startScore) break;
            if (matchTimer != null && matchTimer.matchStopped) { yield return null; continue; }   // referee is doing his thing

            if (enemy.currentState == EnemyStateMachine.EnemyState.Attack)
            {
                float secs = SecondsToNextHit();
                bool haveS = secs >= 0f;
                if (!haveS || secs > lastS + 0.02f) armed = true;
                lastS = haveS ? secs : float.MaxValue;
                if (armed && haveS && CueNow(lead))
                {
                    armed = false;
                    var ts = new Step
                    {
                        title = s.title,
                        body = s.body,
                        controls = s.controls,
                        image = s.image,
                        requireResponse = RequiredResponse.Block,
                        teachUsesPanelText = true
                    };
                    yield return Teach(ts);
                    float s2 = SecondsToNextHit();
                    lastS = s2 >= 0f ? s2 : float.MaxValue;
                }
            }
            else { armed = true; lastS = float.MaxValue; }

            if (enemy.InCounterWindow)
            {
                if (!counterTaught)
                {
                    counterTaught = true;
                    var ts = new Step
                    {
                        title = s.afterTitle,
                        body = s.afterBody,
                        controls = s.afterControls,
                        image = s.afterImage,
                        requireResponse = RequiredResponse.Punch,
                        teachUsesPanelText = true
                    };
                    yield return Teach(ts);
                }
            }
            else counterTaught = false;
            yield return null;
        }
        yield return WaitForPointFlow(2.5f);
    }

    // ------------------------------------------------------------------ enemy control (EnemyAction steps)
    private enum Reaction { None, Block, Dodge }
    private Reaction reaction = Reaction.None;
    private bool reactedToCurrentAttack;
    private bool dashing;
    private float knockbackTarget = -1f;
    private MatchScore matchScore;
    private int lastPlayerScore;

    private void OnScoreChangedForKnockback(int playerScore, int enemyScore)
    {
        int gain = playerScore - lastPlayerScore;
        lastPlayerScore = playerScore;
        if (gain > 0 && knockbackTarget >= 0f && enemy != null && enemy.Player != null)
            StartCoroutine(ApplyKnockback(knockbackTarget));
    }

    private IEnumerator ApplyKnockback(float targetGap)
    {
        if (knockbackSettleDelay > 0f) yield return new WaitForSecondsRealtime(knockbackSettleDelay);
        if (enemy == null || enemy.Player == null) yield break;
        float dir = Mathf.Sign(enemy.transform.position.x - enemy.Player.position.x);
        if (dir == 0f) dir = 1f;
        Vector3 from = enemy.transform.position;
        Vector3 to = new Vector3(enemy.Player.position.x + dir * targetGap, from.y, from.z);
        float t = 0f, dur = Mathf.Max(0.02f, knockbackSlideSeconds);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            if (enemy != null) enemy.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }
        Debug.Log($"[Tutorial] Knockback override: he now stands {targetGap:F2} from the player.");
    }

    /// <summary>The enemy reacts to the player's attacks the way the last ReactBlock / ReactDodge step said.</summary>
    private void HandleReaction()
    {
        if (reaction == Reaction.None || enemy == null) return;
        var psm = PlayerStateMachine.instance;
        if (psm == null) return;
        if (psm.currentState != PlayerStateMachine.PlayerStates.Attack) { reactedToCurrentAttack = false; return; }
        if (reactedToCurrentAttack || dashing) return;
        if (enemy.currentState != EnemyStateMachine.EnemyState.Idle) return;
        if (reaction == Reaction.Block && enemy.InCounterWindow) return;   // after a perfect block he is meant to be open (a DODGE still happens)
        reactedToCurrentAttack = true;

        if (reaction == Reaction.Block)
        {
            enemy.ChangeState(EnemyStateMachine.EnemyState.Block);   // DmgDetection sees him blocking
            StartCoroutine(PlayEnemyBlockAnim(psm.currentAttackType));
        }
        else StartCoroutine(DoDash(reactionDodgeDistance, reactionDodgeSeconds));
    }

    private IEnumerator PlayEnemyBlockAnim(PlayerStateMachine.AttackTypes type)
    {
        yield return null;
        string state = type == PlayerStateMachine.AttackTypes.Kick ? enemyBlockKickAnim : enemyBlockPunchAnim;
        if (enemy != null && enemy.anim != null && !string.IsNullOrEmpty(state) && enemy.anim.HasState(0, Animator.StringToHash(state)))
            enemy.anim.Play(state, 0, 0f);
    }

    /// <summary>A quick move straight AWAY from the player (a dodge / back-step).</summary>
    private IEnumerator DoDash(float distance, float seconds)
    {
        if (enemy == null || enemy.Player == null) yield break;
        dashing = true;
        float dir = Mathf.Sign(enemy.transform.position.x - enemy.Player.position.x);
        if (dir == 0f) dir = 1f;
        Vector3 from = enemy.transform.position;
        Vector3 to = from + new Vector3(dir * distance, 0f, 0f);
        bool hasAnim = enemy.anim != null && !string.IsNullOrEmpty(repositionAnim) &&
                       enemy.anim.HasState(0, Animator.StringToHash(repositionAnim));
        if (hasAnim) enemy.anim.Play(repositionAnim);

        float t = 0f, dur = Mathf.Max(0.05f, seconds);
        while (t < dur)
        {
            if (liveFight && matchTimer != null && matchTimer.matchStopped) break;   // a point landed: GameManager takes over
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            enemy.transform.position = Vector3.Lerp(from, to, 1f - (1f - u) * (1f - u));   // ease-out: bursts away, then settles
            yield return null;
        }
        if (enemy.anim != null && !string.IsNullOrEmpty(enemyIdleAnim)) enemy.anim.Play(enemyIdleAnim);
        dashing = false;
    }


    // ------------------------------------------------------------------ timing of the freeze
    private System.Reflection.MethodInfo nextHitMethod;
    private bool nextHitSearched;

    private bool HasNextHitReflection()
    {
        if (!nextHitSearched)
        {
            nextHitSearched = true;
            if (attackTrails != null)
                nextHitMethod = typeof(AttackTrails).GetMethod("TryNextHit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (nextHitMethod == null) Debug.LogWarning("[Tutorial] Couldn't read the time to his next hit from AttackTrails - freezing at the red cue instead.");
        }
        return nextHitMethod != null;
    }

    /// <summary>Seconds until his next strike lands (from his own animation events), or -1 if there is none / unknown.</summary>
    private float SecondsToNextHit()
    {
        if (!HasNextHitReflection()) return -1f;
        var args = new object[] { 0f, 0 };
        bool ok;
        try { ok = (bool)nextHitMethod.Invoke(attackTrails, args); } catch { return -1f; }
        return ok ? (float)args[0] : -1f;
    }

    // ---- "can he swap RIGHT NOW?" - asks PlayerSwap itself, so the freeze lands exactly when a swap is possible
    private System.Reflection.MethodInfo canSwapMethod;
    private bool canSwapSearched;

    private bool HasSwapCheck()
    {
        if (!canSwapSearched)
        {
            canSwapSearched = true;
            canSwapMethod = typeof(PlayerSwap).GetMethod("CanSwapNow",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null, System.Type.EmptyTypes, null);
            if (canSwapMethod == null) Debug.LogWarning("[Tutorial] Couldn't ask PlayerSwap when a swap is possible - freezing by timing instead.");
        }
        return canSwapMethod != null && swapScript != null;
    }

    private PlayerSwap swapScript;

    private bool SwapPossibleNow()
    {
        if (swapScript == null) swapScript = FindFirstObjectByType<PlayerSwap>();
        if (!HasSwapCheck()) return false;
        try { return (bool)canSwapMethod.Invoke(swapScript, null); } catch { return false; }
    }

    /// <summary>The freeze moment for ONE strike: 'lead' seconds before it lands, or (lead 0) the trail's red cue.</summary>
    private bool CueNow(float lead)
    {
        // Perfect-block lessons (lead <= 0.15): with distance-based perfect blocks, freeze when his striking limb is close enough
        // that a block pressed right now WILL be perfect.
        var ps = PlayerStateMachine.instance;
        if (lead > 0f && lead <= 0.15f && ps != null && ps.PerfectByDistance)
        {
            float gap = ps.IncomingLimbGap();
            if (gap >= 0f) return gap <= ps.PerfectBlockDistance * 0.75f;
        }
        if (lead > 0f)
        {
            float s = SecondsToNextHit();
            if (s >= 0f) return s <= lead;
        }
        return attackTrails != null && attackTrails.PerfectWindowActive;
    }

    // ------------------------------------------------------------------ the later lessons
    private static T GetPrivate<T>(object o, string field) where T : class
    {
        var f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return f != null ? f.GetValue(o) as T : null;
    }

    /// <summary>The enemy's posture bar (found through his own reference, so no extra setup).</summary>
    private void SetEnemyPosture(float value, bool resetPlayer = false)
    {
        Posture p = enemy != null ? GetPrivate<Posture>(enemy, "enemyPosture") : null;
        if (p == null) p = FindFirstObjectByType<Posture>();
        if (p == null || p.postureSlider == null) { Debug.LogWarning("[Tutorial] SetEnemyPosture: no enemy Posture found."); return; }
        p.postureSlider.value = value;
        p.posDmg(0f);   // restarts its drain delay
        if (resetPlayer)
            foreach (var other in FindObjectsByType<Posture>(FindObjectsSortMode.None))
                if (other != p && other.postureSlider != null) { other.postureSlider.value = 0f; other.posDmg(0f); }
        Debug.Log($"[Tutorial] Enemy posture set to {value:F2}.");
    }

    /// <summary>After a point (or warning) the match stops; wait for it to stop, then for the referee's reset + Hajime.</summary>
    private IEnumerator WaitForPointFlow(float maxWaitForStop)
    {
        if (!liveFight || matchTimer == null) { yield return new WaitForSeconds(1f); yield break; }
        float w = 0f;
        while (!matchTimer.matchStopped && w < maxWaitForStop) { w += Time.deltaTime; yield return null; }
        yield return WaitForPlay();
    }

    /// <summary>Waits for the swap to finish, then makes sure the enemy ends up past the mat edge so OutOfBounds gives him
    /// his warning exactly as it normally would (the clock stops, the referee calls it, everyone resets).</summary>
    private IEnumerator DoPushEnemyOut(Step s)
    {
        if (enemy == null) yield break;
        float g = 0f;
        while (PlayerSwap.Swapping && g < 4f) { g += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.15f);

        var oob = FindFirstObjectByType<OutOfBounds>();
        Transform le = oob != null ? GetPrivate<Transform>(oob, "leftEdge") : null;
        Transform re = oob != null ? GetPrivate<Transform>(oob, "rightEdge") : null;
        if (le == null || re == null)
        {
            Debug.LogWarning("[Tutorial] PushEnemyOut: no OutOfBounds with Left / Right Edge assigned - can't push him out.");
            yield break;
        }
        float lo = Mathf.Min(le.position.x, re.position.x), hi = Mathf.Max(le.position.x, re.position.x);
        float margin = 0.1f;
        var mf = typeof(OutOfBounds).GetField("margin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (mf != null && oob != null) margin = (float)mf.GetValue(oob);

        float centre = (lo + hi) * 0.5f;
        float x = enemy.transform.position.x;
        float dir = x >= centre ? 1f : -1f;
        float target = dir > 0f ? hi + margin + Mathf.Max(0.2f, s.distance) : lo - margin - Mathf.Max(0.2f, s.distance);

        bool alreadyOut = dir > 0f ? x > hi + margin : x < lo - margin;
        if (!alreadyOut)
        {
            Vector3 from = enemy.transform.position;
            Vector3 to = new Vector3(target, from.y, from.z);
            float t = 0f, dur = Mathf.Max(0.05f, s.seconds);
            while (t < dur)
            {
                t += Time.deltaTime;
                enemy.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }
        }
        Debug.Log("[Tutorial] Enemy is past the edge - OutOfBounds should now call the warning.");
        yield return WaitForPointFlow(1.5f);
    }

    /// <summary>After a perfect block: the instant the enemy is open (his counter window) the game freezes and tells the player
    /// what to do (normally: step forward and punch). Then it waits for the point and the reset.</summary>
    private IEnumerator DoCounterLesson(Step s)
    {
        if (enemy == null) yield break;
        float timeout = 3f, w = 0f;
        while (!enemy.InCounterWindow && w < timeout) { w += Time.deltaTime; yield return null; }
        if (!enemy.InCounterWindow) { Debug.LogWarning("[Tutorial] CounterLesson: his counter window never opened - skipping."); yield break; }

        if (s.guaranteeHit && reaction != Reaction.Dodge)
        {
            // The punch must connect: switch off his random "slip the punch" roll for this window.
            var f = typeof(EnemyStateMachine).GetField("counterDodgeRolled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null) f.SetValue(enemy, false);
        }
        var ts = new Step
        {
            title = s.title,
            body = s.body,
            controls = s.controls,
            image = s.image,
            requireResponse = s.requireResponse,
            teachUsesPanelText = true
        };
        yield return Teach(ts);

        if (s.waitForPoint) yield return WaitForPointFlow(2.5f);
        else yield return new WaitForSeconds(Mathf.Max(0.2f, s.seconds));
    }

    /// <summary>The moment the enemy's posture breaks and he is stunned, the game freezes and explains it; the player must kick.</summary>
    private IEnumerator DoStunLesson(Step s)
    {
        if (enemy == null) yield break;
        float w = 0f;
        while (enemy.currentState != EnemyStateMachine.EnemyState.Stun && w < 3f) { w += Time.deltaTime; yield return null; }
        if (enemy.currentState != EnemyStateMachine.EnemyState.Stun) { Debug.LogWarning("[Tutorial] StunLesson: he never got stunned - skipping."); yield break; }

        var ts = new Step
        {
            title = s.title,
            body = s.body,
            controls = s.controls,
            image = s.image,
            requireResponse = s.requireResponse,
            teachUsesPanelText = true
        };
        yield return Teach(ts);

        if (s.waitForPoint) yield return WaitForPointFlow(2.5f);
        else yield return new WaitForSeconds(Mathf.Max(0.2f, s.seconds));
    }

    private IEnumerator DoEnemyAction(Step s)
    {
        if (enemy == null) yield break;
        switch (s.enemyAction)
        {
            case EnemyActionType.Attack:
                {
                    yield return WaitForPlay();
                    while (!EnemyFree) yield return null;
                    if (s.seconds > 0f) yield return new WaitForSeconds(s.seconds);
                    yield return WaitForPlay();
                    enemy.shouldApproach = false;
                    enemy.ChangeState(EnemyStateMachine.EnemyState.Attack);
                    yield return null;
                    if (s.waitUntilDone) while (enemy.currentState == EnemyStateMachine.EnemyState.Attack) yield return null;
                    break;
                }
            case EnemyActionType.Retreat:
                if (s.waitUntilDone) yield return DoReposition(s); else StartCoroutine(DoReposition(s));
                break;
            case EnemyActionType.Dash:
                if (s.waitUntilDone) yield return DoDash(s.distance, s.seconds); else StartCoroutine(DoDash(s.distance, s.seconds));
                break;
            case EnemyActionType.Approach: yield return DoApproach(); break;
            case EnemyActionType.ReactBlock: reaction = Reaction.Block; reactedToCurrentAttack = false; break;
            case EnemyActionType.ReactDodge: reaction = Reaction.Dodge; reactedToCurrentAttack = false; break;
            case EnemyActionType.ReactNone: reaction = Reaction.None; break;
            case EnemyActionType.SetKnockback: knockbackTarget = s.knockbackDistance; break;
        }
    }

    private void Update()
    {
        ApplyInputFilter();
        // The player's clock: a hit stops it; restart it so the tutorial never gets stuck.
        if (running && matchTimer != null && !matchTimer.matchOver && matchStartedByDirector &&
            matchTimer.matchStopped && Time.timeScale > 0f)
        {
            stoppedFor += Time.unscaledDeltaTime;
            if (stoppedFor >= resumeAfterHit) { matchTimer.StartTimer(); stoppedFor = 0f; }
        }
        else stoppedFor = 0f;

        if (enemyDriven && enemy != null && Time.timeScale > 0f) DriveEnemy();
    }

    /// <summary>The enemy's own Update is disabled by the gate timer, so we run the parts of it we want.</summary>
    private void DriveEnemy()
    {
        // Referee has the match stopped (intro, point, Yame): the enemy just stands there.
        if (liveFight && matchTimer != null && matchTimer.matchStopped) return;
        HandleReaction();
        if (enemy.Player != null)
        {
            Vector3 dir = enemy.Player.position - enemy.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(dir), 50f * Time.deltaTime);
        }
        // Idle = standing still, unless we want him to react (block) to the player's attacks.
        if (enemy.currentState != EnemyStateMachine.EnemyState.Idle || enemyBlocksPlayer)
            enemy.HandleStates();
        enemy.HandleMovement();
    }

    private bool EnemyFree => enemy == null || enemy.currentState == EnemyStateMachine.EnemyState.Idle;
    private float EnemyDistance => enemy != null && enemy.Player != null ? Vector3.Distance(enemy.transform.position, enemy.Player.position) : 0f;

    // ------------------------------------------------------------------ main loop
    private IEnumerator Run()
    {
        running = true;
        for (int i = Mathf.Max(0, startAtStep); i < steps.Count; i++)
        {
            Step s = steps[i];
            Debug.Log($"[Tutorial] Step {i}: {s.label} ({s.type})");
            s.onStart?.Invoke();
            switch (s.type)
            {
                case StepType.Comic: yield return DoComic(s.comic); break;
                case StepType.StartMatch: StartMatch(); break;
                case StepType.Panel:
                    HideHint();
                    yield return ShowPanel(s.title, s.body, s.controls, s.image, s.customPanel, s.pauseGame, s.seconds);
                    break;
                case StepType.Hint: ShowHint(s.body); break;
                case StepType.ClearHint: HideHint(); break;
                case StepType.Wait: yield return new WaitForSeconds(s.seconds); break;
                case StepType.EnemyApproach: yield return DoApproach(); break;
                case StepType.EnemyReposition: yield return DoReposition(s); break;
                case StepType.EnemyAttack:
                    if (s.skipIfComboFinished && lastComboTaughtAll) { Debug.Log("[Tutorial] Skipping - the combo already taught every lesson."); break; }
                    yield return DoAttack(s); break;
                case StepType.EnemyCombo: yield return DoCombo(s); break;
                case StepType.EnemyAction: yield return DoEnemyAction(s); break;
                case StepType.SetInput: SetAllowedInput(s.allowMove, s.allowPunch, s.allowKick, s.allowBlock, s.allowDodge, s.allowSwap); break;
                case StepType.FreePlay: yield return DoFreePlay(s); break;
                case StepType.ParryUntilStun: yield return DoParryUntilStun(s); break;
                case StepType.GuidedFight: yield return DoGuidedFight(s); break;
                case StepType.PushEnemyOut: yield return DoPushEnemyOut(s); break;
                case StepType.CounterLesson: yield return DoCounterLesson(s); break;
                case StepType.StunLesson: yield return DoStunLesson(s); break;
                case StepType.SetEnemyPosture: SetEnemyPosture(s.posture, s.resetPlayerPosture); break;
                case StepType.WaitForMatchStart: yield return DoWaitForMatchStart(); break;
                case StepType.WaitForPlayer: yield return DoWaitForPlayer(s); break;
                case StepType.Event: break;
                case StepType.Finale: yield return DoFinale(s); yield break;
            }
        }
        running = false;
        Debug.Log("[Tutorial] Steps finished (no Finale step?).");
    }

    /// <summary>Live fight: waits for the referee's intro to finish and Hajime to start the real clock.</summary>
    private IEnumerator DoWaitForMatchStart()
    {
        if (matchTimer == null) { Debug.LogWarning("[Tutorial] WaitForMatchStart: no MatchTimer assigned."); yield break; }
        while (matchTimer.matchStopped) yield return null;
        Debug.Log("[Tutorial] The fight has started.");
    }

    /// <summary>Live fight: if a point / warning has stopped the match, wait until play resumes (Yame, reset, Hajime).</summary>
    private IEnumerator WaitForPlay()
    {
        if (!liveFight || matchTimer == null) yield break;
        while (matchTimer.matchStopped && !matchTimer.matchOver) yield return null;
    }

    private void StartMatch()
    {
        if (matchTimer == null) { Debug.LogWarning("[Tutorial] No MatchTimer assigned - can't start the match."); return; }
        matchTimer.StartTimer();
        matchStartedByDirector = true;
    }

    private IEnumerator DoComic(ComicCutscene comic)
    {
        if (comic == null) { Debug.LogWarning("[Tutorial] Comic step has no ComicCutscene."); yield break; }
        bool done = false;
        void OnDone() => done = true;
        comic.OnCutsceneComplete += OnDone;
        comic.Play();
        while (!done) yield return null;
        comic.OnCutsceneComplete -= OnDone;
    }

    // ------------------------------------------------------------------ panels
    private void FillPanel(string title, string body, string controls, Sprite image, string prompt)
    {
        if (panelTitle != null) panelTitle.text = title;
        if (panelBody != null) panelBody.text = body;
        if (panelControls != null) { panelControls.text = controls; panelControls.gameObject.SetActive(!string.IsNullOrEmpty(controls)); }
        if (panelImage != null) { panelImage.sprite = image; panelImage.enabled = image != null; }
        if (panelPrompt != null) panelPrompt.text = prompt;
    }

    /// <summary>Shows a panel. seconds &gt; 0 closes it by itself; otherwise waits for Enter / A.</summary>
    private IEnumerator ShowPanel(string title, string body, string controls, Sprite image, GameObject custom, bool pause, float seconds)
    {
        InputHandler.instance?.SetInput(false);
        float prevScale = Time.timeScale;
        if (pause) Time.timeScale = 0f;

        if (custom != null) custom.SetActive(true);
        else
        {
            FillPanel(title, body, controls, image, seconds > 0f ? "" : promptContinue);
            yield return FadeGroup(panelGroup, 1f, panelFade);
        }

        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            if (seconds > 0f) { if (t >= seconds) break; }
            else if (t >= panelMinShow && SkipInput.Pressed()) break;
            yield return null;
        }

        if (custom != null) custom.SetActive(false);
        else yield return FadeGroup(panelGroup, 0f, panelFade);

        Time.timeScale = pause ? (prevScale > 0f ? prevScale : 1f) : prevScale;
        InputHandler.instance?.SetInput(true);
        yield return null;
    }

    private void ShowHint(string text)
    {
        if (hintText != null) hintText.text = text;
        if (hintGroup != null) hintGroup.alpha = 1f;
    }

    private void HideHint()
    {
        if (hintGroup != null) hintGroup.alpha = 0f;
    }

    // ------------------------------------------------------------------ enemy steps
    private IEnumerator DoApproach()
    {
        if (enemy == null) yield break;
        float guard = 0f;
        enemy.shouldApproach = true;
        while (EnemyDistance > attackStartDistance && guard < 8f)
        {
            guard += Time.deltaTime;
            yield return null;
        }
        enemy.shouldApproach = false;

        // Still too far for the strike to connect (he stalled, or his walk stops short)? Glide him in the rest of the way.
        if (enemy.Player != null && EnemyDistance > attackStartDistance + 0.05f)
        {
            float dir = Mathf.Sign(enemy.transform.position.x - enemy.Player.position.x);
            if (dir == 0f) dir = 1f;
            Vector3 from = enemy.transform.position;
            Vector3 to = new Vector3(enemy.Player.position.x + dir * attackStartDistance, from.y, from.z);
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                enemy.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.25f));
                yield return null;
            }
        }
    }

    private IEnumerator DoReposition(Step s)
    {
        if (enemy == null || enemy.Player == null) yield break;
        while (!EnemyFree) yield return null;
        enemy.shouldApproach = false;

        float dir = -Mathf.Sign(enemy.Player.position.x - enemy.transform.position.x);
        if (dir == 0f) dir = 1f;
        Vector3 from = enemy.transform.position;
        Vector3 to = new Vector3(enemy.Player.position.x + dir * s.distance, from.y, from.z);

        bool hasAnim = enemy.anim != null && !string.IsNullOrEmpty(repositionAnim) &&
                       enemy.anim.HasState(0, Animator.StringToHash(repositionAnim));
        if (hasAnim) enemy.anim.Play(repositionAnim);

        float t = 0f, dur = Mathf.Max(0.05f, s.seconds);
        while (t < dur)
        {
            t += Time.deltaTime;
            enemy.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }
        if (enemy.anim != null && !string.IsNullOrEmpty(enemyIdleAnim)) enemy.anim.Play(enemyIdleAnim);
    }

    private IEnumerator DoAttack(Step s)
    {
        if (enemy == null || PlayerStateMachine.instance == null) yield break;
        var psm = PlayerStateMachine.instance;

        if (attackTrails == null) attackTrails = FindFirstObjectByType<AttackTrails>();
        if (s.teachAt != TeachAt.None && attackTrails == null)
            Debug.LogWarning("[Tutorial] EnemyAttack wants to teach at the trails but no Attack Trails is assigned - it will teach when the attack starts instead.");

        for (int attempt = 0; attempt < 12; attempt++)
        {
            yield return WaitForPlay();
            while (!EnemyFree) yield return null;
            yield return DoApproach();

            if (s.seconds > 0f) yield return new WaitForSeconds(s.seconds);

            yield return WaitForPlay();
            while (psm.currentState == PlayerStateMachine.PlayerStates.Hurt || psm.IsStunned) yield return null;

            enemy.shouldApproach = false;
            enemy.ChangeState(EnemyStateMachine.EnemyState.Attack);
            yield return null;

            bool hit = false, responded = false, taught = s.teachAt == TeachAt.None;
            if (!taught && attackTrails == null) { taught = true; yield return Teach(s); responded = s.requireResponse != RequiredResponse.None; }

            while (enemy.currentState == EnemyStateMachine.EnemyState.Attack)
            {
                if (!taught)
                {
                    // The trails emit exactly while the enemy is in his Attack state, so that is the moment they appear.
                    // (Read from his state, so this works with any version of AttackTrails.)
                    bool appears = enemy.currentState == EnemyStateMachine.EnemyState.Attack;
                    if (swapScript == null) swapScript = FindFirstObjectByType<PlayerSwap>();
                    bool window = (s.requireResponse == RequiredResponse.Swap && HasSwapCheck()) ? SwapPossibleNow()
                                                                                                   : CueNow(s.freezeSecondsBeforeHit);
                    if ((s.teachAt == TeachAt.TrailsAppear && appears) || (s.teachAt == TeachAt.PerfectWindow && window))
                    {
                        taught = true;
                        yield return Teach(s);
                        if (s.requireResponse == RequiredResponse.Swap) StartCoroutine(DiagnoseSwap());
                        if (s.requireResponse != RequiredResponse.None) responded = true;
                        if (s.stopComboAfterResponse && s.requireResponse != RequiredResponse.None)
                        {
                            yield return new WaitForSeconds(comboCutDelay);   // room to breathe: let the dodge / block play out first
                            enemy.StopCombo();
                            if (enemy.currentState == EnemyStateMachine.EnemyState.Attack)
                                enemy.ChangeState(EnemyStateMachine.EnemyState.Idle);
                        }
                    }
                }
                var st = psm.currentState;
                if (st == PlayerStateMachine.PlayerStates.Hurt || st == PlayerStateMachine.PlayerStates.Stun) hit = true;
                if (s.requireResponse == RequiredResponse.Swap)
                {
                    if (PlayerSwap.Swapping) responded = true;   // only the real swap counts for a swap lesson
                }
                else
                {
                    if (st == PlayerStateMachine.PlayerStates.Dodge && s.requireResponse != RequiredResponse.Block) responded = true;
                    if (st == PlayerStateMachine.PlayerStates.Block && s.requireResponse != RequiredResponse.Dodge) responded = true;
                }
                yield return null;
            }

            bool needResponse = s.requireResponse != RequiredResponse.None;
            if (!hit && (!needResponse || responded))
            {
                if (!string.IsNullOrEmpty(s.afterTitle) || !string.IsNullOrEmpty(s.afterBody))
                {
                    yield return new WaitForSeconds(0.35f);
                    yield return ShowPanel(s.afterTitle, s.afterBody, s.afterControls, s.afterImage, null, true, 0f);
                }
                yield break;
            }

            Debug.Log(hit ? "[Tutorial] Player was hit - repeating the attack." : "[Tutorial] No response - repeating the attack.");
            ShowHint(hit ? hitRetryText : missRetryText);
            yield return new WaitForSecondsRealtime(retryHintSeconds);
            HideHint();
        }
        Debug.LogWarning("[Tutorial] EnemyAttack: gave up after 12 tries - moving on.");
    }

    /// <summary>One combo, with a freeze-frame lesson on each attack: the game stops the moment the attack can be
    /// blocked (the trails' perfect-window cue), shows what to press, and carries on when the player does it.</summary>
    [Header("Pacing")]
    [Tooltip("After the player answers a lesson, how long his combo keeps playing before it is cut short (only when a step asks to cut it).")]
    [SerializeField] private float comboCutDelay = 0.55f;
    [Tooltip("When a freeze ends, time eases back up from 'Resume Start Scale' to normal over this many real seconds (0 = snaps back).")]
    [SerializeField] private float resumeEaseSeconds = 0.35f;
    [Range(0.05f, 1f)][SerializeField] private float resumeStartScale = 0.35f;
    private int easeId;
    private bool easing;
    private float easeTarget = 1f;

    private IEnumerator EaseTimeScale(float target, int id)
    {
        easing = true; easeTarget = target;
        float t = 0f, dur = Mathf.Max(0.01f, resumeEaseSeconds);
        while (t < dur && id == easeId)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            Time.timeScale = Mathf.Lerp(resumeStartScale, target, u);
            yield return null;
        }
        if (id == easeId) { Time.timeScale = target; easing = false; }
    }

    private bool lastComboTaughtAll;
    private float ignoreHurtUntil;   // a "let it play" leg kick must not count as the player failing

    private IEnumerator DoCombo(Step s)
    {
        if (enemy == null || PlayerStateMachine.instance == null) yield break;
        var psm = PlayerStateMachine.instance;
        if (attackTrails == null) attackTrails = FindFirstObjectByType<AttackTrails>();
        if (attackTrails == null)
            Debug.LogWarning("[Tutorial] EnemyCombo: no Attack Trails found - there is no cue to freeze on, so no lessons will show.");

        for (int attempt = 0; attempt < 12; attempt++)
        {
            yield return WaitForPlay();
            while (!EnemyFree) yield return null;
            yield return DoApproach();
            if (s.seconds > 0f) yield return new WaitForSeconds(s.seconds);
            yield return WaitForPlay();
            while (psm.currentState == PlayerStateMachine.PlayerStates.Hurt || psm.IsStunned) yield return null;

            enemy.shouldApproach = false;
            enemy.ChangeState(EnemyStateMachine.EnemyState.Attack);
            yield return null;

            int hitIndex = 0;
            bool wasCue = false, hit = false, armed = true;
            float lastS = float.MaxValue;
            bool timed = HasNextHitReflection();
            while (enemy.currentState == EnemyStateMachine.EnemyState.Attack)
            {
                bool red = attackTrails != null && attackTrails.PerfectWindowActive;
                float secs = SecondsToNextHit();
                bool haveS = secs >= 0f;
                // 'armed' = a NEW strike is coming (so one strike can't trigger two lessons).
                if (timed) { if (!haveS || secs > lastS + 0.02f) armed = true; lastS = haveS ? secs : float.MaxValue; }
                else if (!red) armed = true;
                float lead = hitIndex < s.hitLessons.Count ? s.hitLessons[hitIndex].freezeSecondsBeforeHit : 0f;
                bool swapLesson = hitIndex < s.hitLessons.Count && !s.hitLessons[hitIndex].letItPlay &&
                                  s.hitLessons[hitIndex].requireResponse == RequiredResponse.Swap;
                if (swapScript == null) swapScript = FindFirstObjectByType<PlayerSwap>();
                bool cue = armed && ((swapLesson && HasSwapCheck()) ? SwapPossibleNow()
                                    : ((lead > 0f && haveS) ? CueNow(lead) : (red && !wasCue)));
                if (cue)   // the next strike of the combo has reached its freeze moment
                {
                    armed = false;
                    if (hitIndex < s.hitLessons.Count)
                    {
                        var l = s.hitLessons[hitIndex];
                        Debug.Log($"[Tutorial] Combo attack {hitIndex + 1}: teaching '{l.title}' ({(l.letItPlay ? "let it play" : l.requireResponse.ToString())}).");
                        var ts = new Step
                        {
                            title = l.title,
                            body = l.body,
                            controls = l.controls,
                            image = l.image,
                            requireResponse = l.requireResponse,
                            teachUsesPanelText = true
                        };
                        if (l.letItPlay)
                        {
                            // No freeze now: the hit lands (a leg kick scores nothing), THEN we explain it.
                            ts.requireResponse = RequiredResponse.None;
                            ignoreHurtUntil = Time.time + 2f;
                            float w = 0f;
                            while (w < 0.7f && psm.currentState != PlayerStateMachine.PlayerStates.Hurt)
                            { w += Time.deltaTime; yield return null; }
                            yield return Teach(ts);   // freezes the instant the kick connects - the punch behind it is held in mid-air
                            ignoreHurtUntil = Time.time + 0.8f;
                            // The leg-hit flinch must not stop him swapping the punch that follows straight away.
                            if (psm.currentState == PlayerStateMachine.PlayerStates.Hurt && !psm.IsStunned)
                                psm.ChangeState(PlayerStateMachine.PlayerStates.Idle);
                            // The kick has landed, so whatever is coming next is a NEW strike: arm the next lesson at once.
                            armed = true;
                            lastS = float.MaxValue;
                        }
                        else
                        {
                            yield return Teach(ts);
                            if (ts.requireResponse == RequiredResponse.Swap) StartCoroutine(DiagnoseSwap());
                            if (s.stopComboAfterLastLesson && hitIndex == s.hitLessons.Count - 1)
                            {
                                yield return new WaitForSeconds(comboCutDelay);   // room to breathe: let the block play out first
                                enemy.StopCombo();
                                if (enemy.currentState == EnemyStateMachine.EnemyState.Attack)
                                    enemy.ChangeState(EnemyStateMachine.EnemyState.Idle);
                            }
                        }
                    }
                    hitIndex++;
                    bool wasLetItPlay = hitIndex - 1 < s.hitLessons.Count && s.hitLessons[hitIndex - 1].letItPlay;
                    if (!wasLetItPlay)
                    {
                        float s2 = SecondsToNextHit();
                        lastS = s2 >= 0f ? s2 : float.MaxValue;
                    }
                    red = attackTrails != null && attackTrails.PerfectWindowActive;
                    if (wasLetItPlay) wasCue = red;
                }
                wasCue = red;

                var st = psm.currentState;
                if ((st == PlayerStateMachine.PlayerStates.Hurt || st == PlayerStateMachine.PlayerStates.Stun) && Time.time > ignoreHurtUntil) hit = true;
                yield return null;
            }

            if (!hit)
            {
                lastComboTaughtAll = hitIndex >= s.hitLessons.Count;
                if (!string.IsNullOrEmpty(s.afterTitle) || !string.IsNullOrEmpty(s.afterBody))
                {
                    yield return new WaitForSeconds(0.35f);
                    yield return ShowPanel(s.afterTitle, s.afterBody, s.afterControls, s.afterImage, null, true, 0f);
                }
                yield break;
            }

            Debug.Log("[Tutorial] Player was hit during the combo - repeating it.");
            ShowHint(hitRetryText);
            yield return new WaitForSecondsRealtime(retryHintSeconds);
            HideHint();
        }
        Debug.LogWarning("[Tutorial] EnemyCombo: gave up after 12 tries - moving on.");
    }

    /// <summary>The freeze-frame lesson: stops everything, shows the panel, waits for the right button.</summary>
    private IEnumerator Teach(Step s)
    {
        bool wasDriven = enemyDriven;   // false in a live fight: the enemy is never driven by the director
        enemyDriven = false;
        float prevScale = easing ? easeTarget : Time.timeScale;
        easeId++; easing = false;   // cancels any ease still running
        Time.timeScale = 0f;

        bool needsPress = s.requireResponse != RequiredResponse.None;
        inLesson = true;
        if (!needsPress) InputHandler.instance?.SetInput(false);   // closing the panel with A must not also punch
        if (s.teachUsesPanelText)
        {
            FillPanel(s.title, s.body, s.controls, s.image, needsPress ? promptDoIt : promptContinue);
            yield return FadeGroup(panelGroup, 1f, panelFade);
        }

        float t = 0f;
        var ih = InputHandler.instance;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            if (needsPress && ih != null)
            {
                // Only the asked-for button counts; stray presses are cleared so they can't fire after the freeze.
                bool wantBlock = s.requireResponse == RequiredResponse.Block || s.requireResponse == RequiredResponse.DodgeOrBlock;
                bool wantDodge = s.requireResponse == RequiredResponse.Dodge || s.requireResponse == RequiredResponse.DodgeOrBlock;
                bool wantSwap = s.requireResponse == RequiredResponse.Swap;
                bool wantPunch = s.requireResponse == RequiredResponse.Punch;
                bool wantKick = s.requireResponse == RequiredResponse.Kick;
                if ((wantBlock && ih.block) || (wantDodge && ih.dodge) || (wantSwap && ih.swap) ||
                    (wantPunch && ih.punch) || (wantKick && ih.kick)) break;
                if (!wantPunch) ih.punch = false;
                if (!wantKick) ih.kick = false;
                if (!wantBlock) ih.block = false;
                if (!wantDodge) ih.dodge = false;
            }
            else if (!needsPress && t >= panelMinShow && SkipInput.Pressed()) break;
            yield return null;
        }

        if (s.teachUsesPanelText) yield return FadeGroup(panelGroup, 0f, panelFade * 0.5f);
        {
            float target = prevScale > 0f ? prevScale : 1f;
            if (resumeEaseSeconds > 0f && target >= 0.99f)
            {
                Time.timeScale = resumeStartScale;
                StartCoroutine(EaseTimeScale(target, ++easeId));
            }
            else Time.timeScale = target;
        }
        enemyDriven = wasDriven;
        if (!needsPress)
        {
            yield return null;
            InputHandler.instance?.SetInput(true);
        }
        else
        {
            // The button that ended the freeze must still reach the player even if the lock would normally block it.
            grantUntil = Time.unscaledTime + 0.8f;
            grantAction = s.requireResponse;
        }
        inLesson = false;
    }

    // ------------------------------------------------------------------ wait for the player
    private IEnumerator DoWaitForPlayer(Step s)
    {
        if (PlayerStateMachine.instance == null) yield break;
        Transform pt = PlayerStateMachine.instance.player != null ? PlayerStateMachine.instance.player.transform : null;
        string label = s.body;
        int count = 0;
        float travelled = 0f, elapsed = 0f;
        Vector3 last = pt != null ? pt.position : Vector3.zero;
        bool wasActive = false;

        while (true)
        {
            elapsed += Time.deltaTime;
            var ps = PlayerStateMachine.instance;
            bool active = false;
            switch (s.action)
            {
                case PlayerAction.Move:
                    if (pt != null) { travelled += Mathf.Abs(pt.position.x - last.x); last = pt.position; }
                    if (travelled >= s.distance) goto done;
                    break;
                case PlayerAction.Punch: active = ps.currentState == PlayerStateMachine.PlayerStates.Attack && ps.currentAttackType == PlayerStateMachine.AttackTypes.Punch; break;
                case PlayerAction.Kick: active = ps.currentState == PlayerStateMachine.PlayerStates.Attack && ps.currentAttackType == PlayerStateMachine.AttackTypes.Kick; break;
                case PlayerAction.Block: active = ps.currentState == PlayerStateMachine.PlayerStates.Block; break;
                case PlayerAction.Dodge: active = ps.currentState == PlayerStateMachine.PlayerStates.Dodge; break;
                case PlayerAction.Swap: active = PlayerSwap.Swapping; break;
                case PlayerAction.MovingPunch:
                    active = ps.currentState == PlayerStateMachine.PlayerStates.Attack && ps.animator != null &&
                             ps.animator.GetCurrentAnimatorStateInfo(0).IsName(movingPunchState); break;
                case PlayerAction.MovingKick:
                    active = ps.currentState == PlayerStateMachine.PlayerStates.Attack && ps.animator != null &&
                             ps.animator.GetCurrentAnimatorStateInfo(0).IsName(movingKickState); break;
            }
            if (s.action != PlayerAction.Move && active && !wasActive)
            {
                count++;
                if (count >= Mathf.Max(1, s.times)) goto done;
            }
            wasActive = active;

            string progress = (s.action != PlayerAction.Move && s.times > 1) ? $"  ({count}/{s.times})" : "";
            string controls = string.IsNullOrEmpty(s.controls) ? "" : $"\n{s.controls}";
            ShowHint(label + progress + controls);
            if (s.timeout > 0f && elapsed >= s.timeout) { Debug.Log("[Tutorial] WaitForPlayer timed out - moving on."); goto done; }
            yield return null;
        }
    done:
        yield return new WaitForSeconds(0.4f);
        HideHint();
    }

    // ------------------------------------------------------------------ finale
    private IEnumerator DoFinale(Step s)
    {
        HideHint();
        SetAllowedInput(false, false, false, false, false, false);
        Debug.Log("[Tutorial] FINALE - the injury.");

        enemyDriven = false;
        InputHandler.instance?.SetInput(false);
        var psm = PlayerStateMachine.instance;
        Animator anim = psm != null ? psm.animator : null;
        if (psm != null) { if (psm.player != null) psm.player.enabled = false; psm.enabled = false; }
        if (matchTimer != null) matchTimer.EndMatch();

        if (enemy != null) enemy.StopForRoundReset();   // drop any attack / block he was in
        if (anim != null)
        {
            bool has = anim.HasState(0, Animator.StringToHash(injuryAnim));
            string clip = has ? injuryAnim : "Stun";
            if (!has) Debug.LogWarning($"[Tutorial] No player animator state '{injuryAnim}' - using '{clip}'.");
            anim.Play(clip, 0, 0f);   // his scripts are switched off above, so nothing overwrites the pose
        }
        if (!string.IsNullOrEmpty(injurySfxId)) Sfx.Play(injurySfxId);

        if (flashOverlay != null) { flashOverlay.alpha = 1f; StartCoroutine(FadeGroup(flashOverlay, 0f, 0.5f)); }

        Time.timeScale = finaleSlowMoScale;
        yield return new WaitForSecondsRealtime(finaleSlowMoSeconds);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(finaleHoldSeconds);

        if (s.comic != null) yield return DoComic(s.comic);

        yield return FadeGroup(fadeOverlay, 1f, fadeTime);

        if (healthLoss != 0f && HealthAndMotivation.instance != null)
            HealthAndMotivation.instance.ModifyHealth(-Mathf.Abs(healthLoss));

        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[Tutorial] Scene '{nextSceneName}' can't be loaded - spelled exactly right and in Build Profiles?");
            yield break;
        }
        SceneManager.LoadScene(nextSceneName);
    }

    // ------------------------------------------------------------------ default lesson plan
    /// <summary>Replaces the Steps list with a complete beginner lesson: move, punch, kick, read the trails, block, perfect
    /// block (and the counter), dodge, swap, then the injury finale. (The tutorial scene has no injury meter.)
    /// Edit any text afterwards.</summary>
    [ContextMenu("Fill default lesson plan")]
    private void FillDefaultLesson()
    {
        var list = new List<Step>();
        Step Add(string label, StepType type, string title = "", string body = "", string controls = "")
        {
            var s = new Step { label = label, type = type, title = title, body = body, controls = controls };
            list.Add(s);
            return s;
        }

        // The tutorial is part of a real, in-story fight: the referee runs the intro and Hajime, then we begin.
        if (liveFight) Add("Wait for Hajime", StepType.WaitForMatchStart);
        else Add("Start the clock", StepType.StartMatch);

        // Input lock: use SetInput steps to say what the player may do from that point on.
        Step Allow(string label, bool move, bool punch, bool kick, bool block, bool dodge = true, bool swap = true)
        {
            var st = Add(label, StepType.SetInput);
            st.allowMove = move; st.allowPunch = punch; st.allowKick = kick; st.allowBlock = block;
            st.allowDodge = dodge; st.allowSwap = swap;
            return st;
        }
        Allow("Input: dodge + swap only", false, false, false, false);

        Add("Opening", StepType.Panel, "The final",
            "You know what to do. Find your range, and watch his hands and feet.");

        // ================= DODGE (x2): the attack plays, the game freezes the instant the trail turns red
        var d1 = Add("Dodge 1", StepType.EnemyAttack, "Dodge",
            "The trail has turned red - that is the moment to react. Dodge now.", controlsDodge);
        d1.stopComboAfterResponse = true; d1.teachAt = TeachAt.PerfectWindow; d1.requireResponse = RequiredResponse.Dodge; d1.seconds = 0.8f;
        Add("He closes in", StepType.EnemyAction).enemyAction = EnemyActionType.Approach;
        var d2 = Add("Dodge 2", StepType.EnemyAttack, "Dodge again",
            "Red again. Dodge.", controlsDodge);
        d2.stopComboAfterResponse = true; d2.teachAt = TeachAt.PerfectWindow; d2.requireResponse = RequiredResponse.Dodge; d2.seconds = 0.5f;
        Add("He closes in again", StepType.EnemyAction).enemyAction = EnemyActionType.Approach;

        // ================= EDGE OF THE MAT
        Add("Edge warning", StepType.Hint, "", "Careful - you are near the edge of the mat. Step out and the referee gives you a warning.");
        Add("Edge warning time", StepType.Wait).seconds = 3f;
        Add("Clear edge warning", StepType.ClearHint);

        // ================= COMBO: leg kick lands (explained), then the punch: freeze, swap, and he is thrown off the mat
        var combo = Add("Combo: leg kick + swap", StepType.EnemyCombo);
        combo.seconds = 0.6f;
        combo.hitLessons = new List<ComboHitLesson>
        {
            new ComboHitLesson { letItPlay = true, title = "Leg kicks",
                                 body = "Hits below the belt don't score points, but they still add to your posture meter." },
            new ComboHitLesson { title = "Swap", body = "A punch! Block and dodge together to swap places with him and throw him off the mat.",
                                 controls = controlsSwap, requireResponse = RequiredResponse.Swap, freezeSecondsBeforeHit = 0.3f },
        };
        var push = Add("Throw him out of bounds", StepType.PushEnemyOut);
        push.seconds = 0.25f; push.distance = 0.4f;
        Add("Out of bounds", StepType.Panel, "Out of bounds",
            "He was thrown off the mat, so the referee gives HIM a warning. Two warnings cost a point - and the same goes for you.");

        // ================= BLOCK, BLOCK, DODGE: three strikes, one lesson each; then his combo is broken
        Allow("Input: + block", false, false, false, true);
        Add("Breathe", StepType.Wait).seconds = 0.8f;
        var three = Add("Combo: block, block, dodge", StepType.EnemyCombo);
        three.seconds = 0.8f;
        three.stopComboAfterLastLesson = true;
        three.hitLessons = new List<ComboHitLesson>
        {
            new ComboHitLesson { title = "Block", body = "The cue has flashed - this strike can be blocked. Block it.",
                                 controls = controlsBlock, requireResponse = RequiredResponse.Block, freezeSecondsBeforeHit = 0.3f },
            new ComboHitLesson { title = "Block again", body = "Another strike is coming. Block it too.",
                                 controls = controlsBlock, requireResponse = RequiredResponse.Block, freezeSecondsBeforeHit = 0.3f },
            new ComboHitLesson { title = "Dodge", body = "You can't block everything. This one - step out of the way.",
                                 controls = controlsDodge, requireResponse = RequiredResponse.Dodge },
        };
        // If his combo only has two strikes, the dodge gets its own attack.
        var dodge3 = Add("Dodge (if the combo was short)", StepType.EnemyAttack, "Dodge",
            "You can't block everything. This one - step out of the way.", controlsDodge);
        dodge3.skipIfComboFinished = true; dodge3.stopComboAfterResponse = true;
        dodge3.teachAt = TeachAt.PerfectWindow; dodge3.requireResponse = RequiredResponse.Dodge; dodge3.seconds = 0.6f;
        { var rs = Add("Reset posture bars", StepType.SetEnemyPosture); rs.posture = 0f; rs.resetPlayerPosture = true; }
        Add("Breathe", StepType.Wait).seconds = 1.0f;

        // ================= PERFECT BLOCK -> he is open -> punch (point)
        var pb1 = Add("Perfect block 1", StepType.EnemyAttack, "Perfect block",
            "The trail is red: block RIGHT NOW. A block at exactly this moment is a perfect block.", controlsBlock);
        pb1.freezeSecondsBeforeHit = 0.10f; pb1.teachAt = TeachAt.PerfectWindow; pb1.requireResponse = RequiredResponse.Block; pb1.seconds = 0.6f;
        Allow("Input: + move + punch", true, true, false, true);
        var c1 = Add("He is open: punch", StepType.CounterLesson, "He's open!",
            "A perfect block leaves him open. Step forward and punch him.", controlsPunch);
        c1.requireResponse = RequiredResponse.Punch; c1.guaranteeHit = true; c1.waitForPoint = true;
        Allow("Input: block only", false, false, false, true);
        Add("Breathe", StepType.Wait).seconds = 0.8f;

        // ================= the same again, but this time he DODGES the punch, then posture + stun are explained
        Add("Reset his posture", StepType.SetEnemyPosture).posture = 0f;
        var pb2 = Add("Perfect block 2", StepType.EnemyAttack, "Again",
            "Perfect block him once more.", controlsBlock);
        pb2.freezeSecondsBeforeHit = 0.10f; pb2.teachAt = TeachAt.PerfectWindow; pb2.requireResponse = RequiredResponse.Block; pb2.seconds = 0.6f;
        Add("He will dodge", StepType.EnemyAction).enemyAction = EnemyActionType.ReactDodge;
        Allow("Input: + move + punch", true, true, false, true);
        var c2 = Add("Punch him again", StepType.CounterLesson, "Punch him",
            "Punch again.", controlsPunch);
        c2.requireResponse = RequiredResponse.Punch; c2.guaranteeHit = false; c2.waitForPoint = false; c2.seconds = 1.2f;
        Add("Back to normal", StepType.EnemyAction).enemyAction = EnemyActionType.ReactNone;
        Allow("Input: block only", false, false, false, true);
        Add("He can dodge too", StepType.Panel, "He can dodge too",
            "Your opponent can slip your attacks just like you slip his. Not every punch lands.");
        Add("Posture and stun", StepType.Panel, "Posture",
            "Every perfect block fills HIS posture bar - and every block, and every leg kick, fills yours. When a bar is full, that " +
            "fighter is STUNNED. Keep parrying and he will break.");
        Add("Breathe", StepType.Wait).seconds = 0.8f;

        // ================= parry, parry, parry ... until he is stunned, then the kick
        Add("Empty his posture", StepType.SetEnemyPosture).posture = 0f;
        var parry = Add("Parry until stunned", StepType.ParryUntilStun, "Perfect block",
            "Red - block right now.", controlsBlock);
        parry.freezeSecondsBeforeHit = 0.10f; parry.teachAt = TeachAt.PerfectWindow; parry.requireResponse = RequiredResponse.Block; parry.seconds = 0.6f;
        Allow("Input: + move + kick", true, false, true, true);
        var stun = Add("Stunned: kick", StepType.StunLesson, "Stunned!",
            "His posture broke. While he is stunned he can't dodge or block. Kick him!", controlsKick);
        stun.requireResponse = RequiredResponse.Kick; stun.waitForPoint = true;
        Add("Breathe", StepType.Wait).seconds = 1.0f;

        // ================= the real fight, guided: freezes at every perfect-block moment until the player scores
        var guided = Add("Guided fight", StepType.GuidedFight, "Perfect block",
            "Red - block right now.", controlsBlock);
        guided.freezeSecondsBeforeHit = 0.10f;
        guided.afterTitle = "He's open!"; guided.afterBody = "Punch him."; guided.afterControls = controlsPunch;

        // ================= then it is the player's fight, no freezes, until someone reaches 7 points
        // (the injury incident is NOT in this plan yet: add a Finale step after this one when you are ready)
        var free = Add("Free play to 7", StepType.FreePlay, "", "");
        free.pointsToEnd = 7;

        steps = list;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"[Tutorial] Default lesson plan filled in: {steps.Count} steps. Edit the text in the Inspector if you like.");
    }

    // ------------------------------------------------------------------ util
    private static IEnumerator FadeGroup(CanvasGroup g, float target, float time)
    {
        if (g == null) yield break;
        float from = g.alpha, t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / Mathf.Max(0.01f, time)));
            yield return null;
        }
        g.alpha = target;
    }

    private void OnDestroy()
    {
        if (matchScore != null) matchScore.OnScoreChanged.RemoveListener(OnScoreChangedForKnockback);
        Time.timeScale = 1f;
    }
}