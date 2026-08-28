using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic "go to sleep" bed interaction — works in ANY story phase, at ANY
/// bed in the game. Space/Cancel to sleep: fade out, advance the calendar
/// via CalendarManager.Sleep(), fade in, done.
///
/// Defers entirely during the BedRest phase — BedRestPhaseController owns
/// sleep during that phase specifically, since it needs the player's model
/// to STAY as its bed rig throughout the whole phase rather than reverting
/// to the normal mesh after every sleep the way this script does.
/// </summary>
public class BedInteraction : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private GameObject playerMeshChild;

    [Header("Sleep Model")]
    [Tooltip("Dedicated model used only for the duration of the sleep cutscene — disabled by default in the scene.")]
    [SerializeField] private GameObject sleepModel;
    [SerializeField] private Animator sleepAnimator;
    [SerializeField] private string fallAsleepAnimation;
    [SerializeField] private string wakeUpAnimation;

    [Header("Camera")]
    [SerializeField] private Camera activityCamera;

    [Header("Fade")]
    [Tooltip("A full-screen black Image on a Canvas set to Screen Space - Overlay. " +
             "Set its alpha to 0 by default.")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    private bool canInteract;
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
        if (sleepModel != null) sleepModel.SetActive(false);
        if (fadeImage != null) SetFadeAlpha(0f);
    }

    private void Update()
    {
        if (!canInteract || isSleeping || player == null || player.interacting) return;

        // Defer entirely during BedRest — BedRestPhaseController owns sleep for that phase.
        if (StoryManager.instance != null && StoryManager.instance.CurrentPhase == StoryPhase.BedRest)
            return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
        {
            if (CalendarManager.instance == null || CalendarManager.instance.CurrentPhase != DayPhase.Night)
            {
                Debug.Log("Can only sleep at Night.");
                return;
            }

            StartCoroutine(SleepSequence());
        }
    }

    public IEnumerator SleepSequence()
    {
        isSleeping = true;
        player.interacting = true;

        if (playerMeshChild != null) playerMeshChild.SetActive(false);
        if (sleepModel != null)
        {
            sleepModel.SetActive(true);
            if (sleepAnimator != null && !string.IsNullOrEmpty(fallAsleepAnimation))
                sleepAnimator.Play(fallAsleepAnimation);
        }

        SceneCameraManager.instance.ActivateCamera(activityCamera);

        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(Fade(0f, 1f));

        CalendarManager.instance.Sleep();

        yield return new WaitForSeconds(0.5f);

        if (sleepAnimator != null && !string.IsNullOrEmpty(wakeUpAnimation))
            sleepAnimator.Play(wakeUpAnimation);

        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(Fade(1f, 0f));
        yield return new WaitForSeconds(0.5f);

        SceneCameraManager.instance.DeactivateCamera(activityCamera);

        if (sleepModel != null) sleepModel.SetActive(false);
        if (playerMeshChild != null) playerMeshChild.SetActive(true);

        player.interacting = false;
        isSleeping = false;
    }

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