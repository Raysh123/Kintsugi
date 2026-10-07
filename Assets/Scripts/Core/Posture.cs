using UnityEngine;
using UnityEngine.UI;

public class Posture : MonoBehaviour
{
    public Slider postureSlider;
    public float dmgAdder = 2f;
    [SerializeField]
    private float drainRate = 0.001f;
    //[SerializeField] private PlayerStateMachine playerStateMachine;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        postureSlider.value -= drainRate;
    }
    public void posDmg(float dmg)
    {
        postureSlider.value += dmg;
    }

}
