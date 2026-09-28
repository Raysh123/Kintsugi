using System;
using UnityEngine;

/// <summary>
/// Persistent singleton — survives scene loads so fight scenes, recovery
/// minigames, and UI meters all read/write the same health & motivation
/// values regardless of which scene they're in.
/// </summary>
public class HealthAndMotivation : MonoBehaviour
{
    public static HealthAndMotivation instance;

    public float currentHealth;
    public float currentMotivation;

    /// Fired whenever health changes, with the new clamped value. Meters/UI can subscribe.
    public event Action<float> OnHealthChanged;
    public event Action<float> OnMotivationChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, 100f);
        currentMotivation = Mathf.Clamp(currentMotivation, 0f, 100f);
    }

    public void ModifyHealth(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, 100f);
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void ModifyMotivation(float amount)
    {
        currentMotivation = Mathf.Clamp(currentMotivation + amount, 0f, 100f);
        OnMotivationChanged?.Invoke(currentMotivation);
    }
}