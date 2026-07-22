using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Works exactly like RecoveryInteraction — place it on a trigger in the scene,
/// assign an activity model with its own animator, and a dedicated camera.
/// 
/// While in the trigger zone the player can:
///   E / Submit      — start a recovery activity on the bed (same as any other interaction)
///   Space / Cancel  — go to sleep (fade out, wake up anim, fade in, new day)
/// </summary>
public class BedInteraction : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private GameObject playerMeshChild;

    [Header("Activity Model")]
    [Tooltip("Dedicated model for bed interactions — disabled by default in the scene.")]
    [SerializeField] private GameObject activityModel;
    [SerializeField] private Animator activityAnimator;
    [Tooltip("Animation that plays while doing a bed recovery activity.")]
    [SerializeField] private string recoveryAnimation;
    [Tooltip("Animation that plays when the player falls asleep.")]
    [SerializeField] private string fallAsleepAnimation;
    [Tooltip("Animation that plays when the player wakes up.")]
    [SerializeField] private string wakeUpAnimation;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Fade")]
    [Tooltip("A full-screen black Image on a Canvas set to Screen Space - Overlay. " +
             "Set its alpha to 0 by default.")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Calendar")]
    [SerializeField] private ActivityDefinition recoveryActivity;

    [Header("Rewards & Penalties")]
    [SerializeField] private float winHealth;
    [SerializeField] private float winMotivation;
    [SerializeField] private float loseHealth;
    [SerializeField] private float loseMotivation;
    [SerializeField] private float injuryHealth;
    [SerializeField] private float injuryMotivation;

    [Header("Minigame (optional bed recovery)")]
    [SerializeField] private Pointer pointer;
    [SerializeField] private GameObject meter;

    private bool canInteract;
    private bool meterActive;
    private bool isSleeping;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canInteract = true;
            player = other.GetComponent<PlayerMovement>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canInteract = false;
            player = null;
        }
    }

    private void Start()
    {
        if (activityModel != null) activityModel.SetActive(false);
        if (meter != null) meter.SetActive(false);
        if (fadeImage != null) SetFadeAlpha(0f);
    }

    private void Update()
    {
        if (!canInteract || isSleeping) return;

        // E — start bed recovery activity
        if (!meterActive && !player.interacting)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit"))
                BeginRecovery();
        }

        // Space — go to sleep (available any time while in trigger, not just during activity)
        if (!meterActive && !player.interacting)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
                StartCoroutine(SleepSequence());
        }
    }

    // ── Recovery activity (same as RecoveryInteraction) ────────────────────────

    private void BeginRecovery()
    {
        if (recoveryActivity == null) return;

        if (!CalendarManager.instance.TryConsumeActivity(recoveryActivity))
        {
            Debug.Log($"{recoveryActivity.displayName} already done today.");
            return;
        }

        player.interacting = true;
        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (activityModel != null)
        {
            activityModel.SetActive(true);
            if (activityAnimator != null && !string.IsNullOrEmpty(recoveryAnimation))
                activityAnimator.Play(recoveryAnimation);
        }

        SceneCameraManager.instance.ActivateCamera(activityCamera);
        meterActive = true;
        if (meter != null) meter.SetActive(true);
        StartCoroutine(RecoveryRoutine());
    }

    private IEnumerator RecoveryRoutine()
    {
        yield return new WaitForSeconds(10f);
        StartCoroutine(EndRecovery());
    }

    private IEnumerator EndRecovery()
    {
        if (pointer != null)
        {
            if (pointer.won)
            {
                Debug.Log("Won");
                HealthAndMotivation.instance.ModifyHealth(winHealth);
                HealthAndMotivation.instance.ModifyMotivation(winMotivation);
            }
            else if (pointer.lost)
            {
                Debug.Log("Lost");
                HealthAndMotivation.instance.ModifyHealth(loseHealth);
                HealthAndMotivation.instance.ModifyMotivation(loseMotivation);
            }
            else if (pointer.injured)
            {
                Debug.Log("Injured");
                HealthAndMotivation.instance.ModifyHealth(injuryHealth);
                HealthAndMotivation.instance.ModifyMotivation(injuryMotivation);
            }
        }

        yield return new WaitForSeconds(0.01f);
        if (activityModel != null) activityModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);
        SceneCameraManager.instance.DeactivateCamera(activityCamera);
        CalendarManager.instance.AdvanceTime(recoveryActivity.timeCostMinutes);
        if (meter != null) meter.SetActive(false);
        player.interacting = false;
        meterActive = false;
    }

    // ── Sleep sequence ──────────────────────────────────────────────────────────

    private IEnumerator SleepSequence()
    {
        isSleeping = true;
        player.interacting = true;

        // Show activity model, play fall-asleep animation
        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (activityModel != null)
        {
            activityModel.SetActive(true);
            if (activityAnimator != null && !string.IsNullOrEmpty(fallAsleepAnimation))
                activityAnimator.Play(fallAsleepAnimation);
        }

        SceneCameraManager.instance.ActivateCamera(activityCamera);

        // Wait a moment for the fall-asleep anim to play before fading
        yield return new WaitForSeconds(1.5f);

        // Fade to black
        yield return StartCoroutine(Fade(0f, 1f));

        // Advance the calendar to next morning while screen is black
        CalendarManager.instance.Sleep();

        // Short pause on black screen
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(Fade(1f, 0f));
        yield return new WaitForSeconds(.5f);

        if (activityAnimator != null && !string.IsNullOrEmpty(wakeUpAnimation))
            activityAnimator.Play(wakeUpAnimation);

        yield return new WaitForSeconds(2f);

        if (activityModel != null) activityModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);
        SceneCameraManager.instance.DeactivateCamera(activityCamera);

        player.interacting = false;
        isSleeping = false;
    }

    // ── Fade helper ─────────────────────────────────────────────────────────────

    private IEnumerator Fade(float fromAlpha, float toAlpha)
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, elapsed / fadeDuration));
            yield return null;
        }
        SetFadeAlpha(toAlpha);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}