using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen points (and optionally the clock). Listens to MatchScore.OnScoreChanged in code,
/// so no Inspector event wiring is needed - just assign the references.
///
/// Setup: on your fight Canvas, make two TMP_Text elements (player score, opponent score) and
/// optionally a third for the timer; put this on the Canvas (or any object) and assign them
/// plus the scene's MatchScore (and MatchTimer for the clock). Labels like "PLAYER" / "OPPONENT"
/// are just separate static text objects - this only drives the numbers.
/// </summary>
public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private MatchScore matchScore;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text enemyScoreText;

    [Header("Score bars (optional) - fill toward the winning score")]
    [Tooltip("Either a Slider...")]
    [SerializeField] private Slider playerSlider;
    [SerializeField] private Slider enemySlider;
    [Tooltip("...or an Image with Image Type = Filled. Use whichever you built; empty is skipped.")]
    [SerializeField] private Image playerFillImage;
    [SerializeField] private Image enemyFillImage;
    [Tooltip("Used as the bar's full value only if MatchScore's Points To Win is 0 (rule off).")]
    [SerializeField] private int fallbackMaxScore = 10;
    [Tooltip("How fast the bars catch up (fraction of the bar per second). 0 = snap.")]
    [SerializeField] private float barSmoothSpeed = 2f;

    [Header("Clock (optional)")]
    [SerializeField] private MatchTimer matchTimer;
    [SerializeField] private TMP_Text timerText;
    [Tooltip("ON = show just the seconds left (e.g. 87) instead of minutes:seconds (01:27).")]
    [SerializeField] private bool secondsOnly = true;

    [Header("Score pop")]
    [Tooltip("Scale a score number briefly pops to when it increases. 1 = no pop.")]
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private float popDuration = 0.35f;

    private int lastPlayer, lastEnemy;
    private float shownPlayer, shownEnemy;   // 0-1, what the bars currently show

    private void OnEnable()
    {
        if (matchScore == null) return;
        matchScore.OnScoreChanged.AddListener(Refresh);
        lastPlayer = matchScore.PlayerScore;
        lastEnemy = matchScore.EnemyScore;
        SetTexts(lastPlayer, lastEnemy);
        shownPlayer = Fraction(lastPlayer);
        shownEnemy = Fraction(lastEnemy);
        ApplyBars();
    }

    private void OnDisable()
    {
        if (matchScore != null) matchScore.OnScoreChanged.RemoveListener(Refresh);
    }

    private float Fraction(int score)
    {
        int max = matchScore != null && matchScore.PointsToWin > 0 ? matchScore.PointsToWin : fallbackMaxScore;
        return max <= 0 ? 0f : Mathf.Clamp01((float)score / max);
    }

    private void ApplyBars()
    {
        if (playerSlider != null) playerSlider.value = Mathf.Lerp(playerSlider.minValue, playerSlider.maxValue, shownPlayer);
        if (enemySlider != null) enemySlider.value = Mathf.Lerp(enemySlider.minValue, enemySlider.maxValue, shownEnemy);
        if (playerFillImage != null) playerFillImage.fillAmount = shownPlayer;
        if (enemyFillImage != null) enemyFillImage.fillAmount = shownEnemy;
    }

    private void Update()
    {
        float step = barSmoothSpeed <= 0f ? 1f : barSmoothSpeed * Time.unscaledDeltaTime;
        shownPlayer = barSmoothSpeed <= 0f ? Fraction(lastPlayer) : Mathf.MoveTowards(shownPlayer, Fraction(lastPlayer), step);
        shownEnemy = barSmoothSpeed <= 0f ? Fraction(lastEnemy) : Mathf.MoveTowards(shownEnemy, Fraction(lastEnemy), step);
        ApplyBars();

        if (timerText != null && matchTimer != null)
            timerText.text = secondsOnly
                ? Mathf.CeilToInt(matchTimer.TimeRemaining).ToString()
                : matchTimer.GetFormattedTime();
    }

    private void Refresh(int player, int enemy)
    {
        SetTexts(player, enemy);
        if (player > lastPlayer) Pop(playerScoreText);
        if (enemy > lastEnemy) Pop(enemyScoreText);
        lastPlayer = player;
        lastEnemy = enemy;
    }

    private void SetTexts(int player, int enemy)
    {
        if (playerScoreText != null) playerScoreText.text = player.ToString();
        if (enemyScoreText != null) enemyScoreText.text = enemy.ToString();
    }

    // Remember each text's own scale so the pop returns to it (not to 1) - otherwise a score
    // enlarged via its Transform scale snaps back to 1 on the first point.
    private readonly System.Collections.Generic.Dictionary<RectTransform, Vector3> baseScales =
        new System.Collections.Generic.Dictionary<RectTransform, Vector3>();
    private readonly System.Collections.Generic.Dictionary<RectTransform, Coroutine> popRoutines =
        new System.Collections.Generic.Dictionary<RectTransform, Coroutine>();

    private void Pop(TMP_Text text)
    {
        if (text == null || popScale <= 1f || !isActiveAndEnabled) return;
        RectTransform rt = text.rectTransform;
        if (!baseScales.ContainsKey(rt)) baseScales[rt] = rt.localScale;
        if (popRoutines.TryGetValue(rt, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
            rt.localScale = baseScales[rt];
        }
        popRoutines[rt] = StartCoroutine(PopRoutine(rt, baseScales[rt]));
    }

    private IEnumerator PopRoutine(RectTransform rt, Vector3 baseScale)
    {
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            rt.localScale = baseScale * Mathf.Lerp(popScale, 1f, k);
            yield return null;
        }
        rt.localScale = baseScale;
        popRoutines[rt] = null;
    }
}