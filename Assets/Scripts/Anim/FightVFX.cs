using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a VFX prefab for each fight event (hit, block, perfect block, stun, leg hit). Put ONE in the fight scene,
/// assign a prefab to each event you want (leave empty = no effect for that event). Effects spawn at the contact
/// point and destroy themselves after their lifetime.
/// </summary>
public class FightVFX : MonoBehaviour
{
    public enum Kind { EnemyHit, PlayerHit, EnemyBlock, PlayerBlock, PerfectBlock, Stun, LegHit, PlayerPunch, PlayerKick, SwapReady, Swap, SwapPush }

    [System.Serializable]
    public class Entry
    {
        public Kind kind;
        [Tooltip("The effect prefab (particle system, VFX Graph, anything). Empty = no effect.")]
        public GameObject prefab;
        [Tooltip("Shift from the contact point (world units).")]
        public Vector3 offset;
        [Tooltip("Rotation of the effect in degrees (X, Y, Z), world space.")]
        public Vector3 rotation;
        public float scale = 1f;
        [Tooltip("Seconds before the spawned effect is destroyed.")]
        public float lifetime = 2f;
        [Tooltip("ON = the effect winds down after its lifetime. OFF = it is removed instantly when the lifetime ends.")]
        public bool dissipate = true;
        [Tooltip("After the lifetime the effect winds down for this long (particles stop emitting and fade out naturally, anything else shrinks away) before it is destroyed.")]
        public float dissipateSeconds = 0.5f;
        [Tooltip("Parent it to the fighter it is about (e.g. stun stars that follow him).")]
        public bool followTarget;
    }

    [SerializeField]
    private List<Entry> effects = new List<Entry>
    {
        new Entry { kind = Kind.EnemyHit },
        new Entry { kind = Kind.PlayerHit },
        new Entry { kind = Kind.EnemyBlock },
        new Entry { kind = Kind.PlayerBlock },
        new Entry { kind = Kind.PerfectBlock },
        new Entry { kind = Kind.Stun, followTarget = true, lifetime = 3f },
        new Entry { kind = Kind.LegHit },
        new Entry { kind = Kind.PlayerPunch },
        new Entry { kind = Kind.PlayerKick },
        new Entry { kind = Kind.SwapReady },
        new Entry { kind = Kind.Swap },
        new Entry { kind = Kind.SwapPush },
    };

    // Makes sure every event has a slot in the Inspector (newer events are added to lists saved earlier).
    private void OnValidate()
    {
        if (effects == null) effects = new List<Entry>();
        foreach (Kind k in System.Enum.GetValues(typeof(Kind)))
        {
            bool found = false;
            foreach (Entry e in effects) if (e != null && e.kind == k) { found = true; break; }
            if (!found) effects.Add(new Entry { kind = k });
        }
    }

    private static FightVFX instance;

    private void Awake() { instance = this; }
    private void OnDestroy() { if (instance == this) instance = null; }

    /// <summary>Spawns the effect set for 'kind' at 'position'. 'follow' is used by effects marked Follow Target.</summary>
    public static void Play(Kind kind, Vector3 position, Transform follow = null)
    {
        if (instance == null) return;
        foreach (Entry e in instance.effects)
        {
            if (e == null || e.kind != kind || e.prefab == null) continue;
            GameObject go = Object.Instantiate(e.prefab, position + e.offset, Quaternion.Euler(e.rotation));
            go.transform.localScale = go.transform.localScale * e.scale;
            if (e.followTarget && follow != null) go.transform.SetParent(follow, true);
            if (e.dissipate) go.AddComponent<VfxDissipate>().Begin(Mathf.Max(0.1f, e.lifetime), Mathf.Max(0f, e.dissipateSeconds));
            else Object.Destroy(go, Mathf.Max(0.1f, e.lifetime));
        }
    }
}

/// <summary>Runs on a spawned effect: after its lifetime it winds down instead of vanishing.</summary>
public class VfxDissipate : MonoBehaviour
{
    private float life, fade;

    public void Begin(float lifetime, float dissipateSeconds)
    {
        life = lifetime;
        fade = dissipateSeconds;
        StartCoroutine(Run());
    }

    private System.Collections.IEnumerator Run()
    {
        yield return new WaitForSeconds(life);

        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Vector3 startScale = transform.localScale;
        Light[] lights = GetComponentsInChildren<Light>(true);
        float[] lightStart = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) lightStart[i] = lights[i].intensity;

        bool hasParticles = systems.Length > 0;
        float t = 0f;
        while (t < fade || (hasParticles && AnyAlive(systems) && t < fade + 5f))
        {
            t += Time.deltaTime;
            float k = fade > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fade)) : 1f;
            if (!hasParticles) transform.localScale = startScale * (1f - k);
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = lightStart[i] * (1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }

    private static bool AnyAlive(ParticleSystem[] systems)
    {
        foreach (ParticleSystem ps in systems) if (ps != null && ps.IsAlive(false)) return true;
        return false;
    }
}