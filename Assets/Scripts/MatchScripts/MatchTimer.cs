using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MatchTimer : MonoBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 120f; // total match length in seconds
    public bool matchStopped = true;                     // starts stopped until referee says "hajime"

    [Tooltip("ON = the timer starts itself the moment the scene loads (old behaviour). Turn OFF " +
             "when a FightIntroSequence is in the scene - it calls StartTimer() itself once the " +
             "referee says begin, and until then the match stays stopped so nobody can fight.")]
    [SerializeField] private bool autoStartOnSceneLoad = true;

    [Header("Hit Pause")]
    [SerializeField] private float hitPauseDuration = 5f;

    [Header("Tie Break")]
    [Tooltip("When time runs out with the score level, add extra time instead of ending the match.")]
    [SerializeField] private bool extendOnTie = true;
    [SerializeField] private float tieExtensionSeconds = 15f;
    [Tooltip("How many extra-time periods before sudden death.")]
    [SerializeField] private int extraTimeRounds = 1;
    [Tooltip("Still tied after the extra time: the next point wins (the clock stops counting).")]
    [SerializeField] private bool suddenDeathAfterExtra = true;
    [Tooltip("Found automatically if empty.")]
    [SerializeField] private MatchScore matchScore;

    [Header("Events")]
    public UnityEvent OnTimeUp; // hook this up to your "match over" logic
    public UnityEvent OnMatchStart;
    public UnityEvent OnMatchStop;
    public UnityEvent OnExtraTime;   // tied at time-up: extra time is starting
    public UnityEvent OnSuddenDeath; // still tied after the extra time: next point wins

    /// <summary>True once the match has been decided (e.g. first to N points). Nothing can restart
    /// the clock after this - the post-point resume is ignored.</summary>
    public bool matchOver { get; private set; }

    /// <summary>True once the extra time has ended level: the next point wins the match.</summary>
    public bool SuddenDeath { get; private set; }

    private float matchTimer;
    private int extensions;
    private Coroutine hitPauseRoutine;

    public float TimeRemaining => matchTimer;

    private void Awake()
    {
        matchTimer = matchDuration;
        if (matchScore == null) matchScore = FindFirstObjectByType<MatchScore>();
    }

    private void Start()
    {
        if (autoStartOnSceneLoad) StartTimer();
    }

    private void Update()
    {
        if (matchStopped || matchTimer <= 0f) return;

        matchTimer -= Time.deltaTime;
        FightStats.time += Time.deltaTime;

        if (matchTimer <= 0f)
        {
            if (extendOnTie && matchScore != null && !matchOver &&
                matchScore.PlayerScore == matchScore.EnemyScore)
            {
                if (extensions < extraTimeRounds)
                {
                    extensions++;
                    matchTimer = tieExtensionSeconds;
                    PauseForCall();
                    OnExtraTime?.Invoke();
                    return;
                }
                if (suddenDeathAfterExtra)
                {
                    matchTimer = 0f;
                    SuddenDeath = true;
                    PauseForCall();
                    OnSuddenDeath?.Invoke();
                    return;
                }
            }

            matchTimer = 0f;
            matchStopped = true;
            OnTimeUp?.Invoke();
        }
    }

    // Stops play for the referee's call, then resumes like after a point.
    private void PauseForCall()
    {
        StopTimer();
        if (hitPauseRoutine != null) StopCoroutine(hitPauseRoutine);
        hitPauseRoutine = StartCoroutine(ResumeAfterHit());
    }

    // Call this from your referee script when "hajime" is called
    public void StartTimer()
    {
        if (matchOver) { matchStopped = true; return; }
        if (matchTimer > 0f)
            matchStopped = false;
        if (hitPauseRoutine != null)
        {
            StopCoroutine(hitPauseRoutine);
            hitPauseRoutine = null;
        }

        Time.timeScale = 1f;
        matchStopped = false;

        OnMatchStart?.Invoke();
    }

    // Call this from your referee script when "yame" is called (point scored, foul, etc.)
    public void StopTimer()
    {
        matchStopped = true;
        //Time.timeScale = 0f;
        OnMatchStop?.Invoke();
    }
    /// <summary>Ends the match for good: freezes the clock and blocks any later resume.</summary>
    public void EndMatch()
    {
        matchOver = true;
        matchStopped = true;
        if (hitPauseRoutine != null)
        {
            StopCoroutine(hitPauseRoutine);
            hitPauseRoutine = null;
        }
    }

    public void RegisterHit()
    {
        // Ignore hits while the match is stopped
        if (matchStopped || (matchTimer <= 0f && !SuddenDeath))
            return;

        StopTimer();

        hitPauseRoutine = StartCoroutine(ResumeAfterHit());
        //StopTimer();

        //if (hitPauseRoutine != null)
        //    StopCoroutine(hitPauseRoutine);

        //hitPauseRoutine = StartCoroutine(ResumeAfterHit());
    }

    /// <summary>While true, the automatic restart after a point / call waits (Hajime is not said). Set and cleared by other scripts.</summary>
    public bool HoldResume { get; set; }

    private IEnumerator ResumeAfterHit()
    {
        // Wait using real-world time
        yield return new WaitForSecondsRealtime(hitPauseDuration);
        while (HoldResume) yield return null;   // something (e.g. the tutorial taunt) wants the pause to last longer

        hitPauseRoutine = null;

        StartTimer();
    }
    public void ResetMatch()
    {
        if (hitPauseRoutine != null)
        {
            StopCoroutine(hitPauseRoutine);
            hitPauseRoutine = null;
        }

        matchTimer = matchDuration;
        extensions = 0;
        SuddenDeath = false;
        matchStopped = true;

        //Time.timeScale = 1f;
    }

    // Handy for a UI Text/TMP element
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(matchTimer / 60f);
        int seconds = Mathf.FloorToInt(matchTimer % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}