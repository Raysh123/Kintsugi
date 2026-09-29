using UnityEngine;

/// <summary>
/// Handles the home location. Right now this does exactly one thing: revert the player's
/// appearance to "Normal" on arrival, same as ParkActivity - home and the park are both
/// plain-clothes locations, so whatever outfit the player picked up elsewhere (e.g. the
/// dojo's) reverts here too. Nothing else lives on this yet; expand it the same way
/// DojoArrivalHandler/ParkActivity grew (dialogue, unlock lists, etc.) whenever home needs
/// its own behavior.
/// </summary>
public class HomeArrivalHandler : MonoBehaviour, ITravelArrivalHandler
{
    [SerializeField] private PlayerAppearanceController appearance;
    [SerializeField] private string normalModelId = "Normal";

    public void OnArrival()
    {
        if (appearance != null) appearance.SetModel(normalModelId);
    }
}