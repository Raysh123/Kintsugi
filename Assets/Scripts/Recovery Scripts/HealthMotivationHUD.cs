using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen Health and Motivation bars. Reads HealthAndMotivation (the persistent singleton),
/// so it shows the same values in the RPG scene, the recovery scene and anywhere else, and it
/// updates live whenever either value changes (a fight injury, a recovery activity, bed rest...).
///
/// Setup: put this on a Canvas object, then for each stat assign EITHER a Slider OR an Image
/// set to Image Type: Filled (Horizontal) - whichever you built the bar with - plus an optional
/// TMP text for the number. Anything you leave empty is just skipped.
///
/// If this lives on a Canvas that persists between scenes, make sure it is a root object. If you
/// put one copy in each scene instead, that works too - each reads the same singleton.
/// </summary>
public class HealthMotivationHUD : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider healthSlider;
    [Tooltip("Alternative to the slider: an Image with Image Type = Filled.")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TMP_Text healthText;
    [Tooltip("Text format. {0} = current value, {1} = max.")]
    [SerializeField] private string healthFormat = "{0:0} / {1:0}";
    [SerializeField] private Color healthColor = new Color(0.85f, 0.2f, 0.25f);
    [Tooltip("Below this value the bar uses Low Health Color. 0 = never.")]
    [SerializeField] private float lowHealthThreshold = 25f;
    [SerializeField] private Color lowHealthColor = new Color(1f, 0.55f, 0.1f);

    [Header("Motivation")]
    [SerializeField] private Slider motivationSlider;
    [SerializeField] private Image motivationFillImage;
    [SerializeField] private TMP_Text motivationText;
    [SerializeField] private string motivationFormat = "{0:0} / {1:0}";
    [SerializeField] private Color motivationColor = new Color(0.25f, 0.6f, 1f);

    [Header("Behaviour")]
    [SerializeField] private float maxValue = 100f;
    [Tooltip("How fast the bars catch up to the real value (fraction of the bar per second). " +
             "0 = snap instantly.")]
    [SerializeField] private float smoothSpeed = 1.5f;
    [Tooltip("Scale pop on a bar's container when its value changes. 1 = no pop.")]
    [SerializeField] private float popScale = 1.08f;
    [SerializeField] private float popDuration = 0.25f;

    private float shownHealth, shownMotivation;     // 0-1, what is currently drawn
    private float targetHealth, targetMotivation;   // 0-1, where it is heading
    private float lastHealth = -1f, lastMotivation = -1f;
    private HealthAndMotivation subscribedTo;
    private Coroutine healthPopRoutine, motivationPopRoutine;

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private IEnumerator BindWhenReady()
    {
        // The singleton can be created a moment after this object wakes up.
        while (HealthAndMotivation.instance == null) yield return null;

        subscribedTo = HealthAndMotivation.instance;
        subscribedTo.OnHealthChanged += HandleHealthChanged;
        subscribedTo.OnMotivationChanged += HandleMotivationChanged;

        // Jump straight to the current values - no animating up from zero on scene load.
        lastHealth = subscribedTo.currentHealth;
        lastMotivation = subscribedTo.currentMotivation;
        targetHealth = shownHealth = Mathf.Clamp01(lastHealth / maxValue);
        targetMotivation = shownMotivation = Mathf.Clamp01(lastMotivation / maxValue);
        Redraw();
    }

    private void Unsubscribe()
    {
        if (subscribedTo == null) return;
        subscribedTo.OnHealthChanged -= HandleHealthChanged;
        subscribedTo.OnMotivationChanged -= HandleMotivationChanged;
        subscribedTo = null;
    }

    private void HandleHealthChanged(float value)
    {
        if (!Mathf.Approximately(value, lastHealth))
            healthPopRoutine = Pop(healthSlider != null ? healthSlider.transform : (healthFillImage != null ? healthFillImage.transform.parent : null), healthPopRoutine);
        lastHealth = value;
        targetHealth = Mathf.Clamp01(value / maxValue);
    }

    private void HandleMotivationChanged(float value)
    {
        if (!Mathf.Approximately(value, lastMotivation))
            motivationPopRoutine = Pop(motivationSlider != null ? motivationSlider.transform : (motivationFillImage != null ? motivationFillImage.transform.parent : null), motivationPopRoutine);
        lastMotivation = value;
        targetMotivation = Mathf.Clamp01(value / maxValue);
    }

    private void Update()
    {
        if (subscribedTo == null) return;

        if (smoothSpeed <= 0f)
        {
            shownHealth = targetHealth;
            shownMotivation = targetMotivation;
        }
        else
        {
            float step = smoothSpeed * Time.unscaledDeltaTime;
            shownHealth = Mathf.MoveTowards(shownHealth, targetHealth, step);
            shownMotivation = Mathf.MoveTowards(shownMotivation, targetMotivation, step);
        }
        Redraw();
    }

    private void Redraw()
    {
        Apply(healthSlider, healthFillImage, shownHealth,
              lastHealth < lowHealthThreshold && lowHealthThreshold > 0f ? lowHealthColor : healthColor);
        Apply(motivationSlider, motivationFillImage, shownMotivation, motivationColor);

        if (healthText != null && lastHealth >= 0f)
            healthText.text = string.Format(healthFormat, lastHealth, maxValue);
        if (motivationText != null && lastMotivation >= 0f)
            motivationText.text = string.Format(motivationFormat, lastMotivation, maxValue);
    }

    private static void Apply(Slider slider, Image fillImage, float normalized, Color color)
    {
        if (slider != null)
        {
            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, normalized);
            if (slider.fillRect != null)
            {
                Image img = slider.fillRect.GetComponent<Image>();
                if (img != null) img.color = color;
            }
        }
        if (fillImage != null)
        {
            fillImage.fillAmount = normalized;
            fillImage.color = color;
        }
    }

    private Coroutine Pop(Transform target, Coroutine running)
    {
        if (target == null || popScale <= 1f || !isActiveAndEnabled) return running;
        if (running != null) StopCoroutine(running);
        return StartCoroutine(PopRoutine(target));
    }

    private IEnumerator PopRoutine(Transform target)
    {
        Vector3 baseScale = Vector3.one;
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / popDuration) * Mathf.PI); // up and back down
            target.localScale = Vector3.Lerp(baseScale, baseScale * popScale, k);
            yield return null;
        }
        target.localScale = baseScale;
    }
}
