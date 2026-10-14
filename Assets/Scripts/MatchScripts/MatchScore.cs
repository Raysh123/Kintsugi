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

    [Header("Result (win / lose)")]
    [Tooltip("SoundLibrary id of the stinger played when the player wins (an SFX). Blank = none.")]
    [SerializeField] private string winStingerId;
    [Tooltip("SoundLibrary id of the stinger played when the player loses. Blank = none.")]
    [SerializeField] private string loseStingerId;
    [Tooltip("Real seconds the result is held (referee call + stinger) before the scene changes. " +
             "The fight music fades out slowly over exactly this time.")]
    [SerializeField] private float resultHold = 4f;
    [Tooltip("Referee that announces 'Player wins / loses'. Auto-found if left empty.")]
    [SerializeField] private RefereeController referee;

    [Header("Stat changes after the fight (set the amounts here)")]
    [Tooltip("Health added to the player when he WINS the fight (by points or on time).")]
    [SerializeField] private float healthGainOnWin = 5f;
    [Tooltip("Motivation added to the player when he WINS.")]
    [SerializeField] private float motivationGainOnWin = 10f;
    [Tooltip("Motivation REMOVED when he LOSES on points or on time. (Losing by injury knockout " +
             "doesn't use this - the injury meter takes health instead, see InjuryMeter.)")]
    [SerializeField] private float motivationLossOnDefeat = 10f;

    [Header("Win by points")]
    [Tooltip("First fighter to reach this many points wins the match immediately (the timer " +
             "running out is still the other win condition). 0 = off.")]
    [SerializeField] private int pointsToWin = 10;
    [Tooltip("Real seconds between the winning point and the scene change - room for the " +
             "referee's point call.")]
    [SerializeField] private float winDelay = 3f;
    [Tooltip("Fired when someone reaches the winning score, before the delay.")]
    public UnityEvent onPointsToWinReached;

    /// <summary>Points needed to win (0 = the points rule is off). Used by the scoreboard bars.</summary>
    public int PointsToWin => pointsToWin;

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

    /// <summary>Removes points from a fighter's score (out-of-bounds penalty). The score can go negative.</summary>
    public void DeductPoints(bool fromPlayer, int points)
    {
        if (fromPlayer) PlayerScore -= points; else EnemyScore -= points;
        Debug.Log($"[MatchScore] {(fromPlayer ? "Player" : "Enemy")} -{points} (penalty) -> {(fromPlayer ? PlayerScore : EnemyScore)}");
        OnScoreChanged?.Invoke(PlayerScore, EnemyScore);
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

        StartCoroutine(ResultSequence(playerWon));
    }

    /// <summary>For win/lose conditions decided elsewhere (e.g. the injury meter): plays the same
    /// result sequence - referee call, stinger, music fade - then reports. Only runs once.</summary>
    public void EndFight(bool playerWon, bool applyStatChanges = true)
    {
        if (winnerDeclared) return;
        winnerDeclared = true;
        Debug.Log($"[MatchScore] Fight ended outside scoring. Player {(playerWon ? "won" : "lost")}.");
        StartCoroutine(ResultSequence(playerWon, applyStatChanges));
    }

    private void ApplyStatChanges(bool playerWon)
    {
        HealthAndMotivation hm = HealthAndMotivation.instance;
        if (hm == null)
        {
            Debug.LogWarning("[MatchScore] No HealthAndMotivation in the scene - health/motivation not changed.");
            return;
        }
        if (playerWon)
        {
            hm.ModifyHealth(healthGainOnWin);
            hm.ModifyMotivation(motivationGainOnWin);
            Debug.Log($"[MatchScore] Win: +{healthGainOnWin} health, +{motivationGainOnWin} motivation.");
        }
        else
        {
            hm.ModifyMotivation(-motivationLossOnDefeat);
            Debug.Log($"[MatchScore] Defeat: -{motivationLossOnDefeat} motivation.");
        }
    }

    private System.Collections.IEnumerator ResultSequence(bool playerWon, bool applyStatChanges = true)
    {
        if (applyStatChanges) ApplyStatChanges(playerWon);

        if (matchTimer != null) matchTimer.EndMatch();
        if (referee == null) referee = FindFirstObjectByType<RefereeController>();

        if (referee != null) referee.AnnounceResult(playerWon, resultHold);

        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopMusic(Mathf.Max(0.01f, resultHold));
            string stinger = playerWon ? winStingerId : loseStingerId;
            if (!string.IsNullOrEmpty(stinger)) AudioManager.instance.PlaySFX(stinger);
        }

        yield return new WaitForSecondsRealtime(resultHold);
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