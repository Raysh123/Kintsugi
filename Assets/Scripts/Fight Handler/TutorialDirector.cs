using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The tutorial fight, kept simple. It does exactly three things:
///
///   1. When the scene starts it opens the TutorialBrowser (the video pages) and waits until the player closes it.
///   2. Then the normal fight goes on - the referee, scoring and the enemy's own AI are untouched.
///   3. When either side reaches the target score (7): the point is awarded, the referee restarts the match (Hajime), then the
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

    // ------------------------------------------------------------------ 4. injury
    [Header("3. The injury (at this score)")]
    [Tooltip("When either fighter reaches this many points, the injury cutscene starts.")]
    [SerializeField] private int pointsToEnd = 7;
    [Tooltip("If nobody has reached the score by the time the clock shows this many seconds left, the injury cutscene starts anyway. 0 = off.")]
    [SerializeField] private float endAtSecondsLeft = 10f;
    [Tooltip("The referee stops the match (Yame, fighters reset, like after a point) and calls this before the cutscene starts. Blank = no call.")]
    [SerializeField] private string timeWarningText = "10 seconds left!";
    [Tooltip("Real seconds the warning stays up after the reset, before Hajime.")]
    [SerializeField] private float timeWarningHold = 1.5f;
    [Tooltip("Real seconds of play after Hajime before the cutscene freezes the fight.")]
    [SerializeField] private float afterHajimeSeconds = 0.6f;
    [SerializeField] private RefereeController referee;
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
    [Tooltip("The line shown when YOU reach the score first.")]
    [TextArea(1, 4)][SerializeField] private string playerReachedLine = "Seven points. One more and the title is mine.";
    [Tooltip("The line shown when the OPPONENT reaches the score first.")]
    [TextArea(1, 4)][SerializeField] private string enemyReachedLine = "Seven points for him. I can't lose like this.";
    [Tooltip("The line shown when the clock hits the time limit instead.")]
    [TextArea(1, 4)][SerializeField] private string timeUpLine = "Time is nearly gone. This ends now.";
    private string[] dialogueLines = new string[0];
    [Tooltip("The dialogue panel (a CanvasGroup). Each line is closed with the project's skip button (Enter / A). Leave empty to skip the dialogue.")]
    [SerializeField] private CanvasGroup dialoguePanel;
    [SerializeField] private TMP_Text dialogueSpeakerText;
    [Tooltip("The PLAYER's icon on the finisher dialogue panel (an Image). Optional.")]
    [SerializeField] private Image dialogueIcon;
    [Tooltip("Sprite put on that icon. Blank = keep whatever the Image already shows.")]
    [SerializeField] private Sprite dialogueIconSprite;
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

    [Header("3c. Opponent taunts (text, after each point HE scores)")]
    [Tooltip("One is picked at random (never the same twice in a row) after each point the opponent scores. Leave empty for no taunts. " +
             "The 7th point goes straight to the finisher instead.")]
    [TextArea(1, 3)]
    [SerializeField]
    private string[] tauntLines = new[]
    {
        "Is that all you've got?",
        "Too slow!",
        "Try to keep up.",
        "You can't touch me."
    };
    [SerializeField] private string tauntSpeaker = "";
    [Tooltip("Its OWN small panel (a CanvasGroup), separate from the finisher dialogue panel. Keep it switched off. Without it no taunts show.")]
    [SerializeField] private CanvasGroup tauntPanel;
    [SerializeField] private TMP_Text tauntSpeakerText;
    [SerializeField] private TMP_Text tauntBodyText;
    [Tooltip("The OPPONENT's icon on the taunt panel (an Image). Optional.")]
    [SerializeField] private Image tauntIcon;
    [Tooltip("Sprite put on that icon. Blank = keep whatever the Image already shows.")]
    [SerializeField] private Sprite tauntIconSprite;
    [Tooltip("Real seconds after the referee's Yame and the reset (the point has been awarded) before the taunt appears.")]
    [SerializeField] private float tauntDelay = 0.3f;
    [Tooltip("The clock stays stopped while the taunt is up; it closes on Enter / A and then the referee says Hajime. " +
             "Ignore that button for this long after the taunt appears.")]
    [SerializeField] private float tauntMinShow = 0.5f;
    [Tooltip("Closes by itself after this many real seconds. 0 = waits for the button.")]
    [SerializeField] private float tauntHold = 0f;

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

    private void Awake()
    {
        // The referee's opening ritual (calls, sounds, music) waits until the tutorial pages are closed.
        RefereeController.HoldIntro = showPagesAtStart;
    }

    private void Start()
    {
        if (enemy == null) enemy = FindFirstObjectByType<EnemyStateMachine>();
        if (matchScore == null) matchScore = FindFirstObjectByType<MatchScore>();
        if (matchTimer == null) matchTimer = FindFirstObjectByType<MatchTimer>();
        if (referee == null) referee = FindFirstObjectByType<RefereeController>();
        if (fadeOverlay != null) fadeOverlay.alpha = 0f;
        if (cutsceneRoot != null) cutsceneRoot.SetActive(false);
        if (tauntPanel != null) tauntPanel.alpha = 0f;
        if (referee != null) { referee.onEnemyPoint.AddListener(OnEnemyScored); referee.onReadyToReset.AddListener(OnResetDone); }

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
        RefereeController.HoldIntro = false;
    }

    private void Update()
    {
        if (promptActive) ClearFightInput();
        if (!introDone || injuryStarted) return;
        CheckScore();
    }

    // ------------------------------------------------------------------ 4. injury
    private void CheckScore()
    {
        if (matchScore == null) return;
        bool scoreReached = matchScore.PlayerScore >= pointsToEnd || matchScore.EnemyScore >= pointsToEnd;
        // time trigger waits for play to be running, so it never cuts into a point that is still being called
        bool timeIsUp = endAtSecondsLeft > 0f && matchTimer != null && !matchTimer.matchOver && !matchTimer.matchStopped &&
                        matchTimer.TimeRemaining <= endAtSecondsLeft;
        if (scoreReached || timeIsUp)
        {
            string line = matchScore.PlayerScore >= pointsToEnd ? playerReachedLine
                        : matchScore.EnemyScore >= pointsToEnd ? enemyReachedLine : timeUpLine;
            dialogueLines = string.IsNullOrEmpty(line) ? new string[0] : new[] { line };
            injuryStarted = true;
            StartCoroutine(Injury(!scoreReached));
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

    private IEnumerator Injury(bool byTime = false)
    {
        tauntPending = false;
        ReleaseHold();
        if (tauntRoutine != null) { StopCoroutine(tauntRoutine); tauntRoutine = null; }
        if (tauntPanel != null) { tauntPanel.alpha = 0f; tauntPanel.gameObject.SetActive(false); }
        HideObjs(tauntSpeakerText, tauntBodyText, tauntIcon);
        Time.timeScale = 1f;

        // 1) the point is awarded, the referee calls Yame / resets the fighters ... and says Hajime (the clock runs again)
        if (byTime && matchTimer != null)
        {
            // no point was scored: stop the match like after a point, warn, reset, Hajime
            matchTimer.StopTimer();
            if (referee == null) referee = FindFirstObjectByType<RefereeController>();
            if (referee != null)
            {
                referee.CallPenalty(timeWarningText, null);
                float wait = 0f;
                while (referee.IsCalling && wait < 8f) { wait += Time.unscaledDeltaTime; yield return null; }
                if (timeWarningHold > 0f) yield return new WaitForSecondsRealtime(timeWarningHold);
            }
            matchTimer.StartTimer();
            yield return new WaitForSecondsRealtime(afterHajimeSeconds);
            matchTimer.StopTimer();
        }
        else if (matchTimer != null)
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
                if (dialogueSpeakerText != null) { dialogueSpeakerText.gameObject.SetActive(true); dialogueSpeakerText.text = dialogueSpeaker; }
                if (dialogueBodyText != null) dialogueBodyText.gameObject.SetActive(true);
                if (dialogueIcon != null) { if (dialogueIconSprite != null) dialogueIcon.sprite = dialogueIconSprite; dialogueIcon.gameObject.SetActive(dialogueIcon.sprite != null); }
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
            HideObjs(dialogueSpeakerText, dialogueBodyText, dialogueIcon);
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
            if (promptLabel != null) { promptLabel.gameObject.SetActive(true); promptLabel.text = promptString; }
            if (promptIcon != null) { promptIcon.gameObject.SetActive(promptIcon.sprite != null); promptIcon.enabled = promptIcon.sprite != null; }
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
            HideObjs(promptLabel, promptIcon);
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

    private Coroutine tauntRoutine;
    private int lastTaunt = -1;

    private void OnEnemyScored()
    {
        if (injuryStarted || tauntPanel == null || tauntLines == null || tauntLines.Length == 0) return;
        if (matchScore != null && matchScore.EnemyScore >= pointsToEnd) return;   // the finisher takes over
        tauntPending = true;   // shown once the referee has called Yame and the fighters are reset
        if (matchTimer != null) matchTimer.HoldResume = true;   // no Hajime until the taunt is closed
    }

    private bool tauntPending;

    // Icons / texts may sit outside their panel, so switch them off themselves when the panel closes.
    private static void HideObjs(params Component[] items)
    {
        foreach (Component c in items) if (c != null) c.gameObject.SetActive(false);
    }

    private void ReleaseHold() { if (matchTimer != null) matchTimer.HoldResume = false; }

    private void OnResetDone()
    {
        if (!tauntPending) return;
        tauntPending = false;
        if (injuryStarted) { ReleaseHold(); return; }
        if (tauntRoutine != null) StopCoroutine(tauntRoutine);
        tauntRoutine = StartCoroutine(TauntRoutine());
    }

    private IEnumerator TauntRoutine()
    {
        yield return new WaitForSecondsRealtime(tauntDelay);
        if (injuryStarted) { ReleaseHold(); yield break; }

        var valid = new System.Collections.Generic.List<int>();
        for (int k = 0; k < tauntLines.Length; k++) if (!string.IsNullOrEmpty(tauntLines[k])) valid.Add(k);
        if (valid.Count == 0) { ReleaseHold(); yield break; }
        if (valid.Count > 1) valid.Remove(lastTaunt);
        lastTaunt = valid[Random.Range(0, valid.Count)];

        if (tauntSpeakerText != null) { tauntSpeakerText.gameObject.SetActive(true); tauntSpeakerText.text = tauntSpeaker; }
        if (tauntIcon != null) { if (tauntIconSprite != null) tauntIcon.sprite = tauntIconSprite; tauntIcon.gameObject.SetActive(tauntIcon.sprite != null); }
        if (tauntBodyText != null) { tauntBodyText.gameObject.SetActive(true); tauntBodyText.text = tauntLines[lastTaunt]; }
        yield return FadePanel(tauntPanel, 1f, panelFade);
        float held = 0f;
        while (true)
        {
            held += Time.unscaledDeltaTime;
            if (held >= tauntMinShow && SkipInput.Pressed()) break;
            if (tauntHold > 0f && held >= tauntHold) break;
            yield return null;
        }
        yield return FadePanel(tauntPanel, 0f, panelFade);
        HideObjs(tauntSpeakerText, tauntBodyText, tauntIcon);
        ReleaseHold();
        tauntRoutine = null;
    }

    private void OnDestroy()
    {
        RefereeController.HoldIntro = false;
        if (referee != null) { referee.onEnemyPoint.RemoveListener(OnEnemyScored); referee.onReadyToReset.RemoveListener(OnResetDone); }
        Time.timeScale = 1f;
    }
}