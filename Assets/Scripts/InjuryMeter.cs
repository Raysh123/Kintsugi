using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

//Always remember to add InputHandler in the scene and reference it properly
public class InjuryMeter : MonoBehaviour
{
    public Slider injuryMeterValue;
    public float injuryMeterDepleteRate = 0.01f;
    public static InjuryMeter instance;
    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (injuryMeterValue.value > 0.98f)
        {
            Debug.Log("injured");
        }
        else
        {
            injuryMeterValue.value -= injuryMeterDepleteRate;
        }
    }
}
