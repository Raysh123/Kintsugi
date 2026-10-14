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
    [Tooltip("Played when an attack lands on a leg collider (posture damage only). Used for either fighter unless " +
             "the player / enemy ones below are filled in.")]
    [SerializeField] private string legHit = "";
    [Tooltip("Leg hit ON THE PLAYER (the flinch). Blank = the shared Leg Hit sound above.")]
    [SerializeField] private string playerLegHit = "";
    [Tooltip("Leg hit ON THE ENEMY. Blank = the shared Leg Hit sound above.")]
    [SerializeField] private string enemyLegHit = "";

    [Tooltip("The player's flinch / grunt when the leg-hit hurt animation plays (separate from the impact sound above). Blank = silent.")]
    [SerializeField] private string playerLegHurt = "";

    [Header("Swap (one sound per phase, in order)")]
    [Tooltip("Phase 1 - SWAP READY: the grab, as the ready animation starts. Blank = silent.")]
    [SerializeField] private string swapReady = "";
    [Tooltip("Phase 2 - SWAP: the two fighters trading places (a whoosh), as the swap phase starts. Blank = silent.")]
    [SerializeField] private string swap = "";
    [Tooltip("Phase 3 - PUSH: played when the hands connect (after Push Impact Delay). Blank = silent.")]
    [SerializeField] private string swapPush = "";

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
    public static void PlayerLegHit() { if (Ready()) Sfx.Play(string.IsNullOrEmpty(instance.playerLegHit) ? instance.legHit : instance.playerLegHit); }
    public static void EnemyLegHit() { if (Ready()) Sfx.Play(string.IsNullOrEmpty(instance.enemyLegHit) ? instance.legHit : instance.enemyLegHit); }
    public static void PlayerLegHurt() { if (Ready()) Sfx.Play(instance.playerLegHurt); }
    public static void SwapReady() { if (Ready()) Sfx.Play(instance.swapReady); }
    public static void Swap() { if (Ready()) Sfx.Play(instance.swap); }
    public static void SwapPush() { if (Ready()) Sfx.Play(instance.swapPush); }

    public static void PlayerBlocked(bool perfect)
    {
        if (!Ready()) return;
        Sfx.Play(perfect && !string.IsNullOrEmpty(instance.perfectBlock) ? instance.perfectBlock : instance.playerBlock);
    }
}