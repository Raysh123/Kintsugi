using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the visual fill bar for FillBarMinigame: fillAmount tracks the
/// current percent, and the bar's color switches between your injury/loss/win
/// colors based on which band it's currently in. Optional marker
/// RectTransforms show exactly where the injury/win cutoffs sit on the bar —
/// useful since those cutoffs move with motivation.
///
/// Setup: fillImage should be a UI Image with Image Type: Filled (Horizontal
/// fill method assumed for the marker positioning below). Markers must be
/// children of fillImage itself — since a Filled Image's RectTransform stays
/// full-size regardless of fillAmount, it's the exact same rect the visible
/// bar renders into, guaranteeing markers line up with no separate track
/// object needed.
/// </summary>
public class FillBarMeterUI : MonoBehaviour
{
    [SerializeField] private FillBarMinigame minigame;

    [Header("Fill Bar (Image Type: Filled, Horizontal)")]
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color injuryColor = new Color(0.85f, 0.2f, 0.2f);
    [SerializeField] private Color lossColor = new Color(0.9f, 0.75f, 0.2f);
    [SerializeField] private Color winColor = new Color(0.25f, 0.8f, 0.35f);

    [Header("Threshold Markers (optional)")]
    [Tooltip("Thin marker RectTransform positioned at the injury cutoff. Must be a child of fillImage.")]
    [SerializeField] private RectTransform injuryMarker;
    [Tooltip("Thin marker RectTransform positioned at the win cutoff. Must be a child of fillImage.")]
    [SerializeField] private RectTransform winMarker;

    private void OnEnable()
    {
        if (minigame == null) return;
        minigame.OnFillChanged += HandleFillChanged;
        minigame.OnThresholdsChanged += HandleThresholdsChanged;
    }

    private void OnDisable()
    {
        if (minigame == null) return;
        minigame.OnFillChanged -= HandleFillChanged;
        minigame.OnThresholdsChanged -= HandleThresholdsChanged;
    }

    private void HandleFillChanged(float percent)
    {
        if (fillImage == null) return;

        fillImage.fillAmount = Mathf.Clamp01(percent / 100f);
        fillImage.color = GetColorForPercent(percent);
    }

    private Color GetColorForPercent(float percent)
    {
        if (minigame == null) return winColor;

        if (percent <= minigame.currentInjuryThreshold) return injuryColor;
        if (percent >= minigame.currentWinThreshold) return winColor;
        return lossColor;
    }

    private void HandleThresholdsChanged(float injuryThreshold, float winThreshold)
    {
        PositionMarker(injuryMarker, injuryThreshold);
        PositionMarker(winMarker, winThreshold);
    }

    private void PositionMarker(RectTransform marker, float percent)
    {
        if (marker == null || fillImage == null) return;

        RectTransform track = fillImage.rectTransform;

        if (marker.parent != track)
        {
            Debug.LogWarning($"{name}: {marker.name} isn't a child of fillImage — its position is " +
                              "computed from fillImage's rect, so it needs to be parented there.");
        }

        float normalized = Mathf.Clamp01(percent / 100f);

        // Anchor pinned to the fill bar's own left edge — an Image with Type: Filled always
        // keeps its RectTransform at full size (fillAmount only clips the rendered sprite,
        // it never resizes the rect), so this is guaranteed to match the visible bar exactly.
        Vector2 anchor = new Vector2(0f, 0.5f);
        marker.anchorMin = anchor;
        marker.anchorMax = anchor;
        marker.pivot = new Vector2(0.5f, marker.pivot.y);

        float xOffset = normalized * track.rect.width;
        marker.anchoredPosition = new Vector2(xOffset, marker.anchoredPosition.y);
    }
}