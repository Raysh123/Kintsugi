using UnityEngine;

/// <summary>
/// One place for animation blending. Animator.PlayBlend(name) works like Animator.Play(name) but
/// cross-fades from whatever is playing instead of snapping. Calling it every frame with the same
/// state is safe (it does nothing if that state is already playing or already being faded to).
/// Change DefaultBlend to make every blended change faster or slower.
/// </summary>
public static class AnimBlend
{
    public static float DefaultBlend = 0.1f;

    /// <summary>State names that always snap instead of blending (fill from the Inspector of a state machine if one clip misbehaves).</summary>
    public static readonly System.Collections.Generic.HashSet<string> NoBlend = new System.Collections.Generic.HashSet<string>();

    public static void PlayBlend(this Animator a, string stateName, float blend = -1f)
    {
        if (a == null || string.IsNullOrEmpty(stateName)) return;
        if (NoBlend.Contains(stateName)) blend = 0f;
        PlayBlend(a, Animator.StringToHash(stateName), blend);
    }

    public static void PlayBlend(this Animator a, int hash, float blend = -1f)
    {
        if (a == null || !a.HasState(0, hash)) return;
        if (blend < 0f) blend = DefaultBlend;

        if (a.IsInTransition(0))
        {
            if (a.GetNextAnimatorStateInfo(0).shortNameHash == hash) return;
        }
        else if (a.GetCurrentAnimatorStateInfo(0).shortNameHash == hash)
        {
            return;
        }

        if (blend <= 0f) a.Play(hash, 0, 0f);
        else a.CrossFadeInFixedTime(hash, blend, 0, 0f);
    }
}