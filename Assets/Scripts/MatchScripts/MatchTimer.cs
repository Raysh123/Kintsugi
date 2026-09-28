using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MatchTimer : MonoBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 120f; // total match length in seconds
    public bool matchStopped = true;                     // starts stopped until referee says "hajime"

    [Header("Hit Pause")]
    [SerializeField] private float hitPauseDuration = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true; // logs the countdown to the Console in whole seconds

    [Header("Events")]
    public UnityEvent OnTimeUp; // hook this up to your "match over" logic

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
        StartTimer();
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
        if (matchTimer > 0f)
            matchStopped = false;
    }

    // Call this from your referee script when "yame" is called (point scored, foul, etc.)
    public void StopTimer()
    {
        matchStopped = true;
    }

    public void RegisterHit()
    {
        StopTimer();

        if (hitPauseRoutine != null)
            StopCoroutine(hitPauseRoutine);

        hitPauseRoutine = StartCoroutine(ResumeAfterHit());
    }

    private IEnumerator ResumeAfterHit()
    {
        yield return new WaitForSeconds(hitPauseDuration);
        StartTimer();
        hitPauseRoutine = null;
    }

    // Handy for a UI Text/TMP element
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(matchTimer / 60f);
        int seconds = Mathf.FloorToInt(matchTimer % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
}