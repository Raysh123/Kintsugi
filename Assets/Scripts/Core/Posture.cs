using UnityEngine;
using UnityEngine.UI;

public class Posture : MonoBehaviour
{
    public Slider postureSlider;
    public float dmgAdder = 2f;

    [Tooltip("How much posture drains back down per SECOND (frame-rate independent - the old per-frame " +
             "value drained several times faster in a build running at a higher FPS than in the " +
             "editor). 0.06 equals the old 0.001 per frame at 60 FPS.")]
    [SerializeField] private float drainPerSecond = 0.06f;

    [Tooltip("Posture doesn't start draining until this many seconds after it last went UP " +
             "(a hit, a block...). Every new increase restarts the pause.")]
    [SerializeField] private float drainDelay = 1.0f;

    private float lastValue;
    private float delayTimer;

    void Start()
    {
        if (postureSlider != null) lastValue = postureSlider.value;
    }

    void Update()
    {
        if (postureSlider == null) return;

        // Catches every increase, whether it came through posDmg or someone setting the slider directly.
        if (postureSlider.value > lastValue + 0.0001f) delayTimer = drainDelay;

        if (delayTimer > 0f)
            delayTimer -= Time.deltaTime;
        else
            postureSlider.value -= drainPerSecond * Time.deltaTime;

        lastValue = postureSlider.value;
    }

    public void posDmg(float dmg)
    {
        postureSlider.value += dmg;
        delayTimer = drainDelay;
        lastValue = postureSlider.value;
    }
}