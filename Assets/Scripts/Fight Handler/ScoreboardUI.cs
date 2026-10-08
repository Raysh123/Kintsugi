using System.Collections;
using TMPro;
using UnityEngine;

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

    [Header("Clock (optional)")]
    [SerializeField] private MatchTimer matchTimer;
    [SerializeField] private TMP_Text timerText;

    [Header("Score pop")]
    [Tooltip("Scale a score number briefly pops to when it increases. 1 = no pop.")]
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private float popDuration = 0.35f;

    private int lastPlayer, lastEnemy;

    private void OnEnable()
    {
        if (matchScore == null) return;
        matchScore.OnScoreChanged.AddListener(Refresh);
        lastPlayer = matchScore.PlayerScore;
        lastEnemy = matchScore.EnemyScore;
        SetTexts(lastPlayer, lastEnemy);
    }

    private void OnDisable()
    {
        if (matchScore != null) matchScore.OnScoreChanged.RemoveListener(Refresh);
    }

    private void Update()
    {
        if (timerText != null && matchTimer != null)
            timerText.text = matchTimer.GetFormattedTime();
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

    private void Pop(TMP_Text text)
    {
        if (text == null || popScale <= 1f || !isActiveAndEnabled) return;
        StartCoroutine(PopRoutine(text.rectTransform));
    }

    private IEnumerator PopRoutine(RectTransform rt)
    {
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            rt.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, k);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
