using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the comic-strip cutscene as the game's opening beat for the EGX build - but ONLY the
/// first time, and only once the story is actually in FriendVisit. Two separate gates:
///
///   - Phase gate: CurrentPhase == FriendVisit. Mirrors BedRestPhaseController's own
///     SubscribeWhenReady() - StoryManager.instance isn't guaranteed to exist yet the instant
///     Start() runs, so this waits a frame at a time for it exactly the same way that method
///     does, rather than checking once and silently giving up if it's still null.
///   - Played-once gate: PersistentGameFlags, same one-time pattern as AudioCollisionTrigger/
///     AudioEventTrigger's onlyTriggerOnce and the recovery interactions' onboarding tutorials
///     elsewhere in this project. This is the one that actually stops it replaying every time
///     the player walks back into this scene after a fight - the phase alone would still be
///     FriendVisit on every one of those return trips, so phase-only gating isn't enough.
///
/// Setup: put this on an empty GameObject in the FriendVisit starting scene, and assign the
/// SAME ComicCutscene strip/panels BedRestPhaseController used to reference as its
/// bedRestEndCutscene (move that component here, or duplicate it) — ComicCutscene itself
/// already handles locking player movement/camera for the duration and firing
/// OnCutsceneComplete when it's done, so nothing else needs to change.
/// </summary>
public class GameIntroController : MonoBehaviour
{
    [SerializeField] private ComicCutscene introCutscene;

    [Tooltip("Leave empty to auto-generate one from this component.")]
    [SerializeField] private string playedFlagId;

    [Tooltip("Extra delay (seconds, real-time) after StoryManager is ready before actually " +
             "playing the cutscene. Turning the strip on manually a moment after scene load " +
             "worked every time in testing, while turning it on immediately didn't - something " +
             "else running in that first instant was fighting over the same GameObject. This " +
             "sidesteps whatever that is without needing to track it down.")]
    [SerializeField] private float extraStartupDelay = 1f;

    private string PlayedFlagId => string.IsNullOrEmpty(playedFlagId)
        ? $"GameIntroController_{GetInstanceID()}_comic_played"
        : playedFlagId;

    private void Start()
    {
        StartCoroutine(TryPlayIntro());
    }

    private IEnumerator TryPlayIntro()
    {
        float waitedSeconds = 0f;
        while (StoryManager.instance == null)
        {
            waitedSeconds += Time.unscaledDeltaTime;
            if (waitedSeconds > 5f)
            {
                Debug.LogWarning("[GameIntro] StoryManager.instance never appeared after 5s - giving up.");
                yield break;
            }
            yield return null;
        }

        bool alreadyPlayed = PersistentGameFlags.instance != null &&
                              PersistentGameFlags.instance.HasFlag(PlayedFlagId);

        if (StoryManager.instance.CurrentPhase != StoryPhase.FriendVisit || alreadyPlayed)
        {
            Debug.Log($"[GameIntro] Not playing intro - phase={StoryManager.instance.CurrentPhase}, " +
                      $"alreadyPlayed={alreadyPlayed}.");
            yield break;
        }

        if (introCutscene == null)
        {
            Debug.LogWarning("[GameIntro] No Intro Cutscene assigned in the Inspector - nothing will play.");
            yield break;
        }

        if (extraStartupDelay > 0f)
        {
            Debug.Log($"[GameIntro] Waiting {extraStartupDelay}s before playing, to clear whatever " +
                      "else runs in the first instant of scene load.");
            yield return new WaitForSecondsRealtime(extraStartupDelay);
        }

        // ComicCutscene.Play() does StartCoroutine(RunCutscene()) - if its GameObject was left
        // disabled in the scene (common here, since it used to only ever get SetActive(true) by
        // some other script's logic), StartCoroutine silently fails with "Coroutine couldn't be
        // started because the game object is inactive" and NOTHING happens, no exception. Force
        // it active first so that can't be the reason this doesn't play.
        if (!introCutscene.gameObject.activeSelf)
        {
            Debug.Log("[GameIntro] Intro cutscene's GameObject was inactive - activating it.");
            introCutscene.gameObject.SetActive(true);
        }

        if (PersistentGameFlags.instance != null)
            PersistentGameFlags.instance.SetFlag(PlayedFlagId);
        else
            Debug.LogWarning("[GameIntro] PersistentGameFlags.instance is null - this won't be remembered, " +
                              "the intro will replay on next load.");

        Debug.Log("[GameIntro] Playing intro cutscene.");
        introCutscene.Play();
    }
}