using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Put on the win / lose / injured panel. Every time the panel is switched on it fills its texts from FightStats
/// and works out the grade. Leave any text empty to skip it.
/// Win: B, A, S (flawless within S Time), SS (flawless within SS Time).  Lose: C or B.  Injured: F or D.
/// "Flawless" = won without the enemy scoring. A / B / D split on the performance score (0-100):
/// share of the points you scored, plus perfect parries and swaps.
/// </summary>
public class BattleReportUI : MonoBehaviour
{
    [Header("Texts (all optional)")]
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text timeText;
    [Tooltip("Final score, e.g. 10 - 2 (yours first).")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private string scoreFormat = "{0} - {1}";
    [SerializeField] private TMP_Text hitsDealtText;
    [SerializeField] private TMP_Text hitsTakenText;
    [SerializeField] private TMP_Text swapsText;
    [SerializeField] private TMP_Text perfectParriesText;
    [SerializeField] private TMP_Text normalBlocksText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text motivationText;

    [System.Serializable]
    public class RevealStep
    {
        [Tooltip("Everything that pops in together for this step, e.g. the label AND its value text.")]
        public GameObject[] objects;
    }

    [Header("Pop-in (steps show one by one, in this order)")]
    [Tooltip("Suggested order: Time, Hits Dealt, Hits Taken, Swaps, Perfect Parries, Grade, Health, Motivation. " +
             "Anything NOT listed here is just shown straight away. Leave empty for no animation.")]
    [SerializeField] private RevealStep[] steps;
    [SerializeField] private float firstDelay = 0.4f;
    [SerializeField] private float stepInterval = 0.4f;
    [Tooltip("Extra pause before the grade (step number below, counting from 1). 0 = none.")]
    [SerializeField] private int gradeStep = 6;
    [SerializeField] private float gradePause = 0.4f;
    [SerializeField] private float popTime = 0.25f;
    [Tooltip("How far past full size it overshoots.")]
    [SerializeField] private float popOvershoot = 1.25f;
    [Tooltip("SoundLibrary id played on each pop. Blank = none.")]
    [SerializeField] private string popSoundId;
    [Tooltip("SoundLibrary id played when the grade pops. Blank = use the pop sound.")]
    [SerializeField] private string gradeSoundId;

    [Header("Grading")]
    [SerializeField] private float ssTime = 20f;
    [SerializeField] private float sTime = 45f;
    [Tooltip("Performance score needed for A on a win / B on a loss.")]
    [SerializeField] private float goodScore = 65f;
    [Tooltip("Performance score needed for D on an injury (below it is F).")]
    [SerializeField] private float injuredPassScore = 40f;
    [Tooltip("Score added per perfect parry, up to Max Parries.")]
    [SerializeField] private float pointsPerParry = 4f;
    [SerializeField] private int maxParries = 5;
    [Tooltip("Score added per swap, up to Max Swaps.")]
    [SerializeField] private float pointsPerSwap = 10f;
    [SerializeField] private int maxSwaps = 2;

    [Header("Colours")]
    [SerializeField] private Color gradeSS = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color gradeS = new Color(1f, 0.7f, 0.2f);
    [SerializeField] private Color gradeGood = Color.white;
    [SerializeField] private Color gradeBad = new Color(0.8f, 0.3f, 0.3f);

    /// <summary>Set by PostFightHandler for the one panel that should show no report (the fight that finishes the game).</summary>
    public static bool SkipNext;

    [Tooltip("Optional: a parent object holding all the report rows. Hidden when the report is skipped.")]
    [SerializeField] private GameObject reportRoot;

    private void HideReport()
    {
        if (reportRoot != null) reportRoot.SetActive(false);
        foreach (TMP_Text t in new[] { gradeText, timeText, scoreText, hitsDealtText, hitsTakenText, swapsText, perfectParriesText, normalBlocksText, healthText, motivationText })
            if (t != null) t.gameObject.SetActive(false);
        if (steps != null)
            foreach (RevealStep st in steps)
                if (st != null && st.objects != null)
                    foreach (GameObject o in st.objects) if (o != null) o.SetActive(false);
    }

    private void OnEnable()
    {
        if (SkipNext) { SkipNext = false; HideReport(); return; }
        if (reportRoot != null) reportRoot.SetActive(true);
        foreach (TMP_Text t in new[] { gradeText, timeText, scoreText, hitsDealtText, hitsTakenText, swapsText, perfectParriesText, normalBlocksText, healthText, motivationText })
            if (t != null) t.gameObject.SetActive(true);
        bool won = FightSessionManager.wonLastFight;
        bool injured = FightSessionManager.injuredLastFight && !won;

        int total = FightStats.playerHits + FightStats.enemyHits;
        float share = total > 0 ? FightStats.playerHits / (float)total : 0.5f;
        float score = share * 60f
                    + Mathf.Min(FightStats.perfectParries, maxParries) * pointsPerParry
                    + Mathf.Min(FightStats.swaps, maxSwaps) * pointsPerSwap;

        string grade;
        if (won)
        {
            bool flawless = FightStats.enemyHits == 0;
            if (flawless && FightStats.time <= ssTime) grade = "SS";
            else if (flawless && FightStats.time <= sTime) grade = "S";
            else grade = score >= goodScore ? "A" : "B";
        }
        else if (injured) grade = score >= injuredPassScore ? "D" : "F";
        else grade = score >= goodScore ? "B" : "C";

        Set(gradeText, grade);
        if (gradeText != null)
            gradeText.color = grade == "SS" ? gradeSS : grade == "S" ? gradeS : (grade == "F" || grade == "D" || grade == "C") ? gradeBad : gradeGood;

        int m = Mathf.FloorToInt(FightStats.time / 60f), s = Mathf.FloorToInt(FightStats.time % 60f);
        Set(timeText, m.ToString("00") + ":" + s.ToString("00"));
        Set(scoreText, string.Format(scoreFormat, FightStats.playerScore, FightStats.enemyScore));
        Set(hitsDealtText, FightStats.playerHits.ToString());
        Set(hitsTakenText, FightStats.enemyHits.ToString());
        Set(swapsText, FightStats.swaps.ToString());
        Set(perfectParriesText, FightStats.perfectParries.ToString());
        Set(normalBlocksText, FightStats.normalBlocks.ToString());
        Set(healthText, Signed(FightStats.healthDelta));
        Set(motivationText, Signed(FightStats.motivationDelta));

        StopAllCoroutines();
        if (steps != null && steps.Length > 0)
        {
            foreach (RevealStep st in steps)
                if (st != null && st.objects != null)
                    foreach (GameObject o in st.objects) if (o != null) o.SetActive(false);
            StartCoroutine(Reveal());
        }
    }

    private IEnumerator Reveal()
    {
        yield return new WaitForSecondsRealtime(firstDelay);
        for (int i = 0; i < steps.Length; i++)
        {
            if (gradeStep == i + 1 && gradePause > 0f) yield return new WaitForSecondsRealtime(gradePause);
            RevealStep st = steps[i];
            if (st != null && st.objects != null)
            {
                bool isGrade = gradeStep == i + 1 && !string.IsNullOrEmpty(gradeSoundId);
                Sfx.Play(isGrade ? gradeSoundId : popSoundId);
                foreach (GameObject o in st.objects)
                    if (o != null) StartCoroutine(Pop(o.transform, o));
            }
            yield return new WaitForSecondsRealtime(stepInterval);
        }
    }

    private IEnumerator Pop(Transform t, GameObject o)
    {
        Vector3 full = t.localScale;
        o.SetActive(true);
        float e = 0f;
        while (e < popTime)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / Mathf.Max(0.01f, popTime));
            // ease out back: overshoots then settles
            float c = popOvershoot * 1.70158f / 1.25f;
            float p = 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f);
            t.localScale = full * p;
            yield return null;
        }
        t.localScale = full;
    }

    private static void Set(TMP_Text t, string v) { if (t != null) t.text = v; }

    private static string Signed(float v)
    {
        string n = v % 1f == 0f ? ((int)v).ToString() : v.ToString("0.#");
        return v > 0f ? "+" + n : n;
    }
}