using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Out-of-bounds + warnings. If either fighter steps off the mat the fight stops exactly like after a
/// point (hit anims, referee calls Yame, fighters are reset), but instead of a point the referee gives
/// that fighter a WARNING. Every fighter keeps their own warning count for the whole match; the SECOND
/// warning is a point deduction from their score (scores may go negative) and their count goes back to 0.
///
/// Setup (fight scene, one object):
///   1. Make two empty objects at the left and right EDGE of the mat and assign them to Left Edge / Right
///      Edge. Only their X position matters. A fighter is out when his pivot passes either one.
///   2. Add this component anywhere (e.g. next to the referee). Everything else is found automatically.
///   3. Optional: two TMP texts to show each fighter's warnings.
///   The reset after the stop uses the same Referee "On Ready To Reset" (GameManager.ResetPos) as points.
/// </summary>
public class OutOfBounds : MonoBehaviour
{
    [System.Serializable] public class WarningsChangedEvent : UnityEvent<int, int> { }

    [Header("The mat")]
    [Tooltip("Empty object at the LEFT edge of the mat (only its X is used).")]
    [SerializeField] private Transform leftEdge;
    [Tooltip("Empty object at the RIGHT edge of the mat (only its X is used).")]
    [SerializeField] private Transform rightEdge;
    [Tooltip("A fighter must be this far beyond the edge before it counts (so grazing the line is fine).")]
    [SerializeField] private float margin = 0.1f;

    [Header("Penalty")]
    [Tooltip("Warnings that lead to a point deduction (then the count resets to 0).")]
    [SerializeField] private int warningsPerDeduction = 2;
    [Tooltip("Points removed from the offender's score on the deduction. The score can go negative.")]
    [SerializeField] private int pointsDeducted = 1;

    [Header("Referee calls ({who} = Player / Opponent, {n} = warnings so far)")]
    [SerializeField] private string warningText = "Warning! {who} out of bounds ({n}/2)";
    [SerializeField] private string deductionText = "{who} out of bounds again - point deducted";
    [SerializeField] private string playerName = "Player";
    [SerializeField] private string enemyName = "Opponent";
    [Tooltip("Optional referee Animator trigger for the call (skipped if the Animator has none).")]
    [SerializeField] private string refereeTrigger = "Warning";
    [Tooltip("Referee triggers used instead when the fighter stepped off the LEFT / RIGHT edge of the mat " +
             "(blank = use the general trigger above).")]
    [SerializeField] private string leftTrigger = "WarningLeft";
    [SerializeField] private string rightTrigger = "WarningRight";

    [Header("Optional UI")]
    [SerializeField] private TMP_Text playerWarningText;
    [SerializeField] private TMP_Text enemyWarningText;
    [SerializeField] private string warningUiFormat = "Warnings: {0}";

    [Header("References (auto-found)")]
    [SerializeField] private MatchScore matchScore;
    [SerializeField] private MatchTimer matchTimer;
    [SerializeField] private RefereeController referee;
    [SerializeField] private Transform player;
    [SerializeField] private Transform enemy;

    [Tooltip("Fired whenever a warning count changes: (player warnings, enemy warnings).")]
    public WarningsChangedEvent OnWarningsChanged;

    /// <summary>(isPlayer, wasDeduction) - fired when a warning / deduction is given. Used by FighterReactions.</summary>
    public event System.Action<bool, bool> OnPenaltyCalled;

    public int PlayerWarnings { get; private set; }
    public int EnemyWarnings { get; private set; }

    private bool armed = true;

    private void Start()
    {
        if (matchScore == null) matchScore = FindFirstObjectByType<MatchScore>();
        if (matchTimer == null) matchTimer = FindFirstObjectByType<MatchTimer>();
        if (referee == null) referee = FindFirstObjectByType<RefereeController>();
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (enemy == null)
        {
            var es = FindFirstObjectByType<EnemyStateMachine>();
            if (es != null) enemy = es.transform;
        }
        if (leftEdge == null || rightEdge == null)
            Debug.LogWarning("[OutOfBounds] Left Edge / Right Edge not assigned - nothing will be detected.");
        RefreshUi();
    }

    private void Update()
    {
        if (leftEdge == null || rightEdge == null || player == null || enemy == null) return;
        if (matchTimer == null || matchTimer.matchStopped || matchTimer.matchOver) return;
        if (PlayerSwap.Swapping) return;   // a swap carries both fighters; judge them after it

        bool pOut = IsOut(player), eOut = IsOut(enemy);
        if (!pOut && !eOut) { armed = true; return; }   // re-armed once everyone is back on the mat
        if (!armed) return;

        armed = false;
        if (pOut) Penalise(true);
        else Penalise(false);
    }

    private bool IsOut(Transform t)
    {
        float lo = Mathf.Min(leftEdge.position.x, rightEdge.position.x) - margin;
        float hi = Mathf.Max(leftEdge.position.x, rightEdge.position.x) + margin;
        float x = t.position.x;
        return x < lo || x > hi;
    }

    private void Penalise(bool isPlayer)
    {
        int count = (isPlayer ? PlayerWarnings : EnemyWarnings) + 1;
        string who = isPlayer ? playerName : enemyName;
        string text;
        bool deduction = false;

        if (count >= Mathf.Max(1, warningsPerDeduction))
        {
            deduction = true;
            count = 0;
            if (matchScore != null) matchScore.DeductPoints(isPlayer, pointsDeducted);
            text = deductionText;
            Debug.Log($"[OutOfBounds] {who} out of bounds - warning limit reached: -{pointsDeducted} point(s), warnings reset to 0.");
        }
        else
        {
            text = warningText;
            Debug.Log($"[OutOfBounds] {who} out of bounds - warning {count}/{warningsPerDeduction}.");
        }

        if (isPlayer) PlayerWarnings = count; else EnemyWarnings = count;
        RefreshUi();
        OnWarningsChanged?.Invoke(PlayerWarnings, EnemyWarnings);

        OnPenaltyCalled?.Invoke(isPlayer, deduction);

        // Stop play like a point does: the clock stops, the referee calls Yame, fighters are reset, play resumes.
        if (matchTimer != null) matchTimer.RegisterHit();
        if (referee != null)
        {
            Transform t = isPlayer ? player : enemy;
            float centre = (leftEdge.position.x + rightEdge.position.x) * 0.5f;
            bool left = t != null && t.position.x < centre;
            string side = left ? leftTrigger : rightTrigger;
            string trig = string.IsNullOrEmpty(side) ? refereeTrigger : side;
            referee.CallPenalty((text ?? "").Replace("{who}", who).Replace("{n}", (isPlayer ? PlayerWarnings : EnemyWarnings).ToString()), trig);
        }
        else
            Debug.LogWarning("[OutOfBounds] No RefereeController found - no call, and no reset will fire.");
    }

    private void RefreshUi()
    {
        if (playerWarningText != null) playerWarningText.text = string.Format(warningUiFormat, PlayerWarnings);
        if (enemyWarningText != null) enemyWarningText.text = string.Format(warningUiFormat, EnemyWarnings);
    }

    /// <summary>Zero both warning counts (for a brand new match).</summary>
    public void ResetWarnings()
    {
        PlayerWarnings = EnemyWarnings = 0;
        armed = true;
        RefreshUi();
        OnWarningsChanged?.Invoke(0, 0);
    }

    private void OnDrawGizmos()
    {
        if (leftEdge == null || rightEdge == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.1f);
        foreach (Transform e in new[] { leftEdge, rightEdge })
        {
            Vector3 p = e.position;
            Gizmos.DrawLine(p + Vector3.down * 0.5f, p + Vector3.up * 3f);
            Gizmos.DrawWireCube(p + Vector3.up * 0.5f, new Vector3(0.05f, 1f, 3f));
        }
    }
}