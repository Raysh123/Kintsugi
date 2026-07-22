using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays current in-game time as HH:MM and animates a rotating indicator
/// (sun/moon icon or clock hand) whenever time advances. Since the calendar
/// is now discrete (Persona-style), the display animates smoothly between
/// the old and new time value rather than polling every frame.
/// </summary>
public class TimeOfDayDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text timeText;
    [Tooltip("Optional UI element that rotates 360 degrees over one full in-game day.")]
    [SerializeField] private RectTransform clockIndicator;

    [Header("Animation")]
    [Tooltip("How long (real seconds) the display takes to animate to the new time after an activity completes.")]
    [SerializeField] private float animDuration = 1.5f;

    private float _displayedMinutes;
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

        _displayedMinutes = CalendarManager.instance.PreciseMinutesSinceDayStart;
        CalendarManager.instance.OnTimeAdvanced += HandleTimeAdvanced;
        UpdateVisuals(_displayedMinutes);
    }

    private void HandleTimeAdvanced(float newPreciseMinutes)
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateTo(newPreciseMinutes));
    }

    private IEnumerator AnimateTo(float targetMinutes)
    {
        float startMinutes = _displayedMinutes;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            _displayedMinutes = Mathf.Lerp(startMinutes, targetMinutes, elapsed / animDuration);
            UpdateVisuals(_displayedMinutes);
            yield return null;
        }

        _displayedMinutes = targetMinutes % CalendarManager.instance.MinutesPerDay;
        UpdateVisuals(_displayedMinutes);
        _animRoutine = null;
    }

    private void UpdateVisuals(float minutes)
    {
        int minutesPerDay = CalendarManager.instance.MinutesPerDay;
        int wrapped = Mathf.FloorToInt(minutes) % minutesPerDay;
        int hours = (wrapped / 60) % 24;
        int mins = wrapped % 60;

        if (timeText != null)
            timeText.text = $"{hours:00}:{mins:00}";

        if (clockIndicator != null)
        {
            float normalized = wrapped / (float)minutesPerDay;
            clockIndicator.localRotation = Quaternion.Euler(0f, 0f, -normalized * 360f);
        }
    }
}