using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the three progress meters for RhythmSequenceMinigame — win, fail, and
/// injury — filling each toward its threshold as successCount/failCount/riskyMissCount
/// change. Subscribes to OnProgressChanged rather than polling every frame.
///
/// Setup: each meter should be a UI Image with Image Type set to "Filled"
/// (Fill Method Horizontal/Vertical/Radial, whichever matches your art).
/// </summary>
public class RhythmProgressMetersUI : MonoBehaviour
{
    [SerializeField] private RhythmSequenceMinigame minigame;

    [Header("Meters (Image Type: Filled)")]
    [SerializeField] private Image winMeter;
    [SerializeField] private Image failMeter;
    [SerializeField] private Image injuryMeter;

    private void OnEnable()
    {
        if (minigame == null) return;
        minigame.OnProgressChanged += HandleProgressChanged;
        // Prime the meters with current values immediately, in case some
        // progress already happened before this UI was enabled.
        HandleProgressChanged(minigame.successCount, minigame.failCount, minigame.riskyMissCount);
    }

    private void OnDisable()
    {
        if (minigame == null) return;
        minigame.OnProgressChanged -= HandleProgressChanged;
    }

    private void HandleProgressChanged(int successCount, int failCount, int riskyMissCount)
    {
        SetFill(winMeter, successCount, minigame.winThreshold);
        SetFill(failMeter, failCount, minigame.failThreshold);
        SetFill(injuryMeter, riskyMissCount, minigame.injuryThreshold);
    }

    private void SetFill(Image meter, int current, int threshold)
    {
        if (meter == null || threshold <= 0) return;
        meter.fillAmount = Mathf.Clamp01((float)current / threshold);
    }
}