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

    [Header("Trail look (the trail colour and the material emission both use these)")]
    [Tooltip("Trail colour while he is attacking but no hit is imminent (0-255 values: 191 191 191).")]
    [SerializeField] private Color normalColor = new Color32(191, 191, 191, 255);
    [Tooltip("Trail colour for the limb about to hit, during the perfect-block window (191 6 0).")]
    [SerializeField] private Color perfectColor = new Color32(191, 6, 0, 255);
    [Tooltip("Opacity at the limb end of the trail (it fades to 0 at the tail).")]
    [SerializeField] private float normalAlpha = 0.55f;
    [SerializeField] private float perfectAlpha = 1f;

    [Header("Emission (on the trail MATERIAL - needs a shader with an emission colour, e.g. URP/Lit)")]
    [Tooltip("Drop your emission material here. Every trail gets its own copy of it at start, and the emission colour below is " +
             "set on those copies. Empty = use whatever material is already on each TrailRenderer.")]
    [SerializeField] private Material trailMaterial;
    [SerializeField] private bool useEmission = true;
    [Tooltip("Shader property name of the emission colour (URP/Lit and Standard use _EmissionColor). If the material doesn't " +
             "have it, _EmissiveColor, _EmissionColor, _Emission and _EmissionTint are tried too.")]
    [SerializeField] private string emissionProperty = "_EmissionColor";
    [Tooltip("HDR intensity (the picker's Intensity value) for the plain trail.")]
    [SerializeField] private float normalEmissionIntensity = 0.8f;
    [Tooltip("HDR intensity for the red trail.")]
    [SerializeField] private float perfectEmissionIntensity = 3f;

    [Tooltip("Trail width multiplier while red (1 = unchanged). A little wider makes the cue pop.")]
    [SerializeField] private float redWidthMultiplier = 1.25f;

    [Header("Timing")]
    [Tooltip("(Only used when the player's 'Perfect By Distance' is OFF - otherwise the trail turns red when his limb is within " +
             "the player's Perfect Block Distance.) Turns red this many seconds BEFORE the perfect-block window opens (reaction cushion). " +
             "The window itself is the player's Perfect Block Window (read automatically) and ends when the hit lands.")]
    [SerializeField] private float redLeadTime = 0.12f;
    [Tooltip("(Perfect By Distance ON) Turns red when his limb is this much FARTHER than the Perfect Block Distance, " +
             "so the cue shows a little earlier. 0 = exactly when a block would be perfect.")]
    [SerializeField] private float redLeadDistance = 0.3f;
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
    private Gradient normalGradient, perfectGradient;

    private static Color Hdr(Color c, float intensity)
    {
        Color l = c.linear * Mathf.Pow(2f, intensity);
        l.a = 1f;
        return l;
    }

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

        normalGradient = MakeGradient(normalColor, normalAlpha, normalColor);
        perfectGradient = MakeGradient(perfectColor, perfectAlpha, perfectColor);

        var list = new System.Collections.Generic.List<TrailRenderer>();
        if (handTrails != null) list.AddRange(handTrails);
        if (footTrails != null) list.AddRange(footTrails);
        all = list.ToArray();
        baseWidths = new float[all.Length];
        for (int i = 0; i < all.Length; i++) if (all[i] != null) baseWidths[i] = all[i].widthMultiplier;

        if (trailMaterial != null)
            foreach (var t in all)
                if (t != null) t.sharedMaterial = new Material(trailMaterial);   // one copy per trail

        SetEmitting(handTrails, false);
        SetEmitting(footTrails, false);
    }

    private static readonly string[] emissionFallbacks = { "_EmissiveColor", "_EmissionColor", "_Emission", "_EmissionTint" };
    private bool warnedNoEmission;

    private string FindEmissionProperty(Material m)
    {
        if (m.HasProperty(emissionProperty)) return emissionProperty;
        foreach (string n in emissionFallbacks) if (m.HasProperty(n)) return n;
        if (!warnedNoEmission)
        {
            warnedNoEmission = true;
            Debug.LogWarning($"[AttackTrails] The trail material '{m.name}' (shader {m.shader.name}) has no emission colour property - set 'Emission Property' to its exact name.");
        }
        return null;
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
                    if (gap >= 0f && gap <= player.PerfectBlockDistance + Mathf.Max(0f, redLeadDistance)) red = upcoming;
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
        float speed = Mathf.Max(0.01f, st.speedMultiplier);
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
        Color emission = red ? Hdr(perfectColor, perfectEmissionIntensity) : Hdr(normalColor, normalEmissionIntensity);
        foreach (var t in group)
        {
            if (t == null) continue;
            t.colorGradient = g;
            if (useEmission)
            {
                Material m = t.material;   // an instance per trail, so the shared material isn't touched
                string prop = m != null ? FindEmissionProperty(m) : null;
                if (prop != null)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor(prop, emission);
                }
            }
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