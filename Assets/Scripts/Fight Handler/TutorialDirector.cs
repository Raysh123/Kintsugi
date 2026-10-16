using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The tutorial fight, kept simple. It does exactly four things:
///
///   1. When the scene starts it opens the TutorialBrowser (the video pages) and waits until the player closes it.
///   2. Then the normal fight goes on - the referee, scoring and the enemy's own AI are untouched.
///   3. During the fight it smoothly SLOWS TIME DOWN just before each of the enemy's hits, and eases back afterwards.
///   4. When either side reaches the target score (7): the point is awarded, the referee restarts the match (Hajime), then the
///      clock stops and the inputs lock, a short dialogue plays (skip each line with Enter / A), the player is prompted for the
///      FINISHER (Up + Kick / Up + B). When it is pressed the player and the enemy are hidden and the injury cutscene plays
///      (ONE prefab holding both fighters in one animation); 'After Clip Seconds' after the clip ends the next scene loads.
///
/// Nothing else is driven: no scripted attacks, no input locks. (The enemy needs his own real MatchTimer in his Match Timer field -
/// there is no "EnemyGate" any more.)
///
/// Setup: see the tooltips. The cutscene prefab sits in the scene, switched OFF, with an Animator on its root whose default state is the clip.
/// </summary>
[DefaultExecutionOrder(-200)]   // runs before the player scripts read the input, so the finisher prompt can lock everything else
public class TutorialDirector : MonoBehaviour
{
    // ------------------------------------------------------------------ 1. pages
    [Header("1. Tutorial pages (shown when the scene starts)")]
    [Tooltip("The TutorialBrowser screen. Leave empty to find one in the scene (even if switched off). It handles its own panel / canvas.")]
    [SerializeField] private TutorialBrowser tutorialBrowser;
    [SerializeField] private bool showPagesAtStart = true;

    // ------------------------------------------------------------------ 3. slow motion
    [Header("2. Slow-motion before his attacks")]
    [SerializeField] private bool attackSlowMo = true;
    [Tooltip("Time scale at the deepest point (0.4 = 40% speed).")]
    [Range(0.1f, 1f)][SerializeField] private float slowMoScale = 0.4f;
    [Tooltip("Real seconds to ease INTO the slow-down (bigger = softer).")]
    [SerializeField] private float slowMoEaseIn = 0.18f;
    [Tooltip("Real seconds to ease back to normal speed (bigger = softer).")]
    [SerializeField] private float slowMoEaseOut = 0.35f;
    [Tooltip("The slow-down starts when his next hit is this many (game) seconds away, so the windup stays readable.")]
    [SerializeField] private float slowMoLeadSeconds = 0.6f;
    [Tooltip("Stay slow this long after a hit is over, so it never snaps back.")]
    [SerializeField] private float slowMoHoldAfterHit = 0.2f;

    // ------------------------------------------------------------------ 4. injury
    [Header("3. The injury (at this score)")]
    [Tooltip("When either fighter reaches this many points, the injury cutscene starts.")]
    [SerializeField] private int pointsToEnd = 7;
    [Tooltip("The cutscene prefab, placed in the scene and switched OFF. Its root needs an Animator whose default state is the clip.")]
    [SerializeField] private GameObject cutsceneRoot;
    [Tooltip("Leave empty to find the Animator on the Cutscene Root.")]
    [SerializeField] private Animator cutsceneAnimator;
    [Tooltip("Animator state to play. Blank = the controller's default state.")]
    [SerializeField] private string cutsceneState = "";
    [Tooltip("Playback speed of the injury animation (0.6 = 60%). Set from code on the Animator, so you don't have to change the clip or the state speed in Unity.")]
    [Range(0.1f, 2f)][SerializeField] private float cutsceneSpeed = 0.6f;
    [Tooltip("Leave empty: found automatically. These are the two objects that get hidden.")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform enemyRoot;
    [Tooltip("Move the cutscene to the midpoint between the two fighters so it lines up with where they stood.")]
    [SerializeField] private bool placeAtFighters = true;
    [SerializeField] private bool matchFacing = true;
    [Tooltip("Turn the cutscene by this many degrees if the characters face the wrong way (try 90, 180, 270).")]
    [SerializeField] private float facingOffsetY = 0f;
    [Tooltip("Keep the cutscene prefab's own height (Y).")]
    [SerializeField] private bool keepOwnHeight = true;

    [Header("3b. Dialogue and finisher prompt (shown after the 7th point and the referee's Hajime)")]
    [Tooltip("Speaker name shown with the lines (optional).")]
    [SerializeField] private string dialogueSpeaker = "";
    [TextArea(1, 4)]
    [SerializeField]
    private string[] dialogueLines = new[]
    {
        "Seven points. One more and the title is mine.",
        "Time to end this with something he will never forget."
    };
    [Tooltip("The dialogue panel (a CanvasGroup). Each line is closed with the project's skip button (Enter / A). Leave empty to skip the dialogue.")]
    [SerializeField] private CanvasGroup dialoguePanel;
    [SerializeField] private TMP_Text dialogueSpeakerText;
    [SerializeField] private TMP_Text dialogueBodyText;
    [Tooltip("Shown while waiting for the finisher.")]
    [SerializeField] private CanvasGroup promptPanel;
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private string promptString = "Finish him!   Up + Kick";
    [Tooltip("Optional picture of the buttons.")]
    [SerializeField] private Image promptIcon;
    [Tooltip("Safety: if the referee never restarts the match after the 7th point (e.g. the match simply ends), give up waiting after this many real seconds and carry on.")]
    [SerializeField] private float hajimeTimeout = 25f;
    [SerializeField] private float panelFade = 0.2f;
    [Tooltip("Ignore the skip / finisher button for this long after a panel appears (so a leftover press doesn't skip it).")]
    [SerializeField] private float minShow = 0.35f;

    [Header("4. Then")]
    [Tooltip("The scene changes this many seconds AFTER the animation has finished.")]
    [SerializeField] private float afterClipSeconds = 2f;
    [SerializeField] private string nextSceneName = "RPG";
    [Tooltip("Optional black CanvasGroup that fades in before the scene loads.")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeTime = 1.2f;

    [Header("References (auto-found)")]
    [SerializeField] private EnemyStateMachine enemy;
    [SerializeField] private MatchScore matchScore;
    [SerializeField] private MatchTimer matchTimer;

    private bool promptActive;   // the finisher prompt is up: every other fight input is cleared
    private bool introDone;
    private bool injuryStarted;
    private float slowVel;
    private float slowHoldUntil;

    private void Start()
    {
        if (enemy == null) enemy = FindFirstObjectByType<EnemyStateMachine>();
        if (matchScore == null) matchScore = FindFirstObjectByType<MatchScore>();
        if (matchTimer == null) matchTimer = FindFirstObjectByType<MatchTimer>();
        if (fadeOverlay != null) fadeOverlay.alpha = 0f;
        if (cutsceneRoot != null) cutsceneRoot.SetActive(false);


        StartCoroutine(Intro());
    }

    // ------------------------------------------------------------------ 1. pages
    private IEnumerator Intro()
    {
        yield return null;   // let every other script finish its own Start first
        if (showPagesAtStart)
        {
            if (tutorialBrowser == null) tutorialBrowser = FindFirstObjectByType<TutorialBrowser>(FindObjectsInactive.Include);
            if (tutorialBrowser != null) yield return tutorialBrowser.Show();
        }
        introDone = true;
    }

    private void Update()
    {
        if (promptActive) ClearFightInput();
        if (!introDone || injuryStarted) return;
        CheckScore();
        UpdateAttackSlowMo();
    }

    // ------------------------------------------------------------------ 3. slow motion
    /// <summary>Seconds until the enemy's next hit-open event in the clip he is playing (-1 = none).</summary>
    private float SecondsToNextHit()
    {
        if (enemy == null || enemy.anim == null) return -1f;
        Animator a = enemy.anim;
        AnimatorClipInfo[] infos = a.GetCurrentAnimatorClipInfo(0);
        if (infos == null || infos.Length == 0 || infos[0].clip == null) return -1f;
        AnimationClip clip = infos[0].clip;
        AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
        float now = (st.loop ? Mathf.Repeat(st.normalizedTime, 1f) : Mathf.Clamp01(st.normalizedTime)) * clip.length;
        float speed = Mathf.Max(0.01f, a.speed * st.speedMultiplier);
        float best = -1f;
        foreach (AnimationEvent evt in clip.events)
        {
            if ((evt.functionName != "DealDamage" && evt.functionName != "OpenEnemyHitWindow") || evt.time <= now) continue;
            if (best < 0f || evt.time < best) best = evt.time;
        }
        return best < 0f ? -1f : (best - now) / speed;
    }

    /// <summary>Only touches the time scale while it is above 0 (never fights a pause); if something else changed it, it simply
    /// continues smoothly from there.</summary>
    private void UpdateAttackSlowMo()
    {
        if (!attackSlowMo) return;
        float cur = Time.timeScale;
        if (cur <= 0.001f) { slowVel = 0f; return; }

        float target = 1f;
        bool playing = matchTimer == null || !matchTimer.matchStopped;
        if (playing && enemy != null && enemy.currentState == EnemyStateMachine.EnemyState.Attack)
        {
            float secs = SecondsToNextHit();
            bool imminent = enemy.HitWindowOpen || (secs >= 0f && secs <= slowMoLeadSeconds);
            if (imminent) slowHoldUntil = Time.unscaledTime + slowMoHoldAfterHit;
            if (imminent || Time.unscaledTime < slowHoldUntil) target = slowMoScale;
        }

        if (Mathf.Approximately(cur, target) && Mathf.Abs(slowVel) < 0.0001f) return;
        float smooth = target < cur ? slowMoEaseIn : slowMoEaseOut;
        float next = Mathf.SmoothDamp(cur, target, ref slowVel, Mathf.Max(0.01f, smooth), Mathf.Infinity, Time.unscaledDeltaTime);
        Time.timeScale = Mathf.Clamp(next, 0.05f, 1f);
        if (Mathf.Abs(Time.timeScale - target) < 0.005f) { Time.timeScale = target; slowVel = 0f; }
    }

    // ------------------------------------------------------------------ 4. injury
    private void CheckScore()
    {
        if (matchScore == null) return;
        if (matchScore.PlayerScore >= pointsToEnd || matchScore.EnemyScore >= pointsToEnd)
        {
            injuryStarted = true;
            StartCoroutine(Injury());
        }
    }

    private static float LongestClip(Animator a, string preferName)
    {
        if (a == null || a.runtimeAnimatorController == null) return 0f;
        float best = 0f, named = 0f;
        foreach (var c in a.runtimeAnimatorController.animationClips)
        {
            if (c == null) continue;
            best = Mathf.Max(best, c.length);
            if (!string.IsNullOrEmpty(preferName) && c.name == preferName) named = c.length;
        }
        return (named > 0f ? named : best) / Mathf.Max(0.01f, a.speed);
    }

    private bool finisherSeen;

    private static bool UpHeld()
    {
        var kb = Keyboard.current; var gp = Gamepad.current;
        return (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed)) ||
               (gp != null && (gp.leftStick.y.ReadValue() > 0.5f || gp.dpad.up.isPressed));
    }

    /// <summary>The finisher = Up + Kick (Up + B on the pad). Accepted from the UpperKick action, from a plain kick while Up is held,
    /// or from the pad's B while Up is held - so it works even if the UpperKick event isn't wired in the PlayerInput.</summary>
    private bool FinisherPressed(InputHandler ih)
    {
        if (ih != null && ih.upperKick) return true;
        if (finisherSeen) return true;
        var gp = Gamepad.current;
        return gp != null && gp.buttonEast.wasPressedThisFrame && UpHeld();
    }

    /// <summary>While the finisher prompt is up the player can do NOTHING except the finisher: everything else is cleared before his scripts read it.</summary>
    private void ClearFightInput()
    {
        var ih = InputHandler.instance;
        if (ih == null) return;
        if (ih.kick && UpHeld()) finisherSeen = true;   // a kick while holding Up
        ih.move = 0f; ih.speed = 0f;
        ih.punch = false; ih.kick = false; ih.dodge = false; ih.block = false; ih.swap = false;
    }

    // plain on-screen fallback (IMGUI works while the game is frozen) when the dialogue / prompt panels aren't assigned
    private string guiText;
    private void OnGUI()
    {
        if (string.IsNullOrEmpty(guiText)) return;
        float w = Mathf.Min(Screen.width * 0.8f, 900f), h = 110f;
        Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 40f, w, h);
        GUI.Box(r, GUIContent.none);
        var style = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        GUI.Label(r, guiText, style);
    }

    private static IEnumerator FadePanel(CanvasGroup g, float target, float time)
    {
        if (g == null) yield break;
        if (target > 0f) g.gameObject.SetActive(true);
        float from = g.alpha, t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / Mathf.Max(0.01f, time)));
            yield return null;
        }
        g.alpha = target;
        if (target <= 0f) g.gameObject.SetActive(false);
    }

    private IEnumerator Injury()
    {
        Time.timeScale = 1f;   // the referee's sequence runs normally (no slow-mo from here on)
        slowVel = 0f;

        // 1) the point is awarded, the referee calls Yame / resets the fighters ... and says Hajime (the clock runs again)
        if (matchTimer != null)
        {
            float guard = 0f;
            while (!matchTimer.matchStopped && !matchTimer.matchOver && guard < 3f) { guard += Time.unscaledDeltaTime; yield return null; }
            guard = 0f;
            while (matchTimer.matchStopped && !matchTimer.matchOver && guard < hajimeTimeout) { guard += Time.unscaledDeltaTime; yield return null; }
            matchTimer.StopTimer();
        }

        // 2) inputs locked, the fight frozen
        var ih = InputHandler.instance;
        ih?.SetInput(false);
        Time.timeScale = 0f;

        // 3) dialogue: one line at a time, skipped with Enter / A (on-screen text even if no panel is assigned)
        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            if (dialoguePanel != null)
            {
                if (dialogueSpeakerText != null) dialogueSpeakerText.text = dialogueSpeaker;
                if (dialogueBodyText != null) dialogueBodyText.text = dialogueLines[0];
                yield return FadePanel(dialoguePanel, 1f, panelFade);
            }
            foreach (string line in dialogueLines)
            {
                if (dialoguePanel != null) { if (dialogueBodyText != null) dialogueBodyText.text = line; }
                else guiText = (string.IsNullOrEmpty(dialogueSpeaker) ? "" : dialogueSpeaker + ":  ") + line + "\n(Enter / A)";
                float t = 0f;
                while (true)
                {
                    t += Time.unscaledDeltaTime;
                    if (t >= minShow && SkipInput.Pressed()) break;
                    yield return null;
                }
                yield return null;
            }
            guiText = null;
            if (dialoguePanel != null) yield return FadePanel(dialoguePanel, 0f, panelFade);
        }

        // 4) swap to the cutscene NOW, frozen on its first frame: both live fighters are hidden and the paired prefab stands
        //    exactly where they were, so the prompt is shown over the cutscene's first pose
        Animator ca = null;
        if (cutsceneRoot == null)
        {
        }
        else
        {
            var psm = PlayerStateMachine.instance;
            Transform p = playerRoot != null ? playerRoot : (psm != null ? (psm.player != null ? psm.player.transform : psm.transform) : null);
            Transform e = enemyRoot != null ? enemyRoot : (enemy != null ? enemy.transform : null);
            Vector3 pPos = p != null ? p.position : cutsceneRoot.transform.position;
            Vector3 ePos = e != null ? e.position : pPos;
            if (placeAtFighters)
            {
                Vector3 mid = (pPos + ePos) * 0.5f;
                Vector3 pos = cutsceneRoot.transform.position;
                pos.x = mid.x; pos.z = mid.z;
                if (!keepOwnHeight) pos.y = mid.y;
                cutsceneRoot.transform.position = pos;
                if (matchFacing)
                {
                    Vector3 dir = ePos - pPos; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f)
                        cutsceneRoot.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, facingOffsetY, 0f);
                }
            }
            if (p != null) p.gameObject.SetActive(false);
            if (e != null) e.gameObject.SetActive(false);

            cutsceneRoot.SetActive(true);
            ca = cutsceneAnimator != null ? cutsceneAnimator : cutsceneRoot.GetComponentInChildren<Animator>();
            if (ca != null)
            {
                if (!string.IsNullOrEmpty(cutsceneState)) ca.Play(cutsceneState, 0, 0f);
                ca.speed = 0f;      // held on frame 0
                ca.Update(0f);      // apply that first pose right now, even though time is frozen
            }
        }

        // 5) the finisher prompt: only Up + Kick (Up + B) is accepted
        if (ih != null)
        {
            ih.SetInput(true);          // the finisher action needs the handler on ...
            promptActive = true;        // ... but every other input is cleared each frame
            ih.upperKick = false;
            finisherSeen = false;
            if (promptPanel == null) guiText = promptString;
            if (promptLabel != null) promptLabel.text = promptString;
            if (promptIcon != null) promptIcon.enabled = promptIcon.sprite != null;
            yield return FadePanel(promptPanel, 1f, panelFade);
            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                if (t >= minShow && FinisherPressed(ih)) break;
                yield return null;
            }
            ih.upperKick = false;
            guiText = null;
            promptActive = false;
            ih.SetInput(false);
            yield return FadePanel(promptPanel, 0f, 0.1f);
        }

        // 6) the finisher is pressed: time runs again and the animation plays from its first frame
        Time.timeScale = 1f;
        if (ca != null)
        {
            ca.speed = cutsceneSpeed;
            float len = LongestClip(ca, cutsceneState);   // already includes the speed
            if (len <= 0f) { len = 5f; }
            yield return new WaitForSecondsRealtime(len);
        }

        yield return new WaitForSecondsRealtime(afterClipSeconds);

        if (fadeOverlay != null)
        {
            float from = fadeOverlay.alpha, t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, fadeTime)));
                yield return null;
            }
            fadeOverlay.alpha = 1f;
        }

        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            yield break;
        }
        SceneManager.LoadScene(nextSceneName);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}