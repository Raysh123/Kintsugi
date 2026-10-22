using UnityEngine;

/// <summary>For prompt objects shared by several interactions: a script only hides the prompt if it was the one that showed it.</summary>
public static class SharedPrompt
{
    public static void Set(ref bool mine, GameObject prompt, bool on)
    {
        if (prompt == null) return;
        if (on)
        {
            mine = true;
            if (!prompt.activeSelf) prompt.SetActive(true);
        }
        else if (mine)
        {
            mine = false;
            if (prompt.activeSelf) prompt.SetActive(false);
        }
    }
}
