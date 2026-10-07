using System.Collections;
using UnityEngine;

/// <summary>
/// Visual "an attack is coming" indicator - big and blue while the enemy's attack is winding
/// up, continuously shrinking toward a smaller size, landing on red right as the attack's
/// hitbox actually goes live (hitbox.canDealDamage == true). That live window is also
/// precisely when PlayerStateMachine.TryPerfectBlock() can succeed - so red isn't a separate
/// guessed timer, it's driven by the same existing signal.
///
/// The icon's SpriteRenderer is always enabled - "off" is just Idle Color (transparent by
/// default), not a separate enabled/disabled state. This is deliberate: every earlier bug
/// here (stuck on screen after a combo ended, after a round reset, after an interrupt, etc.)
/// came from some code path forgetting to call a "turn it off" method. With only one
/// always-on visual and three colors (idle/safe/perfect), there's no separate on/off
/// bookkeeping to miss - EnemyStateMachine just tells it which color applies right now, the
/// same way it already does for safe vs perfect.
///
/// The shrink happens over a caller-supplied duration (EnemyStateMachine passes the attack
/// clip's own length) rather than a fixed speed - so the RATE of shrinking is itself the
/// urgency tell: a short, fast attack shrinks quickly, a long windup shrinks slowly, even
/// though both start at Start Size Multiplier and both land on End Size Multiplier.
///
/// Put this on a sprite/icon wherever it should appear (above the enemy's head is the usual
/// choice for a 2.5D side-view fighter). EnemyStateMachine drives it directly - no new
/// Animation Events needed, since OpenEnemyHitWindow/CloseEnemyHitWindow already exist and
/// are already correctly timed.
/// </summary>
public class AttackTelegraph : MonoBehaviour
{
    [SerializeField] private SpriteRenderer icon;

    [Header("Colors")]
    [Tooltip("Shown whenever nothing is incoming - defaults to fully transparent, so the " +
             "icon is effectively invisible without ever being disabled.")]
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0f);

    [Header("Size")]
    [Tooltip("Scale when a new shrink starts - 1 = this object's own Transform scale as " +
             "placed in the scene (that's the \"100%\" baseline everything else is relative to).")]
    [SerializeField] private float startSizeMultiplier = 1f;
    [Tooltip("Scale once the shrink finishes - e.g. 0.5 for half the starting size. Should " +
             "land right around when the attack actually becomes live (red).")]
    [SerializeField] private float endSizeMultiplier = 0.5f;

    private Vector3 baseScale;
    private Coroutine shrinkRoutine;

    private void Awake()
    {
        if (icon == null) icon = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;

        // Always on - "invisible" is handled entirely by idleColor's alpha, not by disabling
        // the renderer. Never toggled off again after this.
        if (icon != null)
        {
            icon.enabled = true;
            icon.color = idleColor;
        }
    }

    /// <summary>Sets the color to idleColor, stops any shrink in progress, and resets size
    /// back to full - the "nothing incoming right now" state. Safe to call any number of
    /// times from anywhere; there's no enabled/disabled state underneath this to get out of
    /// sync, so calling it redundantly (or missing a call somewhere) can't leave a stale
    /// color stuck on screen the way Hide()/Show() toggling used to.</summary>
    public void Idle()
    {
        if (shrinkRoutine != null)
        {
            StopCoroutine(shrinkRoutine);
            shrinkRoutine = null;
        }

        if (icon != null) icon.color = idleColor;
        transform.localScale = baseScale * startSizeMultiplier;
    }

    /// <summary>Shows the icon at Start Size Multiplier in the given color, and shrinks
    /// toward End Size Multiplier over shrinkDuration seconds. Call again (e.g. for each hit
    /// in a combo) to restart the shrink from full size for whatever's coming next.</summary>
    public void Show(Color color, float shrinkDuration)
    {
        if (icon == null) return;
        icon.color = color;

        if (shrinkRoutine != null) StopCoroutine(shrinkRoutine);
        shrinkRoutine = StartCoroutine(ShrinkRoutine(shrinkDuration));
    }

    /// <summary>Changes color only - size/shrink-in-progress is left exactly as it is. Use
    /// this for the blue-to-red switch, since by then the shrink should already be at (or
    /// very near) End Size Multiplier and shouldn't jump back up.</summary>
    public void SetColor(Color color)
    {
        if (icon != null) icon.color = color;
    }

    /// <summary>Kept as an alias for Idle() - older call sites (EnemyStateMachine) already
    /// call Hide() at every point that means "nothing incoming right now"; this way that
    /// existing wording still reads correctly even though nothing is actually being
    /// enabled/disabled anymore under the hood.</summary>
    public void Hide() => Idle();

    /// <summary>Blinks the icon between whatever color it's currently showing and
    /// flashColor for the given duration, then restores exactly the color it had before the
    /// flash started - so the caller doesn't need to know or re-apply what color comes next.
    /// Only touches color, never scale, so it runs fine alongside an in-progress shrink
    /// (ShrinkRoutine only ever sets color once, at the start of Show() - this won't fight it).
    /// Caller is expected to `yield return` this directly (it's just an IEnumerator, not
    /// something that needs its own StartCoroutine) so it runs on the CALLER's coroutine and
    /// is cancelled automatically if that coroutine is stopped.</summary>
    public IEnumerator FlashFor(Color flashColor, float duration, float flashInterval = 0.08f)
    {
        if (icon == null || duration <= 0f) yield break;

        Color restoreColor = icon.color;
        bool showingFlash = false;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            showingFlash = !showingFlash;
            icon.color = showingFlash ? flashColor : restoreColor;
            float step = Mathf.Min(flashInterval, duration - elapsed);
            yield return new WaitForSeconds(step);
            elapsed += step;
        }

        icon.color = restoreColor;
    }

    private IEnumerator ShrinkRoutine(float duration)
    {
        transform.localScale = baseScale * startSizeMultiplier;

        if (duration <= 0f)
        {
            transform.localScale = baseScale * endSizeMultiplier;
            shrinkRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float multiplier = Mathf.Lerp(startSizeMultiplier, endSizeMultiplier, elapsed / duration);
            transform.localScale = baseScale * multiplier;
            yield return null;
        }

        transform.localScale = baseScale * endSizeMultiplier;
        shrinkRoutine = null;
    }
}