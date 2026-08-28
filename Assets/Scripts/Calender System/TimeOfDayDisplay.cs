using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays the current quadrant (Morning/Afternoon/Evening/Night) and animates
/// a rotating indicator whenever time advances. There's no clock-time anymore —
/// the calendar is quadrant-ticks only — so this shows the phase name as text
/// and rotates the indicator through the day based on tick progress rather than
/// minutes, but the smooth-animate-between-old-and-new-value approach is the same.
/// </summary>
public class TimeOfDayDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text phaseText;
    [Tooltip("Optional UI element that rotates 360 degrees over one full in-game day.")]
    [SerializeField] private RectTransform clockIndicator;

    [Header("Animation")]
    [Tooltip("How long (real seconds) the display takes to animate to the new tick after an activity completes.")]
    [SerializeField] private float animDuration = 1.5f;

    private float _displayedTick;
    private Coroutine _animRoutine;

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (CalendarManager.instance != null)
            CalendarManager.instance.OnTimeAdvanced -= HandleTimeAdvanced;
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (CalendarManager.instance == null)
            yield return null;

        _displayedTick = CalendarManager.instance.CurrentTick;
        CalendarManager.instance.OnTimeAdvanced += HandleTimeAdvanced;
        UpdateVisuals(_displayedTick);
    }

    private void HandleTimeAdvanced(int newTick)
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateTo(newTick));
    }

    private IEnumerator AnimateTo(float targetTick)
    {
        float startTick = _displayedTick;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            _displayedTick = Mathf.Lerp(startTick, targetTick, elapsed / animDuration);
            UpdateVisuals(_displayedTick);
            yield return null;
        }

        _displayedTick = targetTick % CalendarManager.TicksPerDay;
        UpdateVisuals(_displayedTick);
        _animRoutine = null;
    }

    private void UpdateVisuals(float tick)
    {
        int wrapped = Mathf.FloorToInt(tick) % CalendarManager.TicksPerDay;
        DayPhase phase = (DayPhase)(wrapped / CalendarManager.TicksPerQuadrant);

        if (phaseText != null)
            phaseText.text = phase.ToString();

        if (clockIndicator != null)
        {
            float normalized = wrapped / (float)CalendarManager.TicksPerDay;
            clockIndicator.localRotation = Quaternion.Euler(0f, 0f, -normalized * 360f);
        }
    }
}