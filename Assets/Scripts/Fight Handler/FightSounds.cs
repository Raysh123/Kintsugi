using UnityEngine;

/// <summary>
/// Every combat sound in the fight scene, in one place. DmgDetection, PlayerStateMachine and
/// EnemyStateMachine call these static methods at the right moment, so those scripts need no
/// sound fields of their own. Put ONE of these in the fight scene (next to the AudioManager)
/// and type your SoundDefinition ids into the fields below - a blank id plays nothing.
///
///   Punch / Kick  = the swing, as the strike's hit window opens
///   Block         = an attack of that fighter's being blocked (the impact of the block)
///   Hit           = that fighter getting hit and conceding the point
/// </summary>
public class FightSounds : MonoBehaviour
{
    public static FightSounds instance;
    private static bool warnedMissing;

    [Header("Player")]
    [SerializeField] private string playerPunch = "player_punch";
    [SerializeField] private string playerKick = "player_kick";
    [SerializeField] private string playerBlock = "player_block";
    [SerializeField] private string playerHit = "player_hit";

    [Header("Enemy")]
    [SerializeField] private string enemyPunch = "enemy_punch";
    [SerializeField] private string enemyKick = "enemy_kick";
    [SerializeField] private string enemyBlock = "enemy_block";
    [SerializeField] private string enemyHit = "enemy_hit";

    [Header("Shared")]
    [Tooltip("Played when EITHER fighter is stunned (posture broken).")]
    [SerializeField] private string stun = "stun";

    [Header("Optional extras (blank = silent)")]
    [Tooltip("Played instead of the player's normal block sound when it's a PERFECT block.")]
    [SerializeField] private string perfectBlock = "";
    [Tooltip("Played when an attack lands on a leg collider (posture damage only).")]
    [SerializeField] private string legHit = "";

    private void Awake() => instance = this;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private static bool Ready()
    {
        if (instance != null) return true;
        if (!warnedMissing)
        {
            warnedMissing = true;
            Debug.LogWarning("[FightSounds] No FightSounds component in this scene, so combat sounds " +
                             "won't play. Add one and fill in the sound ids.");
        }
        return false;
    }

    public static void PlayerSwing(bool isKick) { if (Ready()) Sfx.Play(isKick ? instance.playerKick : instance.playerPunch); }
    public static void EnemySwing(bool isKick) { if (Ready()) Sfx.Play(isKick ? instance.enemyKick : instance.enemyPunch); }
    public static void PlayerHit() { if (Ready()) Sfx.Play(instance.playerHit); }
    public static void EnemyHit() { if (Ready()) Sfx.Play(instance.enemyHit); }
    public static void EnemyBlocked() { if (Ready()) Sfx.Play(instance.enemyBlock); }
    public static void Stun() { if (Ready()) Sfx.Play(instance.stun); }
    public static void LegHit() { if (Ready()) Sfx.Play(instance.legHit); }

    public static void PlayerBlocked(bool perfect)
    {
        if (!Ready()) return;
        Sfx.Play(perfect && !string.IsNullOrEmpty(instance.perfectBlock) ? instance.perfectBlock : instance.playerBlock);
    }
}