using UnityEngine;

/// <summary>
/// Bonus fix while wiring in the singleton: previously this only read
/// motivation once in Start() and never updated again. Now it subscribes to
/// HealthAndMotivation's change event, so the meter updates live whenever
/// motivation actually changes (e.g. after winning/losing a recovery minigame).
/// </summary>
public class MeterSize1 : MonoBehaviour
{
    [SerializeField] private GameObject injury;
    [SerializeField] private GameObject winState;
    [SerializeField] private GameObject loseState;

    private Vector3 injuryBaseScale;
    private Vector3 winBaseScale;
    private Vector3 loseBaseScale;

    void Start()
    {
        injuryBaseScale = injury.transform.localScale;
        winBaseScale = winState.transform.localScale;
        loseBaseScale = loseState.transform.localScale;

        HealthAndMotivation.instance.OnMotivationChanged += UpdateMeter;
        UpdateMeter(HealthAndMotivation.instance.currentMotivation);
    }

    private void OnDestroy()
    {
        if (HealthAndMotivation.instance != null)
            HealthAndMotivation.instance.OnMotivationChanged -= UpdateMeter;
    }

    private void UpdateMeter(float motivation)
    {
        injury.transform.localScale = new Vector3(
            injuryBaseScale.x, injuryBaseScale.y, injuryBaseScale.z - motivation * 1f);

        winState.transform.localScale = new Vector3(
            winBaseScale.x, winBaseScale.y, winBaseScale.z + motivation * 2f);

        loseState.transform.localScale = new Vector3(
            loseBaseScale.x, loseBaseScale.y, loseBaseScale.z - motivation * 1f);
    }
}