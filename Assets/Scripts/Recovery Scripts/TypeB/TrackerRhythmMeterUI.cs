using System.Collections.Generic;
using TMPro;
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

    [Header("Hit Feedback (optional)")]
    [Tooltip("Played on every successful hit. Parent this under the knob for it to " +
             "automatically play at the knob's current position. Note: won't render " +
             "through a Screen Space - Overlay Canvas — needs Screen Space - Camera " +
             "or World Space, since particles need a camera pass to draw.")]
    [SerializeField] private ParticleSystem hitParticles;

    [Header("Hit Count Display (optional)")]
    [Tooltip("Shows hitCount/winHitsRequired, e.g. '4/10'.")]
    [SerializeField] private TMP_Text hitCountText;

    [Header("Miss Hearts (optional)")]
    [Tooltip("Prefab spawned once per life — count matches injuryMissLimit automatically, so different " +
             "exercises with different miss limits just work without manually re-matching a list.")]
    [SerializeField] private GameObject heartPrefab;
    [Tooltip("Parent the spawned hearts are instantiated under.")]
    [SerializeField] private Transform heartsContainer;

    private readonly List<GameObject> spawnedHearts = new List<GameObject>();

    private void OnEnable()
    {
        if (minigame == null) return;
        minigame.OnTrackerPositionChanged += HandleTrackerMoved;
        minigame.OnZoneChanged += HandleZoneChanged;
        minigame.OnSuccessfulHit += HandleSuccessfulHit;
        minigame.OnProgressChanged += HandleProgressChanged;
        minigame.OnMinigameReset += HandleMinigameReset;

        WarnIfZeroWidth();

        // Prime immediately in case some state already exists before this UI enabled.
        HandleTrackerMoved(minigame.trackerPosition);
        HandleZoneChanged(minigame.currentZoneMin, minigame.currentZoneMax);
        HandleMinigameReset(); // spawns hearts matching the current injuryMissLimit
        HandleProgressChanged(minigame.hitCount, minigame.missCount); // fixes visibility to current state
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
        if (minigame != null)
        {
            minigame.OnTrackerPositionChanged -= HandleTrackerMoved;
            minigame.OnZoneChanged -= HandleZoneChanged;
            minigame.OnSuccessfulHit -= HandleSuccessfulHit;
            minigame.OnProgressChanged -= HandleProgressChanged;
            minigame.OnMinigameReset -= HandleMinigameReset;
        }

        DestroySpawnedHearts();
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

    private void HandleSuccessfulHit()
    {
        if (hitParticles == null) return;

        // Play(true) restarts including any child particle systems, so rapid
        // successive hits each get a clean burst rather than blending oddly
        // into whatever's still finishing from the previous one.
        hitParticles.Play(true);
    }

    private void HandleProgressChanged(int hitCount, int missCount)
    {
        if (hitCountText != null && minigame != null)
            hitCountText.text = $"{hitCount}/{minigame.winHitsRequired}";

        for (int i = 0; i < spawnedHearts.Count; i++)
        {
            if (spawnedHearts[i] != null)
                spawnedHearts[i].SetActive(i >= missCount);
        }
    }

    /// <summary>
    /// Destroys any hearts left over from the previous attempt (a different exercise
    /// may have used a different injuryMissLimit) and spawns fresh ones matching the
    /// current one. All start active — HandleProgressChanged fixes their visibility
    /// to the current missCount right after this runs.
    /// </summary>
    private void HandleMinigameReset()
    {
        DestroySpawnedHearts();

        if (heartPrefab == null || heartsContainer == null || minigame == null) return;

        for (int i = 0; i < minigame.injuryMissLimit; i++)
            spawnedHearts.Add(Instantiate(heartPrefab, heartsContainer));
    }

    private void DestroySpawnedHearts()
    {
        for (int i = 0; i < spawnedHearts.Count; i++)
        {
            if (spawnedHearts[i] != null)
                Destroy(spawnedHearts[i]);
        }
        spawnedHearts.Clear();
    }

    private void WarnIfNotChild(RectTransform child, string fieldName)
    {
        if (trackRect != null && child.parent != trackRect)
            Debug.LogWarning($"{name}: {fieldName} isn't a child of trackRect — its position is " +
                              "computed from trackRect's rect, so it needs to be parented there.");
    }
}