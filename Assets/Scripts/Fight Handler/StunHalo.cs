using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "dizzy stars" orbiting a fighter's head while they are stunned. 3D objects parented to a
/// head anchor (so they follow the head, get hidden behind it, and sit properly in the scene).
///
/// Setup (one per fighter):
///   1. Make an empty child of the head BONE, positioned just above the top of the head, call it
///      "StunAnchor".
///   2. Put this component on it and set Owner = Player or Enemy.
///   3. Optional: assign a star / bird prefab (small model; its pivot should be its centre).
///      Leave empty to get simple yellow spheres until the model is ready.
///
/// The stun code calls the static API below, so nothing else needs references to this:
///   StunHalo.Begin(isPlayer, stunSeconds)  - stars appear and orbit for the whole stun, then
///                                            vanish one by one over the last part of it
///   StunHalo.Shatter(isPlayer)             - stars flash red and break apart (dodged out of stun)
///   StunHalo.Stop(isPlayer)                - remove them now (no effect while shattering)
/// </summary>
public class StunHalo : MonoBehaviour
{
    public enum Owner { Player, Enemy }

    [SerializeField] private Owner owner = Owner.Enemy;

    [Header("Stars")]
    [Tooltip("Optional star / bird model. Empty = simple spheres.")]
    [SerializeField] private GameObject starPrefab;
    [SerializeField] private int count = 3;
    [SerializeField] private float radius = 0.35f;
    [Tooltip("Degrees per second around the head.")]
    [SerializeField] private float orbitSpeed = 220f;
    [SerializeField] private float bobHeight = 0.04f;
    [SerializeField] private float bobSpeed = 6f;
    [Tooltip("Size of each default sphere (world units). Not used for a prefab - see Prefab Scale.")]
    [SerializeField] private float starScale = 0.15f;
    [Tooltip("Only for a Star Prefab: multiplies the prefab's own scale. 1 = as authored; raise it " +
             "if your model looks too small, lower it if too big.")]
    [SerializeField] private float prefabScale = 1f;
    [Tooltip("Spin of each star on its own axis, degrees per second.")]
    [SerializeField] private float spinSpeed = 180f;
    [Tooltip("Colour of the default spheres (a prefab keeps its own look).")]
    [SerializeField] private Color starColor = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private float popInTime = 0.15f;

    [Header("Ending a stun")]
    [Range(0.1f, 1f)]
    [Tooltip("The stars vanish one by one during the LAST part of the stun - this fraction of it.")]
    [SerializeField] private float vanishWindow = 0.4f;
    [Tooltip("How long each star takes to shrink away.")]
    [SerializeField] private float vanishDuration = 0.25f;

    [Header("Shatter (dodging out of a stun)")]
    [SerializeField] private Color shatterColor = new Color(1f, 0.15f, 0.1f);
    [Tooltip("Seconds the stars hold red before breaking.")]
    [SerializeField] private float redHoldTime = 0.15f;
    [SerializeField] private int shardsPerStar = 6;
    [SerializeField] private float shardSize = 0.04f;
    [SerializeField] private float shardSpeed = 2.5f;
    [SerializeField] private float shardLife = 0.6f;
    [Tooltip("Optional SoundLibrary id played when they shatter.")]
    [SerializeField] private string shatterSfxId;

    private static StunHalo playerHalo, enemyHalo;

    private readonly List<Transform> stars = new List<Transform>();
    private readonly List<Vector3> baseScales = new List<Vector3>();
    private Coroutine runRoutine;
    private bool shattering;

    // ---------------------------------------------------------------- static API
    public static void Begin(bool isPlayer, float duration)
    {
        StunHalo h = isPlayer ? playerHalo : enemyHalo;
        if (h != null) h.BeginInternal(duration);
        else Debug.LogWarning($"[StunHalo] Stun began for the {(isPlayer ? "Player" : "Enemy")} but no StunHalo with " +
                              $"Owner = {(isPlayer ? "Player" : "Enemy")} is active in the scene - add one on that fighter's head.");
    }

    public static void Shatter(bool isPlayer)
    {
        StunHalo h = isPlayer ? playerHalo : enemyHalo;
        if (h != null) h.ShatterInternal();
    }

    public static void Stop(bool isPlayer)
    {
        StunHalo h = isPlayer ? playerHalo : enemyHalo;
        if (h != null) h.StopInternal();
    }

    // ---------------------------------------------------------------- lifecycle
    private void Awake()
    {
        if (owner == Owner.Player) playerHalo = this; else enemyHalo = this;
    }

    private void OnDestroy()
    {
        if (playerHalo == this) playerHalo = null;
        if (enemyHalo == this) enemyHalo = null;
        ClearStars();
    }

    private void BeginInternal(float duration)
    {
        if (shattering) return;                 // a shatter is still playing out
        ClearStars();
        if (runRoutine != null) StopCoroutine(runRoutine);
        Debug.Log($"[StunHalo] {owner}: stars start ({duration:F1}s) at {transform.position:F2}, " +
                  $"anchor active={gameObject.activeInHierarchy}.");
        runRoutine = StartCoroutine(Run(Mathf.Max(0.3f, duration)));
    }

    private void StopInternal()
    {
        if (shattering) return;
        if (runRoutine != null) { StopCoroutine(runRoutine); runRoutine = null; }
        ClearStars();
    }

    private void ClearStars()
    {
        foreach (Transform s in stars) if (s != null) Destroy(s.gameObject);
        stars.Clear();
        baseScales.Clear();
    }

    private Transform MakeStar()
    {
        GameObject go;
        if (starPrefab != null)
        {
            go = Instantiate(starPrefab);   // NOT parented: bones often carry odd scales (FBX 0.01/100)
            go.SetActive(true);             // a scene object assigned here may be switched off
            baseScales.Add(starPrefab.transform.localScale * prefabScale);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = starColor;
            baseScales.Add(Vector3.one * starScale);
        }
        go.transform.localScale = Vector3.zero;
        return go.transform;
    }

    private static void Tint(Transform t, Color c)
    {
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            r.material.color = c;
    }

    // ---------------------------------------------------------------- the orbit
    private IEnumerator Run(float duration)
    {
        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++) stars.Add(MakeStar());

        float span = duration * vanishWindow;
        float vanishStart = duration - span;
        float step = span / n;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                Transform s = stars[i];
                if (s == null) continue;

                float angle = (orbitSpeed * t + i * 360f / n) * Mathf.Deg2Rad;
                float bob = Mathf.Sin(t * bobSpeed + i * 2f) * bobHeight;
                // World space, around the anchor's position - independent of the bone's scale.
                s.position = transform.position + new Vector3(Mathf.Cos(angle) * radius, bob, Mathf.Sin(angle) * radius);
                s.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

                float scale = 1f;   // 0-1 multiplier on this star's own base size
                if (popInTime > 0f) scale *= Mathf.Clamp01(t / popInTime);

                // Vanish one by one: the last star made goes first.
                float myVanish = vanishStart + (n - 1 - i) * step;
                if (t > myVanish)
                    scale *= 1f - Mathf.Clamp01((t - myVanish) / Mathf.Max(0.01f, vanishDuration));

                s.localScale = baseScales[i] * scale;
            }
            yield return null;
        }

        runRoutine = null;
        ClearStars();
    }

    // ---------------------------------------------------------------- shatter
    private void ShatterInternal()
    {
        if (shattering || stars.Count == 0) return;
        if (runRoutine != null) { StopCoroutine(runRoutine); runRoutine = null; }
        StartCoroutine(ShatterRoutine());
    }

    private IEnumerator ShatterRoutine()
    {
        shattering = true;
        foreach (Transform s in stars) if (s != null) Tint(s, shatterColor);

        if (redHoldTime > 0f) yield return new WaitForSeconds(redHoldTime);

        if (!string.IsNullOrEmpty(shatterSfxId)) Sfx.Play(shatterSfxId);

        var shards = new List<Transform>();
        var velocities = new List<Vector3>();
        foreach (Transform s in stars)
        {
            if (s == null) continue;
            for (int k = 0; k < shardsPerStar; k++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Collider c = shard.GetComponent<Collider>();
                if (c != null) Destroy(c);
                shard.transform.position = s.position;
                shard.transform.rotation = Random.rotation;
                shard.transform.localScale = Vector3.one * shardSize;
                shard.GetComponent<Renderer>().material.color = shatterColor;
                shards.Add(shard.transform);
                velocities.Add(Random.onUnitSphere * shardSpeed * Random.Range(0.6f, 1.2f));
            }
        }
        ClearStars();

        float t = 0f;
        while (t < shardLife)
        {
            t += Time.deltaTime;
            float k = 1f - t / shardLife;
            for (int i = 0; i < shards.Count; i++)
            {
                if (shards[i] == null) continue;
                velocities[i] += Vector3.down * 6f * Time.deltaTime;
                shards[i].position += velocities[i] * Time.deltaTime;
                shards[i].Rotate(360f * Time.deltaTime, 270f * Time.deltaTime, 0f);
                shards[i].localScale = Vector3.one * shardSize * Mathf.Max(0f, k);
            }
            yield return null;
        }
        foreach (Transform sh in shards) if (sh != null) Destroy(sh.gameObject);
        shattering = false;
    }
}