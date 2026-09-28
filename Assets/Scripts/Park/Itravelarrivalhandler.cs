/// <summary>
/// Implement this on anything that should react when the player arrives at its
/// location via the travel map (e.g. ParkActivity starting its run sequence).
/// Assign the implementing component to a TravelLocation's Arrival Handler field
/// in TravelMapController.
/// </summary>
public interface ITravelArrivalHandler
{
    void OnArrival();
}