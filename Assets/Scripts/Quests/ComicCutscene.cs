using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Movement")]
    [SerializeField] private float moveDuration = 0.3f;

    [Header("Skip")]
    [Tooltip("Escape skips instantly on keyboard. On gamepad, holding B for this long also skips.")]
    [SerializeField] private float holdToSkipDuration = 1f;

    private int currentIndex = -1;
    private int highestRevealedIndex = -1;
    private bool isPlaying;
    private bool cameraOrbitWasEnabled;
    private Coroutine moveCoroutine;
    private float skipHoldTimer;

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
        StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        isPlaying = true;

        if (stripContainer != null) stripContainer.gameObject.SetActive(true);
        if (player != null) player.movementLocked = true;

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

            // GetButtonDown debounces a continuous stick axis into a discrete
            // "just pressed" edge automatically, same as TravelMapController's
            // cycling — no manual analog tracking needed.
            if (Input.GetButtonDown("Horizontal"))
            {
                float h = Input.GetAxisRaw("Horizontal");
                if (h > 0f)
                {
                    if (currentIndex >= panels.Count - 1)
                        finished = true; // moved past the last panel - done
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

    private void RevealAndFocus(int index)
    {
        if (index < 0 || index >= panels.Count) return;

        currentIndex = index;

        if (index > highestRevealedIndex)
        {
            highestRevealedIndex = index;
            if (panels[index] != null) panels[index].gameObject.SetActive(true);
        }

        Debug.Log($"[Comic] RevealAndFocus({index}) — panel Y = " +
                  $"{(panels[index] != null ? panels[index].anchoredPosition.y.ToString("F1") : "NULL PANEL")}, " +
                  $"stripContainer = {(stripContainer != null ? "assigned" : "NULL")}");

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

        float targetY = -target.anchoredPosition.y;
        float startY = stripContainer.anchoredPosition.y;

        Debug.Log($"[Comic] Moving strip from Y={startY:F1} to Y={targetY:F1}");

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

    private void EndCutscene()
    {
        if (stripContainer != null) stripContainer.gameObject.SetActive(false);
        if (player != null) player.movementLocked = false;
        if (cameraFollow != null) cameraFollow.enableOrbitRotation = cameraOrbitWasEnabled;

        isPlaying = false;
        OnCutsceneComplete?.Invoke();
    }
}