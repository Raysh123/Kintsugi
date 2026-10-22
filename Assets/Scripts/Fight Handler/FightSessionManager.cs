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

    /// <summary>True when the last fight ended because the injury meter filled (PostFightHandler shows the injured
    /// panel instead of the normal loss).</summary>
    public static bool injuredLastFight { get; private set; }

    public static void ClearAll()
    {
        returnSceneName = null;
        returnSpawnPointId = null;
        opponentId = null;
        hasPendingResult = false;
        wonLastFight = false;
        injuredLastFight = false;
    }

    /// <summary>Call just before reporting the result of a fight that ended in injury.</summary>
    public static void MarkInjured()
    {
        injuredLastFight = true;
    }

    /// <summary>
    /// Call this from the RPG scene (FightTrigger) to hand off into the fight scene.
    /// </summary>
    public static void BeginFight(string fightSceneName, string returnSpawnId, string opponent)
    {
        returnSceneName = SceneManager.GetActiveScene().name;
        returnSpawnPointId = returnSpawnId;
        opponentId = opponent;
        hasPendingResult = false;
        injuredLastFight = false;
        FightStats.Reset();

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
        if (won) injuredLastFight = false;

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