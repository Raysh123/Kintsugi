using System;
using UnityEngine;

/// <summary>
/// Sets numbers on the outline material for THIS scene (e.g. different thresholds / thickness in the 2.5D fight scenes and the
/// top-down RPG), and puts the original values back when the scene unloads, so the shared material asset isn't left changed.
///
/// Setup: one of these in each scene that needs its own values.
///   Outline Material - the material your outline uses (the one that already works).
///   Values           - one row per number to change. Type the property name EXACTLY as the material's Inspector shows it
///                      (e.g. "Color Threshold") or its shader reference name (e.g. "_Color_Threshold"); either works.
/// Only single-number (float) properties are handled; a name that can't be found is skipped with one warning.
/// </summary>
public class SceneOutlineThickness : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("The property's name as shown in the material Inspector (e.g. Color Threshold), or its shader reference name.")]
        public string propertyName = "Thickness";
        public float value = 1f;
        [HideInInspector] public string resolved;
        [HideInInspector] public float original;
    }

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Entry[] values =
    {
        new Entry { propertyName = "Thickness", value = 0.7f },
        new Entry { propertyName = "Color Threshold", value = 2.85f },
        new Entry { propertyName = "Normal Threshold", value = 1.78f },
    };
    [Tooltip("Put the material's original values back when this scene is unloaded.")]
    [SerializeField] private bool restoreOnDestroy = true;

    private static string Normalise(string s) => (s ?? "").Replace(" ", "").Replace("_", "").ToLowerInvariant();

    /// <summary>Finds the real shader property: by reference name first, then by the label shown in the Inspector.</summary>
    private static string Resolve(Material m, string wanted)
    {
        if (m.HasProperty(wanted)) return wanted;
        Shader sh = m.shader;
        if (sh == null) return null;
        string target = Normalise(wanted);
        for (int i = 0; i < sh.GetPropertyCount(); i++)
        {
            if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Float &&
                sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Range) continue;
            if (Normalise(sh.GetPropertyDescription(i)) == target || Normalise(sh.GetPropertyName(i)) == target)
                return sh.GetPropertyName(i);
        }
        return null;
    }

    private void Awake()
    {
        if (outlineMaterial == null || values == null) return;
        foreach (Entry e in values)
        {
            if (e == null || string.IsNullOrEmpty(e.propertyName)) continue;
            e.resolved = Resolve(outlineMaterial, e.propertyName);
            if (e.resolved == null)
            {
                Debug.LogWarning("[SceneOutlineThickness] Material '" + outlineMaterial.name + "' has no number property '" + e.propertyName + "'.", this);
                continue;
            }
            e.original = outlineMaterial.GetFloat(e.resolved);
            outlineMaterial.SetFloat(e.resolved, e.value);
        }
    }

    private void OnDestroy()
    {
        if (!restoreOnDestroy || outlineMaterial == null || values == null) return;
        foreach (Entry e in values)
            if (e != null && e.resolved != null) outlineMaterial.SetFloat(e.resolved, e.original);
    }
}
