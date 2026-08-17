using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the visual side of TrackerRhythmMinigame: a knob that slides
/// left-right tracking trackerPosition, and a green (or whatever color)
/// overlay showing exactly where the current hit zone is.
///
/// Setup: trackRect is the single source of truth for the bar's width — both
/// knob and zoneOverlay must be children of it. This mirrors FillBarMeterUI's
/// fix for the marker-misalignment issue: using one shared rect instead of
/// mixing separate images avoids any chance of the knob/zone drifting out of
/// sync with each other.
/// </summary>
public class TrackerRhythmMeterUI : MonoBehaviour
{
    [SerializeField] private TrackerRhythmMinigame minigame;

    [Header("Track")]
    [Tooltip("The bar's RectTransform — knob and zoneOverlay must both be children of this.")]
    [SerializeField] private RectTransform trackRect;

    [Header("Knob")]
    [SerializeField] private RectTransform knob;

    [Header("Hit Zone")]
    [SerializeField] private RectTransform zoneOverlay;

    private void OnEnable()
    {
        if (minigame == null) return;
        minigame.OnTrackerPositionChanged += HandleTrackerMoved;
        minigame.OnZoneChanged += HandleZoneChanged;

        WarnIfZeroWidth();

        // Prime immediately in case some state already exists before this UI enabled.
        HandleTrackerMoved(minigame.trackerPosition);
        HandleZoneChanged(minigame.currentZoneMin, minigame.currentZoneMax);
    }

    private void WarnIfZeroWidth()
    {
        if (trackRect != null && trackRect.rect.width < 1f)
        {
            Debug.LogWarning($"{name}: trackRect's width is reading as ~0 ({trackRect.rect.width:F1}px). " +
                              "Give it an explicit Width on its RectTransform (not just stretch anchors) — " +
                              "otherwise the knob will never move and the zone overlay will never show, " +
                              "since both are computed as a fraction of this width.");
        }
    }

    private void OnDisable()
    {
        if (minigame == null) return;
        minigame.OnTrackerPositionChanged -= HandleTrackerMoved;
        minigame.OnZoneChanged -= HandleZoneChanged;
    }

    private void HandleTrackerMoved(float normalizedPos)
    {
        if (knob == null || trackRect == null) return;

        WarnIfNotChild(knob, nameof(knob));

        // Pin to the track's left edge, centered pivot — same pattern as
        // FillBarMeterUI's threshold markers.
        Vector2 anchor = new Vector2(0f, 0.5f);
        knob.anchorMin = anchor;
        knob.anchorMax = anchor;
        knob.pivot = new Vector2(0.5f, knob.pivot.y);

        float xOffset = Mathf.Clamp01(normalizedPos) * trackRect.rect.width;
        knob.anchoredPosition = new Vector2(xOffset, knob.anchoredPosition.y);
    }

    private void HandleZoneChanged(float zoneMin, float zoneMax)
    {
        if (zoneOverlay == null || trackRect == null) return;

        WarnIfNotChild(zoneOverlay, nameof(zoneOverlay));

        float trackWidth = trackRect.rect.width;

        // Left-pinned anchor AND left-aligned pivot — for a width span (not a single
        // point like the knob), sizeDelta directly represents the zone's width this way.
        Vector2 anchor = new Vector2(0f, zoneOverlay.anchorMin.y);
        zoneOverlay.anchorMin = anchor;
        zoneOverlay.anchorMax = new Vector2(0f, zoneOverlay.anchorMax.y);
        zoneOverlay.pivot = new Vector2(0f, zoneOverlay.pivot.y);

        zoneOverlay.anchoredPosition = new Vector2(zoneMin * trackWidth, zoneOverlay.anchoredPosition.y);
        zoneOverlay.sizeDelta = new Vector2((zoneMax - zoneMin) * trackWidth, zoneOverlay.sizeDelta.y);
    }

    private void WarnIfNotChild(RectTransform child, string fieldName)
    {
        if (trackRect != null && child.parent != trackRect)
            Debug.LogWarning($"{name}: {fieldName} isn't a child of trackRect — its position is " +
                              "computed from trackRect's rect, so it needs to be parented there.");
    }
}
