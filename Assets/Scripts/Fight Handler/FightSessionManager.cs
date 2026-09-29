using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static bridge between the RPG scene and the separately-built fighting scene.
/// Neither side needs to know about the other's internals — the RPG scene calls
/// BeginFight() to hand off into a match, and the fight scene's own GameManager
/// (or whatever decides win/lose) calls ReportResult() when the match ends.
///
/// Static fields survive a SceneManager.LoadScene() call (they only reset on domain
/// reload — entering Play mode or a script recompile — not on scene changes), so no
/// DontDestroyOnLoad object is needed here.
/// </summary>
public static class FightSessionManager
{
    public static string returnSceneName { get; private set; }
    public static string returnSpawnPointId { get; private set; }
    public static string opponentId { get; private set; }

    public static bool hasPendingResult { get; private set; }
    public static bool wonLastFight { get; private set; }

    /// <summary>
    /// Call this from the RPG scene (FightTrigger) to hand off into the fight scene.
    /// </summary>
    public static void BeginFight(string fightSceneName, string returnSpawnId, string opponent)
    {
        returnSceneName = SceneManager.GetActiveScene().name;
        returnSpawnPointId = returnSpawnId;
        opponentId = opponent;
        hasPendingResult = false;

        Debug.Log($"[FightSessionManager] Beginning fight '{opponent}' in scene '{fightSceneName}'. " +
                  $"Will return to '{returnSceneName}' at spawn '{returnSpawnId}'.");

        SceneManager.LoadScene(fightSceneName);
    }

    /// <summary>
    /// Call this from your fight scene's GameManager (or wherever the match result is
    /// decided) when the match ends. This is the one hook that needs adding to the
    /// separately-built fight system — a single line at the point it currently decides
    /// win/lose, e.g.:
    ///     FightSessionManager.ReportResult(playerWon);
    /// </summary>
    public static void ReportResult(bool won)
    {
        wonLastFight = won;
        hasPendingResult = true;

        Debug.Log($"[FightSessionManager] Fight result reported: {(won ? "WIN" : "LOSS")}. Returning to '{returnSceneName}'.");

        SceneManager.LoadScene(returnSceneName);
    }

    /// <summary>
    /// Called by PostFightHandler once it has consumed the pending result, so a second
    /// Start() (e.g. from re-entering the scene later) doesn't replay the same result.
    /// </summary>
    public static void ClearPendingResult()
    {
        hasPendingResult = false;
    }
}
