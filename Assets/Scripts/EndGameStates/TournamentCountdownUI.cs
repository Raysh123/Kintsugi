using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// "X days till the tournament" banner. Pops up every morning during TournamentPrep -
/// however the morning arrives (sleeping, or an activity that rolls the clock over).
/// It listens to CalendarManager.OnPhaseChanged (-> Morning), so nothing else needs to call it.
///
/// Put it in the RPG scene (any object), assign the panel's CanvasGroup + a TMP text.
/// The panel can start enabled or disabled; it fades itself in and out and never blocks input.
/// </summary>
public class TournamentCountdownUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The banner's CanvasGroup (alpha is animated). Keep the GameObject itself active.")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text text;

    [Header("Wording")]
    [SerializeField] private string manyDays = "{0} days till the tournament";
    [SerializeField] private string oneDay = "1 day till the tournament";
    [SerializeField] private string today = "The tournament is today!";

    [Header("Timing")]
    [SerializeField] private float fadeIn = 0.6f;
    [SerializeField] private float hold = 3f;
    [SerializeField] private float fadeOut = 0.8f;
    [Tooltip("If a dialogue is open when morning arrives, wait for it (up to this many seconds).")]
    [SerializeField] private float maxWaitForDialogue = 15f;
    [Tooltip("Optional SoundLibrary id played when the banner appears.")]
    [SerializeField] private string sfxId;

    private bool subscribed;
    private Coroutine running;

    private void OnEnable() => StartCoroutine(Subscribe());

    private void OnDisable()
    {
        if (subscribed && CalendarManager.instance != null)
            CalendarManager.instance.OnPhaseChanged -= OnPhaseChanged;
        subscribed = false;
    }

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
    }

    private IEnumerator Subscribe()
    {
        while (CalendarManager.instance == null) yield return null;
        CalendarManager.instance.OnPhaseChanged += OnPhaseChanged;
        subscribed = true;
    }

    private void OnPhaseChanged(DayPhase phase)
    {
        if (phase != DayPhase.Morning) return;
        if (StoryManager.instance == null || StoryManager.instance.CurrentPhase != StoryPhase.TournamentPrep) return;
        if (StoryManager.instance.BothPartnersBeaten) return;   // the game is ending
        if (!StoryManager.instance.TournamentDateSet) return;   // not set until the sensei talk
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Show());
    }

    private IEnumerator Show()
    {
        // Let the calendar finish updating the date before we read it.
        yield return null;

        int days = DaysLeft();
        if (days < 0) { running = null; yield break; }

        float wait = 0f;
        while (ActiveDialogueTracker.AnyActive && wait < maxWaitForDialogue)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null) text.text = days == 0 ? today : days == 1 ? oneDay : string.Format(manyDays, days);
        if (!string.IsNullOrEmpty(sfxId)) Sfx.Play(sfxId);

        yield return FadeTo(1f, fadeIn);
        yield return new WaitForSecondsRealtime(hold);
        yield return FadeTo(0f, fadeOut);
        running = null;
    }

    /// <summary>Whole days from today to the tournament date (0 = today), or -1 if unknown.</summary>
    private int DaysLeft()
    {
        if (CalendarManager.instance == null || StoryManager.instance == null) return -1;
        GameDate now = CalendarManager.instance.CurrentDate;
        GameDate target = StoryManager.instance.GetTournamentDate();
        for (int n = 0; n <= 400; n++)
            if (now.AddDays(n) >= target) return n;
        return -1;
    }

    private IEnumerator FadeTo(float target, float time)
    {
        if (group == null) yield break;
        float from = group.alpha, t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / Mathf.Max(0.01f, time)));
            yield return null;
        }
        group.alpha = target;
    }
}