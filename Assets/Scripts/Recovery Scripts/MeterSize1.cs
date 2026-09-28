using UnityEngine;

public class MeterSize1 : MonoBehaviour
{
    [SerializeField]
    private GameObject injury;
    [SerializeField]
    private GameObject winState;
    [SerializeField]
    private GameObject loseState;
    [SerializeField]
    private float motivation;
    [SerializeField]
    private HealthAndMotivation hnM;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        motivation = hnM.currentMotivation;

        injury.transform.localScale = new Vector3(
                injury.transform.localScale.x,
                injury.transform.localScale.y,
                injury.transform.localScale.z - motivation * 1f
            );

        winState.transform.localScale = new Vector3(
            winState.transform.localScale.x,
            winState.transform.localScale.y,
            winState.transform.localScale.z + motivation * 2f
        );

        loseState.transform.localScale = new Vector3(
            loseState.transform.localScale.x,
            loseState.transform.localScale.y,
            loseState.transform.localScale.z - motivation * 1f
        );
    }

    // Update is called once per frame
    void Update()
    {

    }
}
