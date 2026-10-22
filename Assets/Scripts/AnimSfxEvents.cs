using UnityEngine;

/// <summary>
/// Plays sounds from Animation Events, through the AudioManager's sound library. Put it on the SAME GameObject as the
/// Animator (for the tutorial injury cutscene: the cutscene root), then in the animation clip add an event at the exact frame:
///   Function: PlaySound
///   String:   the SoundLibrary id
///   Float:    volume (leave 0 for full volume)
/// Add as many events as you like on the same clip, each at its own frame.
/// </summary>
public class AnimSfxEvents : MonoBehaviour
{
    public void PlaySound(AnimationEvent e)
    {
        if (e == null || string.IsNullOrEmpty(e.stringParameter)) return;
        float volume = Mathf.Approximately(e.floatParameter, 0f) ? 1f : e.floatParameter;
        Sfx.Play(e.stringParameter, volume);
    }
}
