using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual prompt for RhythmSequenceMinigame, styled as a karate breaking board.
///
/// Flow per beat:
///   1. Board zooms in from off-screen left (position + scale together) showing
///      the Up/Down icon to press (a distinct sprite/colour if the beat is
///      risky). The icon itself is chosen by whether a controller is currently
///      connected (Input.GetJoystickNames), not by which device was used most
///      recently — see IsControllerConnected().
///   2. Holds in place while the beat is active — RhythmSequenceMinigame's own
///      timingWindow governs how long that is, this script doesn't run its own timer.
///   3. On resolve (hit or miss), the board falls straight down and disappears.
///      A correct press also swaps in brokenBoardSprite first, if assigned, so
///      it reads as breaking on the way down rather than just falling intact.
///
/// Setup: put this on a UI object under a Canvas, with a child RectTransform
/// ("board") holding an Image (the board sprite) and a second Image (the
/// button icon) as its own child so it moves/fades together with the board.
/// </summary>
public class BoardPromptUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private RhythmSequenceMinigame minigame;

    [Header("Board")]
    [SerializeField] private RectTransform board;
    [SerializeField] private Image boardImage;
    [SerializeField] private Image buttonIcon;

    [Header("Sprites — Board")]
    [SerializeField] private Sprite normalBoardSprite;
    [SerializeField] private Sprite riskyBoardSprite;
    [Tooltip("Optional. Swapped in on a correct press before the board falls. Leave blank to skip.")]
    [SerializeField] private Sprite brokenBoardSprite;

    [Header("Sprites — Icon (Mouse & Keyboard)")]
    [SerializeField] private Sprite upIconSpriteMouse;
    [SerializeField] private Sprite downIconSpriteMouse;

    [Header("Sprites — Icon (Controller)")]
    [SerializeField] private Sprite upIconSpriteController;
    [SerializeField] private Sprite downIconSpriteController;

    [Header("Positions (anchored position on the board's parent)")]
    [SerializeField] private Vector2 onScreenPos = Vector2.zero;
    [Tooltip("Off-screen to the left — where the board zooms in from.")]
    [SerializeField] private Vector2 offScreenPos = new Vector2(-800f, 0f);
    [Tooltip("Where the board falls to after being resolved (hit or missed), then disappears.")]
    [SerializeField] private Vector2 fallExitPos = new Vector2(0f, -600f);

    [Header("Motion")]
    [SerializeField] private float slideInTime = 0.15f;
    [SerializeField] private float exitTime = 0.25f;
    [Tooltip("Scale the board starts at while zooming in from off-screen.")]
    [SerializeField] private float zoomInStartScale = 0.4f;

    private Coroutine activeRoutine;

    private bool IsControllerConnected()
    {
        string[] joysticks = Input.GetJoystickNames();
        for (int i = 0; i < joysticks.Length; i++)
        {
            if (!string.IsNullOrEmpty(joysticks[i])) return true;
        }
        return false;
    }

    private void OnEnable()
    {
        if (minigame == null) return;
        minigame.OnBeatStarted += HandleBeatStarted;
        minigame.OnBeatResolved += HandleBeatResolved;
        if (board != null) board.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (minigame == null) return;
        minigame.OnBeatStarted -= HandleBeatStarted;
        minigame.OnBeatResolved -= HandleBeatResolved;
    }

    private void HandleBeatStarted(RhythmSequenceMinigame.RhythmBeat beat)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ShowBoard(beat));
    }

    private IEnumerator ShowBoard(RhythmSequenceMinigame.RhythmBeat beat)
    {
        board.gameObject.SetActive(true);
        board.anchoredPosition = offScreenPos;

        boardImage.sprite = beat.isRisky ? riskyBoardSprite : normalBoardSprite;
        boardImage.color = Color.white;

        bool usingController = IsControllerConnected();
        buttonIcon.enabled = true;
        buttonIcon.sprite = beat.expectedButton == RhythmSequenceMinigame.BeatButton.Up
            ? (usingController ? upIconSpriteController : upIconSpriteMouse)
            : (usingController ? downIconSpriteController : downIconSpriteMouse);

        yield return ZoomIn(offScreenPos, onScreenPos, zoomInStartScale, slideInTime);
        // Board now holds at onScreenPos until HandleBeatResolved fires.
    }

    private IEnumerator ZoomIn(Vector2 fromPos, Vector2 toPos, float fromScale, float duration)
    {
        float elapsed = 0f;
        board.anchoredPosition = fromPos;
        board.localScale = Vector3.one * fromScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Ease-out — fast start, settles into place, reads as a "zoom in" snap.
            float t = 1f - (1f - elapsed / duration) * (1f - elapsed / duration);
            board.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
            board.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, t);
            yield return null;
        }

        board.anchoredPosition = toPos;
        board.localScale = Vector3.one;
    }

    private void HandleBeatResolved(bool success)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ResolveBoard(success));
    }

    private IEnumerator ResolveBoard(bool success)
    {
        buttonIcon.enabled = false;

        if (success && brokenBoardSprite != null)
            boardImage.sprite = brokenBoardSprite;

        yield return Fall(board.anchoredPosition, fallExitPos, exitTime);

        board.gameObject.SetActive(false);
    }

    private IEnumerator Fall(Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        board.anchoredPosition = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Ease-in — starts slow, accelerates, reads as gravity pulling it down.
            float t = elapsed / duration;
            board.anchoredPosition = Vector2.Lerp(from, to, t * t);
            yield return null;
        }
        board.anchoredPosition = to;
    }
}