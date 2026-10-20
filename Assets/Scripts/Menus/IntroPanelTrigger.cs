using System.Collections;
using UnityEngine;

/// <summary>
/// Shows an image panel in the intro passage scene and waits for the player to close it with A (or E / Submit).
/// Use one of these per panel:
///   - Panel 1 (controls: left stick to walk, RB / LB to turn the camera): tick Show On Start.
///   - Panel 2: put this on a trigger collider further down the passage; it opens when the player walks in.
/// The player's movement is locked while a panel is open. Each panel only ever shows once per visit to the scene.
/// Keep the panel objects INACTIVE in the scene.
/// </summary>
public class IntroPanelTrigger : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private PlayerMovement player;
    [Tooltip("Open by itself when the scene starts (after Start Delay), instead of waiting for the player to walk into a trigger.")]
    [SerializeField] private bool showOnStart;
    [SerializeField] private float startDelay = 0.8f;
    [Tooltip("Seconds before the panel can be closed, so a stray press doesn't skip it.")]
    [SerializeField] private float minShow = 0.6f;
    [Tooltip("Only objects with this tag open the panel when they walk into the trigger.")]
    [SerializeField] private string requiredTag = "Player";

    private bool shown;

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (showOnStart) StartCoroutine(ShowPanel(startDelay));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (showOnStart || shown) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        StartCoroutine(ShowPanel(0f));
    }

    private IEnumerator ShowPanel(float delay)
    {
        if (shown || panel == null) yield break;
        shown = true;

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (player != null) player.movementLocked = true;
        panel.SetActive(true);

        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            if (t >= minShow &&
                (Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit")))
                break;
            yield return null;
        }

        panel.SetActive(false);
        yield return null;
        if (player != null) player.movementLocked = false;
    }
}
