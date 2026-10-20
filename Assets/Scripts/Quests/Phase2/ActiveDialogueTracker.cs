using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static gate tracking "is any dialogue currently on screen". PlayerMovement locks while AnyActive.
/// Each running dialogue registers its owner; an owner that is destroyed or switched off no longer
/// counts, so a dialogue that dies mid-way (scene change, object disabled) can never leave the
/// player locked. Begin()/End() without an owner still work as a plain counter.
/// </summary>
public static class ActiveDialogueTracker
{
    private static int anonymousCount;
    private static readonly List<Object> owners = new List<Object>();

    public static bool AnyActive
    {
        get
        {
            for (int i = owners.Count - 1; i >= 0; i--)
            {
                Object o = owners[i];
                Behaviour b = o as Behaviour;
                if (o == null || (b != null && !b.isActiveAndEnabled)) owners.RemoveAt(i);
            }
            return anonymousCount > 0 || owners.Count > 0;
        }
    }

    public static void Begin(Object owner = null)
    {
        if (owner != null) owners.Add(owner);
        else anonymousCount++;
    }

    public static void End(Object owner = null)
    {
        if (owner != null) owners.Remove(owner);
        else if (anonymousCount > 0) anonymousCount--;
    }

    public static void Reset()
    {
        anonymousCount = 0;
        owners.Clear();
    }
}