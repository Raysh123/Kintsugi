using System;
using UnityEngine;

public class HealthAndMotivation : MonoBehaviour
{
    public float currentHealth;
    public float currentMotivation;

    private void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, 100f);
        currentMotivation = Mathf.Clamp(currentMotivation, 0f, 100f);
    }

    public void ModifyHealth(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, 100f);
    }

    public void ModifyMotivation(float amount)
    {
        currentMotivation = Mathf.Clamp(currentMotivation + amount, 0f, 100f);
    }
}
