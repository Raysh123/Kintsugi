using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Floating "+10 Health / -5 Motivation" text shown whenever the player's Health or Motivation changes.
/// It listens to HealthAndMotivation's change events, works out the difference itself and groups changes that happen
/// together into one popup, so no other script needs touching.
/// Put it on a UI object in the RPG scene with a CanvasGroup and a TMP text (centre-aligned, rich text on).
/// Keep the object ACTIVE; it hides itself.
/// </summary>
public class ActivityResultPopup : MonoBehaviour
{
    public static ActivityResultPopup instance;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text text;
    [SerializeField] private string healthLabel = "Health";
    [SerializeField] private string motivationLabel = "Motivation";
    [SerializeField] private Color gainColor = new Color(0.45f, 0.9f, 0.45f);
    [SerializeField] private Color lossColor = new Color(0.95f, 0.35f, 0.35f);
    [SerializeField] private float popTime = 0.25f;
    [SerializeField] private float popOvershoot = 1.25f;
    [SerializeField] private float hold = 2f;
    [SerializeField] private float fadeOut = 0.6f;
    [Tooltip("SoundLibrary ids played when the text appears. Blank = none.")]
    [SerializeField] private string gainSoundId;
    [SerializeField] private string lossSoundId;

    [Tooltip("Changes smaller than this are ignored.")]
    [SerializeField] private float minChange = 0.5f;

    private Coroutine running;
    private HealthAndMotivation bound;
    private float lastH, lastM, pendingH, pendingM;
    private bool collecting;
    private Vector3 fullScale = Vector3.one;

    private void Awake()
    {
        instance = this;
        if (text != null) fullScale = text.transform.localScale;
        if (group != null) group.alpha = 0f;
    }

    private void OnEnable() => StartCoroutine(Bind());

    private void OnDisable()
    {
        if (bound == null) return;
        bound.OnHealthChanged -= OnHealth;
        bound.OnMotivationChanged -= OnMotivation;
        bound = null;
    }

    private IEnumerator Bind()
    {
        while (HealthAndMotivation.instance == null) yield return null;
        bound = HealthAndMotivation.instance;
        lastH = bound.currentHealth;
        lastM = bound.currentMotivation;
        bound.OnHealthChanged += OnHealth;
        bound.OnMotivationChanged += OnMotivation;
    }

    private void OnHealth(float value) { pendingH += value - lastH; lastH = value; Collect(); }
    private void OnMotivation(float value) { pendingM += value - lastM; lastM = value; Collect(); }

    private void Collect()
    {
        if (!collecting) StartCoroutine(CollectRoutine());
    }

    // Wait a frame so a health change and a motivation change made together show as one popup.
    private IEnumerator CollectRoutine()
    {
        collecting = true;
        yield return null;
        float h = Mathf.Abs(pendingH) >= minChange ? pendingH : 0f;
        float m = Mathf.Abs(pendingM) >= minChange ? pendingM : 0f;
        pendingH = pendingM = 0f;
        collecting = false;
        Display(h, m);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Display(float health, float motivation)
    {
        if (text == null || (health == 0f && motivation == 0f)) return;
        var sb = new StringBuilder();
        Line(sb, health, healthLabel);
        Line(sb, motivation, motivationLabel);
        text.text = sb.ToString().TrimEnd('\n');
        Sfx.Play(health + motivation >= 0f ? gainSoundId : lossSoundId);
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run());
    }

    private void Line(StringBuilder sb, float v, string label)
    {
        if (v == 0f) return;
        string n = v % 1f == 0f ? ((int)Mathf.Abs(v)).ToString() : Mathf.Abs(v).ToString("0.#");
        Color c = v > 0f ? gainColor : lossColor;
        sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>')
          .Append(v > 0f ? "+" : "-").Append(n).Append(' ').Append(label).Append("</color>\n");
    }

    private IEnumerator Run()
    {
        Transform t = text.transform;
        if (group != null) group.alpha = 1f;
        float e = 0f;
        while (e < popTime)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / Mathf.Max(0.01f, popTime));
            float c = popOvershoot * 1.70158f / 1.25f;
            t.localScale = fullScale * (1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f));
            yield return null;
        }
        t.localScale = fullScale;
        yield return new WaitForSecondsRealtime(hold);
        e = 0f;
        while (e < fadeOut)
        {
            e += Time.unscaledDeltaTime;
            if (group != null) group.alpha = 1f - Mathf.Clamp01(e / Mathf.Max(0.01f, fadeOut));
            yield return null;
        }
        if (group != null) group.alpha = 0f;
        text.text = "";
        running = null;
    }
}
