using UnityEngine;

/// <summary>
/// Trails on the enemy's hands and feet that show when an attack is coming.
///
///   * Trails are OFF normally. While he is attacking they are ON in a plain colour.
///   * A moment before the PERFECT BLOCK WINDOW of each hit opens, the limb that is about to strike
///     (hands for a punch, feet for a kick - read from the clip's own hit events) turns RED, and stays red
///     while the window runs and while the hit is live. When the hit ends it goes back to the plain colour.
///
/// It only READS the enemy (state, his animator's clip events, hit-window flag) - it doesn't change how he fights.
/// Works with every combo as long as the clips carry DealDamage (or OpenEnemyHitWindow) events:
/// int 0 = punch (hands go red), int 1 = kick (feet go red).
///
/// Setup: see the field tooltips. Put it on any object (e.g. the enemy root).
/// The trails' MATERIAL must use vertex colours (e.g. URP Particles/Unlit or Sprites/Default), or the colour change
/// won't show. This script sets each trail's Color GRADIENT from the two gradients below (it overrides the one on the trail).
/// </summary>
public class AttackTrails : MonoBehaviour
{
    [Header("Trails (TrailRenderers on the limbs; leave 'Emitting' unticked on them)")]
    [SerializeField] private TrailRenderer[] handTrails;
    [SerializeField] private TrailRenderer[] footTrails;

    [Header("Gradients (left = the limb / newest part of the trail, right = the tail end)")]
    [Tooltip("Trail look while he is attacking but no hit is imminent. Edit the colour keys AND the alpha keys " +
             "(top markers = alpha) - e.g. fade the alpha to 0 at the right so the tail vanishes.")]
    [SerializeField] private Gradient normalGradient = MakeGradient(new Color(1f, 1f, 1f), 0.55f, new Color(0.6f, 0.8f, 1f));
    [Tooltip("The limb about to hit uses this gradient for the perfect-block window.")]
    [SerializeField] private Gradient perfectGradient = MakeGradient(new Color(1f, 0.15f, 0.1f), 1f, new Color(1f, 0.55f, 0.1f));
    [Tooltip("Trail width multiplier while red (1 = unchanged). A little wider makes the cue pop.")]
    [SerializeField] private float redWidthMultiplier = 1.25f;

    [Header("Timing")]
    [Tooltip("(Only used when the player's 'Perfect By Distance' is OFF - otherwise the trail turns red when his limb is within " +
             "the player's Perfect Block Distance.) Turns red this many seconds BEFORE the perfect-block window opens (reaction cushion). " +
             "The window itself is the player's Perfect Block Window (read automatically) and ends when the hit lands.")]
    [SerializeField] private float redLeadTime = 0.05f;
    [Tooltip("Keep it red this long AFTER the hit stops being live, in seconds. 0 = back to normal at once.")]
    [SerializeField] private float redHoldAfterHit = 0f;
    [Tooltip("Only the limb that is about to strike is turned on. OFF = all trails on during his whole attack " +
             "(the striking limb still turns red).")]
    [SerializeField] private bool onlyStrikingLimbTrail = false;

    [Header("References (auto-found)")]
    [SerializeField] private EnemyStateMachine enemy;
    [SerializeField] private PlayerStateMachine player;

    [SerializeField] private bool logTrails = false;

    /// <summary>True while a trail is red (the perfect-block moment): can be read by tutorials, UI or sound.</summary>
    public bool PerfectWindowActive { get; private set; }
    /// <summary>0 = hands (punch), 1 = feet (kick), -1 = none.</summary>
    public int RedLimb { get; private set; } = -1;
    /// <summary>True from the moment his attack starts (trails on) until it ends.</summary>
    public bool TrailsActive => wasAttacking;

    private bool wasAttacking;
    private int appliedRed = -2;          // which limb group is currently red (-1 none), -2 = nothing applied yet
    private float holdUntil;
    private int holdLimb = -1;
    private float[] baseWidths;           // for the width multiplier
    private TrailRenderer[] all;

    private void Start()
    {
        if (enemy == null) enemy = GetComponentInParent<EnemyStateMachine>();
        if (enemy == null) enemy = FindFirstObjectByType<EnemyStateMachine>();
        if (player == null) player = FindFirstObjectByType<PlayerStateMachine>();

        var list = new System.Collections.Generic.List<TrailRenderer>();
        if (handTrails != null) list.AddRange(handTrails);
        if (footTrails != null) list.AddRange(footTrails);
        all = list.ToArray();
        baseWidths = new float[all.Length];
        for (int i = 0; i < all.Length; i++) if (all[i] != null) baseWidths[i] = all[i].widthMultiplier;

        SetEmitting(handTrails, false);
        SetEmitting(footTrails, false);
        if (enemy == null) Debug.LogWarning("[AttackTrails] No EnemyStateMachine found.");
    }

    private void Update()
    {
        if (enemy == null || enemy.anim == null) return;
        bool attacking = enemy.currentState == EnemyStateMachine.EnemyState.Attack;

        if (!attacking)
        {
            if (wasAttacking) { SetEmitting(handTrails, false); SetEmitting(footTrails, false); ResetLook(); }
            wasAttacking = false;
            PerfectWindowActive = false; RedLimb = -1;
            return;
        }

        if (!wasAttacking)
        {
            // Attack just started: fresh trails (no streak from where he was before), plain colour.
            ClearAll();
            ResetLook();
            wasAttacking = true;
            if (logTrails) Debug.Log($"[AttackTrails] attack started - trails on ({(handTrails != null ? handTrails.Length : 0)} hand, {(footTrails != null ? footTrails.Length : 0)} foot).");
        }

        // Which limb is about to strike (or striking), and is the perfect window on?
        int red = -1;
        int upcoming = -1;
        if (enemy.HitWindowOpen)
        {
            red = enemy.requiredTag == "ePunch" ? 0 : 1;
            upcoming = red;
            holdUntil = Time.time + redHoldAfterHit;
            holdLimb = red;
        }
        else
        {
            if (TryNextHit(out float secs, out int type))
            {
                upcoming = type == 0 ? 0 : 1;
                if (player != null && player.PerfectByDistance)
                {
                    // Distance rule: red exactly while a block pressed NOW would be perfect (his striking limb is close enough).
                    float gap = player.IncomingLimbGap();
                    if (gap >= 0f && gap <= player.PerfectBlockDistance) red = upcoming;
                }
                else
                {
                    float window = player != null ? player.PerfectBlockWindow : 0.15f;
                    if (secs <= window + Mathf.Max(0f, redLeadTime)) red = upcoming;
                }
            }
            if (red < 0 && Time.time < holdUntil) red = holdLimb;
        }

        // Which trails are emitting.
        if (onlyStrikingLimbTrail)
        {
            int show = upcoming >= 0 ? upcoming : holdLimb;
            SetEmitting(handTrails, show == 0);
            SetEmitting(footTrails, show == 1);
        }
        else
        {
            SetEmitting(handTrails, true);
            SetEmitting(footTrails, true);
        }

        PerfectWindowActive = red >= 0;
        RedLimb = red;
        if (red != appliedRed)
        {
            appliedRed = red;
            ApplyColors(red);
            if (logTrails) Debug.Log($"[AttackTrails] red limb = {(red == 0 ? "hands" : red == 1 ? "feet" : "none")}");
        }
    }

    private void OnDisable()
    {
        if (all == null) return;
        SetEmitting(handTrails, false);
        SetEmitting(footTrails, false);
    }

    // ------------------------------------------------------------------ helpers
    // Seconds from now until the next hit-open event in the playing clip, and its int parameter (0 punch, 1 kick).
    private bool TryNextHit(out float seconds, out int type)
    {
        seconds = 0f; type = -1;
        Animator a = enemy.anim;
        AnimatorClipInfo[] infos = a.GetCurrentAnimatorClipInfo(0);
        if (infos == null || infos.Length == 0 || infos[0].clip == null) return false;
        AnimationClip clip = infos[0].clip;
        AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
        float now = (st.loop ? Mathf.Repeat(st.normalizedTime, 1f) : Mathf.Clamp01(st.normalizedTime)) * clip.length;
        float speed = Mathf.Max(0.01f, a.speed * st.speedMultiplier);
        float best = -1f;
        foreach (AnimationEvent evt in clip.events)
        {
            if ((evt.functionName != "DealDamage" && evt.functionName != "OpenEnemyHitWindow") || evt.time <= now) continue;
            if (best < 0f || evt.time < best) { best = evt.time; type = evt.intParameter; }
        }
        if (best < 0f) return false;
        seconds = (best - now) / speed;
        return true;
    }

    private void ApplyColors(int redLimb)
    {
        ColorGroup(handTrails, redLimb == 0);
        ColorGroup(footTrails, redLimb == 1);
    }

    private void ColorGroup(TrailRenderer[] group, bool red)
    {
        if (group == null) return;
        Gradient g = red ? perfectGradient : normalGradient;
        foreach (var t in group)
        {
            if (t == null) continue;
            t.colorGradient = g;
            int idx = System.Array.IndexOf(all, t);
            float baseW = (idx >= 0 && baseWidths != null) ? baseWidths[idx] : t.widthMultiplier;
            t.widthMultiplier = red ? baseW * redWidthMultiplier : baseW;
        }
    }

    private static Gradient MakeGradient(Color a, float alphaStart, Color b)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
            new[] { new GradientAlphaKey(alphaStart, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private void ResetLook()
    {
        appliedRed = -2;
        holdUntil = 0f; holdLimb = -1;
        ApplyColors(-1);
    }

    private static void SetEmitting(TrailRenderer[] group, bool on)
    {
        if (group == null) return;
        foreach (var t in group) if (t != null && t.emitting != on) t.emitting = on;
    }

    private void ClearAll()
    {
        if (all == null) return;
        foreach (var t in all) if (t != null) t.Clear();
    }
}