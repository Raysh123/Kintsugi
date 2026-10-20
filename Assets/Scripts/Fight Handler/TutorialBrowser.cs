using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// A "how to play" screen made of PAGES (Dodge, Block, Perfect block, ...). Each page has a title, a text, the controls and a LOOPING
/// video clip (or a still picture) - like an ability screen in a console game.
///
///   Left stick / D-pad / Left-Right arrows : previous / next page
///   A (or Enter / Space)                   : next page (on the last page: close)
///   B (or Esc / Backspace)                 : close
///
/// It works while the game is paused (everything runs on real time), and it can be used from ANYWHERE:
///   * The TutorialDirector shows it before the fight (step "Browse Tutorial").
///   * A menu button can open it again: button OnClick -> TutorialBrowser.Open().
///
/// UI setup (Canvas): a panel (assign it to Panel Group - a CanvasGroup) containing
///   Title (TMP), Body (TMP), Controls (TMP, optional), Page Counter (TMP, optional), Tabs (TMP, optional list of all page names with the
///   current one highlighted), Hint (TMP, optional "Stick: browse  A: next  B: close"),
///   a RawImage for the video (Video Image) and a Video Player (Render Mode = Render Texture, Target Texture = a RenderTexture asset that the
///   RawImage shows; Play On Awake OFF), and optionally an Image for pages that use a picture instead (Still Image).
/// Keep the panel's object active or inactive - it is switched on while open.
/// </summary>
public class TutorialBrowser : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public string title = "Page";
        [TextArea(2, 6)] public string body;
        [Tooltip("What to press, e.g. \"Gamepad: B     Keyboard: L\". Blank = no controls line.")]
        public string controls;
        [Tooltip("A picture of the buttons instead of (or as well as) the text - e.g. the B button icon, or one image showing Up + B. " +
                 "Blank = no icon.")]
        public Sprite controlsIcon;
        [Tooltip("Looping demo clip for this page (drag a video file here).")]
        public VideoClip clip;
        [Tooltip("Used instead when there is no clip.")]
        public Sprite image;
    }

    [Header("Pages")]
    [SerializeField] private List<Page> pages = new List<Page>();

    [Header("UI")]
    [SerializeField] private CanvasGroup panelGroup;
    [Tooltip("Optional: the part that fades a little when the page changes (usually the same panel).")]
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text controlsText;
    [Tooltip("Optional: an Image that shows the page's Controls Icon (switched off when the page has none).")]
    [SerializeField] private Image controlsImage;
    [SerializeField] private TMP_Text pageCounterText;
    [Tooltip("Optional: all page titles in one line / column, the current one highlighted.")]
    [SerializeField] private TMP_Text tabsText;
    [SerializeField] private string tabsSeparator = "     ";
    [SerializeField] private Color tabActiveColor = new Color(1f, 0.84f, 0.3f);
    [SerializeField] private Color tabIdleColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private string hintString = "Left / Right: browse      A: next      B: close";
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;
    private RenderTexture createdTexture;
    [SerializeField] private Image stillImage;

    [Header("Behaviour")]
    [Tooltip("Freeze the game while it is open (it runs on real time, so it still works).")]
    [SerializeField] private bool pauseGame = true;
    [Tooltip("Stop the player's fight controls and the menu's own navigation while it is open.")]
    [SerializeField] private bool lockOtherInput = true;
    [SerializeField] private bool wrapAround = false;
    [Tooltip("Ignore buttons for this long after opening (so the press that opened it doesn't also close it).")]
    [SerializeField] private float minShow = 0.3f;
    [SerializeField] private float stickThreshold = 0.5f;
    [Tooltip("Holding the stick: wait this long, then repeat every 'Repeat Rate' seconds.")]
    [SerializeField] private float repeatDelay = 0.4f;
    [SerializeField] private float repeatRate = 0.22f;
    [SerializeField] private float fadeTime = 0.18f;

    public UnityEvent OnOpened;
    public UnityEvent OnClosed;

    public bool IsOpen { get; private set; }

    private int index;
    private int swapId;
    private int heldDir;
    private float nextRepeat;

    private void Awake()
    {
        if (panelGroup != null && !IsOpen) { panelGroup.alpha = 0f; panelGroup.gameObject.SetActive(false); }
    }

    /// <summary>For a menu button (OnClick) or any UnityEvent.</summary>
    public void Open()
    {
        if (!IsOpen) StartCoroutine(Show());
    }

    /// <summary>Opens the browser and finishes when the player closes it. The TutorialDirector waits on this.</summary>
    public IEnumerator Show()
    {
        if (IsOpen) yield break;
        if (pages == null || pages.Count == 0) FillDefaultPages();

        IsOpen = true;
        float prevScale = Time.timeScale;
        if (pauseGame) Time.timeScale = 0f;
        if (lockOtherInput) InputHandler.instance?.SetInput(false);
        EventSystem es = EventSystem.current;
        bool prevNav = es != null && es.sendNavigationEvents;
        if (lockOtherInput && es != null) es.sendNavigationEvents = false;

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;   // plays even while the game is paused
        }

        if (panelGroup != null) { panelGroup.gameObject.SetActive(true); panelGroup.alpha = 0f; }
        index = 0; heldDir = 0;
        ApplyPage(index);
        OnOpened?.Invoke();
        yield return Fade(panelGroup, 1f, fadeTime);

        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            if (t >= minShow)
            {
                if (ClosePressed()) break;
                int dir = ReadNav();
                if (dir != 0) GoTo(index + dir);
                else if (NextPressed())
                {
                    if (index >= pages.Count - 1) break;
                    GoTo(index + 1);
                }
            }
            yield return null;
        }

        yield return Fade(panelGroup, 0f, fadeTime);
        if (videoPlayer != null) videoPlayer.Stop();
        yield return null;   // let the closing press pass before anything else reads input
        if (es != null) es.sendNavigationEvents = prevNav;
        if (lockOtherInput) InputHandler.instance?.SetInput(true);
        if (pauseGame) Time.timeScale = prevScale > 0f ? prevScale : 1f;
        IsOpen = false;
        OnClosed?.Invoke();

        // Hidden LAST: if this script sits on the panel's own object, hiding it ends this coroutine, so everything
        // above has to be finished first.
        if (panelGroup != null) panelGroup.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ pages
    private void GoTo(int i)
    {
        int n = pages.Count;
        if (wrapAround) i = (i % n + n) % n;
        else i = Mathf.Clamp(i, 0, n - 1);
        if (i == index) return;
        index = i;
        StartCoroutine(SwapPage(++swapId));
    }

    private IEnumerator SwapPage(int id)
    {
        if (contentGroup != null)
        {
            float a = contentGroup.alpha;
            float t = 0f, d = Mathf.Max(0.01f, fadeTime * 0.5f);
            while (t < d && id == swapId) { t += Time.unscaledDeltaTime; contentGroup.alpha = Mathf.Lerp(a, 0f, t / d); yield return null; }
        }
        if (id != swapId) yield break;
        ApplyPage(index);
        if (contentGroup != null)
        {
            float t = 0f, d = Mathf.Max(0.01f, fadeTime * 0.5f);
            while (t < d && id == swapId) { t += Time.unscaledDeltaTime; contentGroup.alpha = Mathf.Lerp(0f, 1f, t / d); yield return null; }
            if (id == swapId) contentGroup.alpha = 1f;
        }
    }

    private void ApplyPage(int i)
    {
        Page p = pages[i];
        if (titleText != null) titleText.text = p.title;
        if (bodyText != null) bodyText.text = p.body;
        if (controlsText != null) { controlsText.text = p.controls; controlsText.gameObject.SetActive(!string.IsNullOrEmpty(p.controls)); }
        if (controlsImage != null)
        {
            controlsImage.sprite = p.controlsIcon;
            controlsImage.preserveAspect = true;
            controlsImage.gameObject.SetActive(p.controlsIcon != null);
        }
        if (pageCounterText != null) pageCounterText.text = $"{i + 1} / {pages.Count}";
        if (hintText != null) hintText.text = hintString;
        if (contentGroup != null) contentGroup.alpha = 1f;

        if (tabsText != null)
        {
            var sb = new StringBuilder();
            string on = ColorUtility.ToHtmlStringRGBA(tabActiveColor), off = ColorUtility.ToHtmlStringRGBA(tabIdleColor);
            for (int k = 0; k < pages.Count; k++)
            {
                if (k > 0) sb.Append(tabsSeparator);
                sb.Append(k == i ? $"<color=#{on}><b>{pages[k].title}</b></color>" : $"<color=#{off}>{pages[k].title}</color>");
            }
            tabsText.text = sb.ToString();
        }

        bool hasClip = p.clip != null && videoPlayer != null;
        if (videoImage != null) videoImage.enabled = hasClip;
        if (stillImage != null) { stillImage.sprite = p.image; stillImage.enabled = !hasClip && p.image != null; }
        if (videoPlayer != null)
        {
            if (hasClip)
            {
                videoPlayer.Stop();
                EnsureVideoTexture(p.clip);
                videoPlayer.clip = p.clip;
                videoPlayer.isLooping = true;
                videoPlayer.Play();
            }
            else videoPlayer.Stop();
        }
    }

    /// <summary>Makes sure the Video Player renders into a texture the Video Image shows. If no Target Texture was
    /// assigned (or the Raw Image has none, which draws as a white box), one is created to match the clip.</summary>
    private void EnsureVideoTexture(VideoClip clip)
    {
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;

        RenderTexture target = videoPlayer.targetTexture;
        bool ownsTarget = target != null && target == createdTexture;

        if (target == null || (ownsTarget && (target.width != (int)clip.width || target.height != (int)clip.height)))
        {
            if (createdTexture != null) createdTexture.Release();

            createdTexture = new RenderTexture(Mathf.Max(2, (int)clip.width), Mathf.Max(2, (int)clip.height), 0);
            createdTexture.Create();
            videoPlayer.targetTexture = createdTexture;
            target = createdTexture;
        }

        if (videoImage != null && videoImage.texture != target) videoImage.texture = target;
    }

    private void OnDestroy()
    {
        if (createdTexture != null) createdTexture.Release();
    }

    // ------------------------------------------------------------------ input (works while the game is paused)
    private bool ClosePressed()
    {
        var gp = Gamepad.current; var kb = Keyboard.current;
        if (gp != null && gp.buttonEast.wasPressedThisFrame) return true;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) return true;
        return false;
    }

    private bool NextPressed()
    {
        var gp = Gamepad.current; var kb = Keyboard.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
        return false;
    }

    /// <summary>-1 / +1 when the stick, D-pad or arrow keys push left / right (once, then repeating while held); otherwise 0.</summary>
    private int ReadNav()
    {
        float x = 0f;
        var gp = Gamepad.current; var kb = Keyboard.current;
        if (gp != null) x += Mathf.Abs(gp.leftStick.x.ReadValue()) > Mathf.Abs(gp.dpad.x.ReadValue()) ? gp.leftStick.x.ReadValue() : gp.dpad.x.ReadValue();
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) x -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) x += 1f;
        }
        int dir = x > stickThreshold ? 1 : (x < -stickThreshold ? -1 : 0);
        float now = Time.unscaledTime;
        if (dir != heldDir)
        {
            heldDir = dir;
            if (dir != 0) { nextRepeat = now + repeatDelay; return dir; }
            return 0;
        }
        if (dir != 0 && now >= nextRepeat) { nextRepeat = now + repeatRate; return dir; }
        return 0;
    }

    private static IEnumerator Fade(CanvasGroup g, float target, float time)
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

    // ------------------------------------------------------------------ default pages
    [ContextMenu("Fill default pages")]
    private void FillDefaultPages()
    {
        pages = new List<Page>
        {
            new Page { title = "Dodge", body = "Slip out of the way of a strike. It costs nothing, but you end up further from him." },
            new Page { title = "Block", body = "Block a strike as it arrives. It stops the hit, but every block adds to your posture bar." },
            new Page { title = "Perfect block", body = "Block at the very last moment - his trail turns red - and you parry him. He is left open: step in and punch." },
            new Page { title = "Below the belt", body = "Kicks to the legs score no points, but they still fill your posture bar." },
            new Page { title = "Swap", body = "Against a PUNCH, swap places with him and push him away. Push him off the mat and the referee gives HIM a warning." },
            new Page { title = "Posture and stun", body = "Perfect blocks fill HIS posture bar; blocks and leg kicks fill yours. A full bar means STUNNED: no blocking, no dodging. Break his posture, then hit him." },
        };
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}