using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A non-interactive comic-book-style cutscene, laid out as one tall strip of
/// panel images (webtoon-style). Rather than scrolling a ScrollRect, this
/// simply moves the WHOLE strip vertically so whichever panel is currently
/// focused has its pivot centered on screen — X position is never touched.
///
/// Navigation: Left/Right (A/D keys + left stick, same debounced GetButtonDown
/// approach as TravelMapController) moves between panels. Moving right past
/// the last panel reveals it (if not already shown) and focuses it; pressing
/// right again once already on the last panel completes the cutscene. Moving
/// left revisits already-revealed panels, so a player who looks away can
/// scroll back without re-triggering anything. Escape skips the whole
/// cutscene instantly on keyboard; holding B on gamepad skips after
/// Hold To Skip Duration seconds held.
///
/// Call Play() directly to trigger — this does NOT auto-fire on any phase
/// change. It's meant to be invoked from the middle of a specific sleep
/// sequence (e.g. BedRestPhaseController, right after the fade-to-black,
/// before the wake-up/fade-in), so the calling code controls exactly when
/// it plays rather than this component guessing based on phase timing.
/// </summary>
public class ComicCutscene : MonoBehaviour
{
    [Header("Lock During Cutscene")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("Optional direct reference. If left blank, resolved automatically from Camera.main at Start.")]
    [SerializeField] private TopDownCameraFollow cameraFollow;

    [Header("Comic Strip")]
    [Tooltip("The tall parent container holding all comic panel children, stacked top-to-bottom. " +
             "This whole object is moved vertically (only) so whichever panel is focused centers on screen.")]
    [SerializeField] private RectTransform stripContainer;
    [Tooltip("Each panel, in the order they appear top-to-bottom. Each should start INACTIVE — " +
             "this script activates them the first time they're reached.")]
    [SerializeField] private List<RectTransform> panels = new List<RectTransform>();

    [Header("Audio")]
    [Tooltip("Music (SoundLibrary id on this scene's AudioManager) that plays during the cutscene. " +
             "Blank = no music.")]
    [SerializeField] private string musicId;
    [SerializeField] private float musicFadeIn = 1f;
    [Tooltip("Fade-out when the cutscene ends.")]
    [SerializeField] private float musicFadeOut = 1.5f;
    [Tooltip("Fade out the scene's ambience (RecoverySoundscape) while the cutscene plays, and " +
             "bring it back afterwards.")]
    [SerializeField] private bool silenceAmbience = true;

    [Header("Movement")]
    [SerializeField] private float moveDuration = 0.3f;

    [Header("Skip")]
    [Tooltip("Escape skips instantly on keyboard. On gamepad, holding B for this long also skips.")]
    [SerializeField] private float holdToSkipDuration = 1f;

    private const float cycleThreshold = 0.5f;

    private int currentIndex = -1;
    private int highestRevealedIndex = -1;
    private bool isPlaying;
    private bool cameraOrbitWasEnabled;
    private Coroutine moveCoroutine;
    private float skipHoldTimer;
    private bool musicStartedByMe;

    public event System.Action OnCutsceneComplete;

    private void Start()
    {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<TopDownCameraFollow>();

        if (stripContainer != null) stripContainer.gameObject.SetActive(false);
    }

    public void Play()
    {
        if (isPlaying) return;

        // These two are the only things that actually make the cutscene visible - if either
        // is unassigned (e.g. a reference lost when this object was moved/duplicated into a
        // new scene), everything below still "runs" (Play() gets called, the coroutine
        // starts, logs say it's playing) but nothing ever appears on screen, silently.
        if (stripContainer == null)
            Debug.LogWarning("[Comic] Play() called but Strip Container is not assigned - nothing will show.");
        if (panels == null || panels.Count == 0)
            Debug.LogWarning("[Comic] Play() called but Panels list is empty - nothing will show.");

        StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        isPlaying = true;

        if (silenceAmbience) RecoverySoundscape.SetAmbienceSuppressed(true);
        if (!string.IsNullOrEmpty(musicId))
        {
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PlayMusic(musicId, musicFadeIn);
                musicStartedByMe = true;
            }
            else Debug.LogWarning("[Comic] Music Id set but there's no AudioManager in the scene.");
        }

        if (stripContainer != null)
        {
            stripContainer.gameObject.SetActive(true);
        }
        // cutsceneLocked, NOT movementLocked - StoryEnvironmentController owns movementLocked
        // and unconditionally sets it to (phase == BedRest) whenever its own phase-changed
        // check runs, which was silently un-locking movement here the instant that happened
        // to fire right after this line - see PlayerMovement.cutsceneLocked's own comment.
        if (player != null) player.cutsceneLocked = true;

        if (cameraFollow != null)
        {
            cameraOrbitWasEnabled = cameraFollow.enableOrbitRotation;
            cameraFollow.enableOrbitRotation = false;
        }

        foreach (RectTransform panel in panels)
            if (panel != null) panel.gameObject.SetActive(false);

        currentIndex = -1;
        highestRevealedIndex = -1;
        skipHoldTimer = 0f;
        bool horizontalArmed = true;

        // Wait one frame before checking input — otherwise the button press that
        // triggered this (or the wake-up moment itself) could immediately register.
        yield return null;


        RevealAndFocus(0);

        bool finished = false;
        while (!finished)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                finished = true;
                break;
            }

            // Hold-to-skip on gamepad B — GetKeyDown only fires once on the initial
            // press, so this needs GetKey (held-this-frame) tracked over time instead.
            if (Input.GetKey(KeyCode.JoystickButton1))
            {
                skipHoldTimer += Time.deltaTime;
                if (skipHoldTimer >= holdToSkipDuration)
                {
                    finished = true;
                    break;
                }
            }
            else
            {
                skipHoldTimer = 0f;
            }

            float h = ReadHorizontal();
            if (Mathf.Abs(h) < cycleThreshold)
            {
                horizontalArmed = true;
            }
            else if (horizontalArmed)
            {
                horizontalArmed = false;
                if (h > 0f)
                {
                    if (currentIndex >= panels.Count - 1)
                    {
                        finished = true; // moved past the last panel - done
                    }
                    else
                        RevealAndFocus(currentIndex + 1);
                }
                else if (currentIndex > 0)
                {
                    RevealAndFocus(currentIndex - 1);
                }
            }

            yield return null;
        }

        EndCutscene();
    }

    // Reads every plausible left/right source and returns the strongest one.
    private static float ReadHorizontal()
    {
        float h = 0f;
        try { h = Input.GetAxisRaw("Horizontal"); } catch (System.Exception) { }
        try
        {
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h = 1f;
            else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) h = -1f;
        }
        catch (System.Exception) { }
#if ENABLE_INPUT_SYSTEM
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null)
        {
            float x = pad.leftStick.x.ReadValue();
            if (pad.dpad.right.isPressed) x = 1f;
            else if (pad.dpad.left.isPressed) x = -1f;
            if (Mathf.Abs(x) > Mathf.Abs(h)) h = x;
        }
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h = 1f;
            else if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h = -1f;
        }
#endif
        return h;
    }

    private void RevealAndFocus(int index)
    {
        if (index < 0 || index >= panels.Count) return;

        currentIndex = index;

        if (index > highestRevealedIndex)
        {
            highestRevealedIndex = index;
            if (panels[index] != null) panels[index].gameObject.SetActive(true);
        }


        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveStripToCenter(panels[index]));
    }

    /// <summary>
    /// Moves stripContainer (never the panel itself) so the target panel's pivot
    /// lands at Y=0 in the strip's parent space — which, assuming that parent is
    /// a full-screen centered container, is screen center. Only Y changes.
    /// </summary>
    private IEnumerator MoveStripToCenter(RectTransform target)
    {
        if (stripContainer == null || target == null)
        {
            Debug.LogWarning($"[Comic] MoveStripToCenter aborted — stripContainer null: " +
                              $"{stripContainer == null}, target null: {target == null}");
            yield break;
        }

        // Work out where the panel really is, whatever its anchors / layout - not just its anchoredPosition,
        // which is only comparable between panels that share the same anchors.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(stripContainer);

        float startY = stripContainer.anchoredPosition.y;
        Transform space = stripContainer.parent != null ? stripContainer.parent : stripContainer;
        float panelY = space.InverseTransformPoint(target.position).y;
        float targetY = startY - panelY;


        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            t = 1f - (1f - t) * (1f - t); // ease out

            Vector2 pos = stripContainer.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, t);
            stripContainer.anchoredPosition = pos;
            yield return null;
        }

        Vector2 finalPos = stripContainer.anchoredPosition;
        finalPos.y = targetY;
        stripContainer.anchoredPosition = finalPos;
    }

    private void OnDisable()
    {
        // Safety: never leave the ambience muted if this object is switched off mid-cutscene.
        if (isPlaying && silenceAmbience) RecoverySoundscape.SetAmbienceSuppressed(false);
    }

    private void EndCutscene()
    {
        if (silenceAmbience) RecoverySoundscape.SetAmbienceSuppressed(false);
        if (musicStartedByMe && AudioManager.instance != null)
            AudioManager.instance.StopMusic(musicFadeOut);
        musicStartedByMe = false;

        if (stripContainer != null) stripContainer.gameObject.SetActive(false);
        if (player != null) player.cutsceneLocked = false;
        if (cameraFollow != null) cameraFollow.enableOrbitRotation = cameraOrbitWasEnabled;

        isPlaying = false;
        OnCutsceneComplete?.Invoke();
    }
}