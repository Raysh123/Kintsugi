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

    [Header("Debug")]
    [SerializeField] private bool debugLog = true; // logs the countdown to the Console in whole seconds

    [Header("Events")]
    public UnityEvent OnTimeUp; // hook this up to your "match over" logic
    public UnityEvent OnMatchStart;
    public UnityEvent OnMatchStop;

    /// <summary>True once the match has been decided (e.g. first to N points). Nothing can restart
    /// the clock after this - the post-point resume is ignored.</summary>
    public bool matchOver { get; private set; }

    private float matchTimer;
    private int lastLoggedSecond = -1;
    private Coroutine hitPauseRoutine;

    public float TimeRemaining => matchTimer;

    private void Awake()
    {
        matchTimer = matchDuration;
    }

    private void Start()
    {
        if (autoStartOnSceneLoad) StartTimer();
    }

    private void Update()
    {
        if (matchStopped || matchTimer <= 0f) return;

        matchTimer -= Time.deltaTime;

        if (matchTimer <= 0f)
        {
            matchTimer = 0f;
            matchStopped = true;
            OnTimeUp?.Invoke();
        }

        if (debugLog)
        {
            int currentSecond = Mathf.CeilToInt(matchTimer);
            if (currentSecond != lastLoggedSecond)
            {
                lastLoggedSecond = currentSecond;
                Debug.Log("Match Timer: " + currentSecond);
            }
        }
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
        if (matchStopped || matchTimer <= 0f)
            return;

        StopTimer();

        hitPauseRoutine = StartCoroutine(ResumeAfterHit());
        //StopTimer();

        //if (hitPauseRoutine != null)
        //    StopCoroutine(hitPauseRoutine);

        //hitPauseRoutine = StartCoroutine(ResumeAfterHit());
    }

    private IEnumerator ResumeAfterHit()
    {
        // Wait using real-world time
        yield return new WaitForSecondsRealtime(hitPauseDuration);

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
        lastLoggedSecond = -1;
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