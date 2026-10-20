using UnityEngine;

/// <summary>
/// Exaggerates animation poses in code. Put it on a character (player or enemy). Every frame, AFTER the
/// Animator has posed the model, each bone's rotation and position is pushed further away from the character's
/// IDLE pose: result = idle + (animated - idle) * amount. 1 = unchanged, 1.3 = 30% bigger movements.
/// The idle pose itself is re-read whenever the Animator is sitting in its Idle state, so standing still
/// is never distorted - only the attacks, blocks and hurts get bigger.
/// </summary>
[DefaultExecutionOrder(500)]
public class PoseExaggerator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Top of the skeleton to affect (the object holding the bones / controllers). Empty = this object.")]
    [SerializeField] private Transform root;
    [Tooltip("The Animator state that counts as the neutral pose.")]
    [SerializeField] private string idleStateName = "Idle";

    [Header("Amounts (1 = no change)")]
    [Range(0.5f, 2.5f)] [SerializeField] private float rotationAmount = 1.25f;
    [Range(0.5f, 2.5f)] [SerializeField] private float positionAmount = 1.25f;
    [Tooltip("Fade the effect in/out when this is changed at runtime (per second). 0 = instant.")]
    [SerializeField] private float amountSmoothing = 0f;

    [Header("Filters")]
    [Tooltip("Transforms whose name contains any of these are left alone (the whole-body 'Armature' turn is excluded by default, " +
             "so spins are not exaggerated).")]
    [SerializeField] private string[] ignoreNameContains = { "Armature" };

    /// <summary>Scales both amounts at runtime (1 = as set above, 0 = off).</summary>
    public float Intensity { get; set; } = 1f;

    private Transform[] bones;
    private Quaternion[] refRot, lastRot;
    private Vector3[] refPos, lastPos;
    private bool[] wrote;
    private bool haveIdle;
    private int idleHash;
    private float shown = 1f;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (root == null) root = transform;
        idleHash = Animator.StringToHash(idleStateName);

        var all = root.GetComponentsInChildren<Transform>(true);
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var t in all)
        {
            if (t == root) continue;
            bool skip = false;
            foreach (string n in ignoreNameContains)
                if (!string.IsNullOrEmpty(n) && t.name.Contains(n)) { skip = true; break; }
            if (!skip) list.Add(t);
        }
        bones = list.ToArray();
        int c = bones.Length;
        refRot = new Quaternion[c]; lastRot = new Quaternion[c];
        refPos = new Vector3[c]; lastPos = new Vector3[c];
        wrote = new bool[c];
    }

    private void LateUpdate()
    {
        if (animator == null || bones == null) return;

        float target = Mathf.Max(0f, Intensity);
        shown = amountSmoothing > 0f ? Mathf.MoveTowards(shown, target, amountSmoothing * Time.unscaledDeltaTime) : target;

        bool inIdle = !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == idleHash;

        if (inIdle)
        {
            // Neutral pose: remember it, leave everything exactly as animated.
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                refRot[i] = bones[i].localRotation;
                refPos[i] = bones[i].localPosition;
                wrote[i] = false;
            }
            haveIdle = true;
            return;
        }

        if (!haveIdle || shown <= 0.0001f) return;

        float rAmt = Mathf.LerpUnclamped(1f, rotationAmount, shown);
        float pAmt = Mathf.LerpUnclamped(1f, positionAmount, shown);

        for (int i = 0; i < bones.Length; i++)
        {
            Transform b = bones[i];
            if (b == null) continue;

            Quaternion curRot = b.localRotation;
            Vector3 curPos = b.localPosition;

            // If the Animator did not write this bone this frame, what we see is our own last output - don't compound it.
            if (wrote[i] && curRot == lastRot[i] && curPos == lastPos[i]) continue;

            b.localRotation = Quaternion.SlerpUnclamped(refRot[i], curRot, rAmt);
            b.localPosition = Vector3.LerpUnclamped(refPos[i], curPos, pAmt);

            lastRot[i] = b.localRotation;
            lastPos[i] = b.localPosition;
            wrote[i] = true;
        }
    }
}
