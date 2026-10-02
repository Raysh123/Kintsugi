using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds every visible body/outfit the player can have, and switches between them. Each
/// PlayerModel is a self-contained sibling child under the player - its own mesh/rig plus its
/// own Animator (idle/walk, same parameters as every other model) - exactly the same pattern
/// the player already uses for its one current model, just with multiple slots instead of one
/// hardcoded child.
///
/// Whichever arrival handler owns a location just calls SetModel(id) on arrival:
/// DojoArrivalHandler calls SetModel("Dojo"); ParkActivity and (whenever it's built) a Home
/// arrival handler both call SetModel("Normal"). Adding a new outfit for some future area is
/// just: add a new entry to Models here with its own id, and have that area's arrival handler
/// call SetModel with that id - nothing else in this script needs to change.
/// </summary>
public class PlayerAppearanceController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerModel
    {
        [Tooltip("Referenced by arrival handlers via SetModel(id) - keep these unique " +
                 "(e.g. \"Normal\", \"Dojo\").")]
        public string id;
        [Tooltip("This model's own GameObject (mesh/rig) - a sibling child under the player, " +
                 "toggled active/inactive as a whole.")]
        public GameObject modelObject;
        [Tooltip("Animator on this model. Needs the same parameters (MoveSpeed) as every " +
                 "other model, since PlayerMovement drives whichever one is currently active.")]
        public Animator animator;
    }

    [SerializeField] private PlayerMovement player;
    [SerializeField] private List<PlayerModel> models = new List<PlayerModel>();
    [Tooltip("Which model id is active at game start - normally \"Normal\".")]
    [SerializeField] private string defaultModelId = "Normal";

    public string CurrentModelId { get; private set; }

    /// <summary>
    /// Awake, NOT Start - Unity guarantees every object's Awake() runs before any object's
    /// Start() in the same scene load, regardless of script execution order settings. This
    /// matters because other scripts (PostFightHandler, DojoArrivalHandler) call SetModel
    /// from their own Start()-triggered logic on the SAME scene load (e.g. returning from a
    /// fight reloads the whole RPG scene) - if this also ran in Start(), it would be a race:
    /// whichever Start() happened to run second would silently win and undo the other.
    /// </summary>
    private void Awake()
    {
        SetModel(defaultModelId);
    }

    /// <summary>Activates the model matching id and deactivates every other one, re-pointing
    /// PlayerMovement's Animator at the new model's. Safe to call repeatedly with the same id
    /// (e.g. every OnArrival(), whether it's a first visit or a return one) - it's a no-op
    /// beyond re-confirming the same state.</summary>
    public void SetModel(string id)
    {
        Debug.Log($"[PlayerAppearanceController] SetModel(\"{id}\") called on '{gameObject.name}'.");

        PlayerModel match = models.Find(m => m.id == id);
        if (match == null)
        {
            Debug.LogWarning($"[PlayerAppearanceController] No model registered with id \"{id}\" - ignoring. " +
                              $"Registered ids: [{string.Join(", ", models.ConvertAll(m => m.id))}]");
            return;
        }

        if (match.modelObject == null)
        {
            Debug.LogWarning($"[PlayerAppearanceController] Model entry \"{id}\" has no Model Object assigned - nothing to activate.");
        }

        foreach (PlayerModel model in models)
        {
            if (model.modelObject == null) continue;
            bool shouldBeActive = model == match;
            model.modelObject.SetActive(shouldBeActive);
            Debug.Log($"[PlayerAppearanceController]   '{model.modelObject.name}' (id \"{model.id}\") -> active={shouldBeActive}");
        }

        if (player != null)
        {
            player.SetAnimator(match.animator);
        }
        else
        {
            Debug.LogWarning("[PlayerAppearanceController] Player (PlayerMovement) is not assigned - movement animation won't follow this model switch.");
        }

        CurrentModelId = id;
    }
}