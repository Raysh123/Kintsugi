using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Point-based scoring for the match. DmgDetection calls AddPlayerPoint/AddEnemyPoint at the
/// exact moment it resolves an unblocked hit, passing the move type that landed - point value
/// per move type comes from the assigned AttackTypes asset, so tuning scoring is a data
/// change, not a code change.
///
/// Subscribes to MatchTimer.OnTimeUp directly in code (rather than relying on it being wired
/// up via the Inspector) so the winner is always declared exactly once, automatically, the
/// moment the match timer runs out. Whoever has the most points wins; see tieGoesToPlayer for
/// how a tie is currently handled - this project doesn't have sudden-death/encho-sen rules
/// implemented, so a tie is just a coin-flip-by-config for now rather than a real tiebreaker.
/// </summary>
public class MatchScore : MonoBehaviour
{
    /// <summary>UnityEvent<int,int> needs a concrete subclass like this to be serializable
    /// and show up in the Inspector - a bare generic UnityEvent<> won't.</summary>
    [System.Serializable]
    public class ScoreChangedEvent : UnityEvent<int, int> { }

    [SerializeField] private MatchTimer matchTimer;
    [SerializeField] private AttackTypes attackPointValues;

    [Tooltip("No sudden-death/encho-sen round implemented yet - this just decides who the " +
             "winner is if points are exactly equal when time runs out.")]
    [SerializeField] private bool tieGoesToPlayer = false;

    [Header("Win by points")]
    [Tooltip("First fighter to reach this many points wins the match immediately (the timer " +
             "running out is still the other win condition). 0 = off.")]
    [SerializeField] private int pointsToWin = 10;
    [Tooltip("Real seconds between the winning point and the scene change - room for the " +
             "referee's point call.")]
    [SerializeField] private float winDelay = 3f;
    [Tooltip("Fired when someone reaches the winning score, before the delay.")]
    public UnityEvent onPointsToWinReached;

    public int PlayerScore { get; private set; }
    public int EnemyScore { get; private set; }

    [Tooltip("Fired whenever either score changes, with the new (playerScore, enemyScore) - " +
             "hook your scoreboard UI up to this instead of polling PlayerScore/EnemyScore " +
             "every frame.")]
    public ScoreChangedEvent OnScoreChanged;

    private bool winnerDeclared;

    private void OnEnable()
    {
        if (matchTimer != null) matchTimer.OnTimeUp.AddListener(DeclareWinner);
    }

    private void OnDisable()
    {
        if (matchTimer != null) matchTimer.OnTimeUp.RemoveListener(DeclareWinner);
    }

    public void AddPlayerPoint(PlayerStateMachine.AttackTypes moveType)
    {
        int points = attackPointValues != null ? attackPointValues.GetPoints(moveType) : 1;
        PlayerScore += points;
        Debug.Log($"[MatchScore] Player +{points} ({moveType}) -> {PlayerScore}");
        OnScoreChanged?.Invoke(PlayerScore, EnemyScore);
        CheckPointsToWin();
    }

    public void AddEnemyPoint(PlayerStateMachine.AttackTypes moveType)
    {
        int points = attackPointValues != null ? attackPointValues.GetPoints(moveType) : 1;
        EnemyScore += points;
        Debug.Log($"[MatchScore] Enemy +{points} ({moveType}) -> {EnemyScore}");
        OnScoreChanged?.Invoke(PlayerScore, EnemyScore);
        CheckPointsToWin();
    }

    private bool endingByPoints;

    private void CheckPointsToWin()
    {
        if (pointsToWin <= 0 || winnerDeclared || endingByPoints) return;
        if (PlayerScore < pointsToWin && EnemyScore < pointsToWin) return;
        endingByPoints = true;
        StartCoroutine(EndByPoints());
    }

    private System.Collections.IEnumerator EndByPoints()
    {
        Debug.Log($"[MatchScore] {pointsToWin} points reached ({PlayerScore}-{EnemyScore}) - match over.");
        if (matchTimer != null) matchTimer.EndMatch();
        onPointsToWinReached?.Invoke();
        yield return new WaitForSecondsRealtime(winDelay);
        DeclareWinner();
    }

    /// <summary>Compares final scores and hands the result off to FightSessionManager, which
    /// loads the RPG scene back at the door the player fought through. Safe to call more than
    /// once (e.g. if OnTimeUp somehow fires twice) - only the first call actually reports.</summary>
    public void DeclareWinner()
    {
        if (winnerDeclared) return;
        winnerDeclared = true;

        bool playerWon = PlayerScore > EnemyScore ||
                          (PlayerScore == EnemyScore && tieGoesToPlayer);

        Debug.Log($"[MatchScore] Final score - Player: {PlayerScore}, Enemy: {EnemyScore}. " +
                  $"Winner: {(playerWon ? "Player" : "Enemy")}" +
                  (PlayerScore == EnemyScore ? " (tie, decided by Tie Goes To Player)" : "") + ".");

        FightSessionManager.ReportResult(playerWon);
    }

    /// <summary>Call from GameManager.ResetPos() (or wherever a fresh match starts) if scores
    /// should zero out between separate matches rather than carrying over.</summary>
    public void ResetScore()
    {
        PlayerScore = 0;
        EnemyScore = 0;
        winnerDeclared = false;
        endingByPoints = false;
        OnScoreChanged?.Invoke(PlayerScore, EnemyScore);
    }
}