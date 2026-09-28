using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pseudo loading screen shown for a few seconds during travel (see
/// TravelMapController.TravelSequence). Nothing is actually being loaded - this exists purely
/// to cover the moment the follow-camera takes to catch up to the player after a teleport, and
/// to give the transition some personality instead of a hard cut or an obvious camera snap
/// (e.g. an animation of the player waiting on a bus, cycling, whatever fits the location).
///
/// Whoever calls Play() (TravelMapController) waits for it to finish before calling the
/// destination's OnArrival() - so this doubles as the "force wait" for anything that shouldn't
/// start until the transition is actually done, like ParkActivity's run minigame kicking off
/// mid camera-move.
/// </summary>
public class TravelLoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [Tooltip("The character/rig playing the loading animation.")]
    [SerializeField] private Animator animator;

    [Tooltip("One is picked at random each time Play() runs, and played via " +
             "Animator.Play(clip.name) - so each clip's NAME must match a state name in " +
             "Animator's Controller (Unity does this automatically if a state was created " +
             "by dragging the clip straight into the Controller, which is the easiest way " +
             "to set these up). Leave empty to show just the bare panel with no animation.")]
    [SerializeField] private List<AnimationClip> idleClips = new List<AnimationClip>();

    [Header("Duration")]
    [Tooltip("A random value between Min/Max Duration is picked each time, so back-to-back " +
             "trips don't feel identical. Set both to the same value for a fixed duration.")]
    [SerializeField] private float minDuration = 2f;
    [SerializeField] private float maxDuration = 5f;

    /// <summary>Shows the panel, plays one randomly-chosen clip, waits a randomized duration,
    /// hides everything again. Callers should `yield return loadingScreen.Play()` so whatever
    /// runs next is forced to wait for the full duration rather than firing immediately after
    /// the teleport.</summary>
    public IEnumerator Play()
    {
        if (panel != null) panel.SetActive(true);

        if (animator != null && idleClips.Count > 0)
        {
            AnimationClip chosen = idleClips[Random.Range(0, idleClips.Count)];
            if (chosen != null)
                animator.Play(chosen.name, 0, 0f);
        }

        float duration = Random.Range(minDuration, maxDuration);
        yield return new WaitForSeconds(duration);

        if (panel != null) panel.SetActive(false);
    }
}