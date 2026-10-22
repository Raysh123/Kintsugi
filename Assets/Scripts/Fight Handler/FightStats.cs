/// <summary>Counters for the last fight. Static, so the RPG scene can read them after the fight scene unloads.
/// Reset by FightSessionManager.BeginFight.</summary>
public static class FightStats
{
    public static float time;
    public static int playerHits, enemyHits;
    public static int perfectParries, normalBlocks;
    public static int swaps;                    // swaps that threw the enemy off the mat
    public static int playerScore, enemyScore;   // final score
    public static float healthDelta, motivationDelta;

    public static void Reset()
    {
        time = 0f;
        playerHits = enemyHits = 0;
        perfectParries = normalBlocks = swaps = 0;
        playerScore = enemyScore = 0;
        healthDelta = motivationDelta = 0f;
    }
}