using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen date and time of day. Reads CalendarManager (the persistent singleton), so it is the same in every scene.
/// Assign whichever of the texts / icon you built - anything left empty is skipped.
///
///   * Date text: "13th Saturday" style (no month or year). Set the start date number and weekday below.
///   * Day number text: "Day 1, Day 2 ..." counted from the story's start date.
///   * Time of day text + icon: Morning / Afternoon / Evening / Night, with an icon per phase.
/// It updates itself the moment the date or time of day changes, with a small pop.
/// </summary>
public class CalendarHUD : MonoBehaviour
{
    [System.Serializable]
    public class PhaseLook
    {
        public DayPhase phase;
        [Tooltip("Text shown for this time of day.")]
        public string label = "Morning";
        [Tooltip("Optional icon for this time of day (sun, moon...).")]
        public Sprite icon;
        [Tooltip("Tint for the time-of-day text and icon.")]
        public Color tint = Color.white;
    }

    [Header("Date")]
    [SerializeField] private TMP_Text dateText;
    [Tooltip("{0} = date number with ordinal (13th), {1} = weekday (Saturday).")]
    [SerializeField] private string dateFormat = "{0} {1}";
    [Tooltip("Date number of the story's first day.")]
    [SerializeField] private int startDateNumber = 1;
    [Tooltip("Weekday of the story's first day.")]
    [SerializeField] private System.DayOfWeek startWeekday = System.DayOfWeek.Monday;
    [Tooltip("Date number wraps back to 1 after this many days. 0 = never wraps.")]
    [SerializeField] private int daysInMonth = 30;
    [SerializeField] private TMP_Text dayNumberText;
    [Tooltip("{0} = the day number, counting the first day as 1.")]
    [SerializeField] private string dayNumberFormat = "Day {0}";

    [Header("Time of day")]
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private Image phaseIcon;
    [SerializeField]
    private PhaseLook[] phases =
    {
        new PhaseLook { phase = DayPhase.Morning,   label = "Morning" },
        new PhaseLook { phase = DayPhase.Afternoon, label = "Afternoon" },
        new PhaseLook { phase = DayPhase.Evening,   label = "Evening" },
        new PhaseLook { phase = DayPhase.Night,     label = "Night" },
    };

    [Header("Pop on change")]
    [SerializeField] private float popScale = 1.12f;
    [SerializeField] private float popDuration = 0.3f;

    private int shownDay = -1;
    private int shownPhase = -1;
    private Vector3 baseScale;
    private float popT = 1f;

    private void Awake() { baseScale = transform.localScale; }

    private void OnEnable() { shownDay = -1; shownPhase = -1; }

    private void Update()
    {
        CalendarManager cal = CalendarManager.instance;
        if (cal == null) return;

        bool changed = false;

        int day = DayNumber(cal);
        if (day != shownDay)
        {
            bool first = shownDay < 0;
            shownDay = day;
            int n = day - 1;
            int dateNum = startDateNumber + n;
            if (daysInMonth > 0) dateNum = (startDateNumber - 1 + n) % daysInMonth + 1;
            string weekday = ((System.DayOfWeek)(((int)startWeekday + n) % 7)).ToString();
            if (dateText != null) dateText.text = string.Format(dateFormat, Ordinal(dateNum), weekday);
            if (dayNumberText != null) dayNumberText.text = string.Format(dayNumberFormat, day);
            changed = !first;
        }

        int phase = (int)cal.CurrentPhase;
        if (phase != shownPhase)
        {
            bool first = shownPhase < 0;
            shownPhase = phase;
            ApplyPhase(cal.CurrentPhase);
            changed |= !first;
        }

        if (changed) popT = 0f;
        if (popT < 1f)
        {
            popT += Time.unscaledDeltaTime / Mathf.Max(0.01f, popDuration);
            float k = Mathf.Sin(Mathf.Clamp01(popT) * Mathf.PI);
            transform.localScale = baseScale * Mathf.Lerp(1f, popScale, k);
            if (popT >= 1f) transform.localScale = baseScale;
        }
    }

    private void ApplyPhase(DayPhase current)
    {
        foreach (PhaseLook look in phases)
        {
            if (look == null || look.phase != current) continue;
            if (phaseText != null) { phaseText.text = look.label; phaseText.color = look.tint; }
            if (phaseIcon != null)
            {
                phaseIcon.sprite = look.icon;
                phaseIcon.color = look.tint;
                phaseIcon.enabled = look.icon != null;
            }
            return;
        }
    }

    private static string Ordinal(int n)
    {
        int t = n % 100;
        if (t >= 11 && t <= 13) return n + "th";
        switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
    }

    private static int DayNumber(CalendarManager cal)
    {
        if (StoryManager.instance == null) return 1;
        GameDate start = StoryManager.instance.storyStartDate;
        GameDate now = cal.CurrentDate;
        for (int n = 0; n <= 2000; n++)
            if (start.AddDays(n) >= now) return n + 1;
        return 1;
    }
}