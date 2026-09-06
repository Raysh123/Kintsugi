using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A visual map UI for fast-travel between unlocked locations. Each location's
/// pin IS a UI Button — cycling calls Select() on the current one, which
/// triggers Unity's own built-in Selected color/sprite transition, so no
/// separate cursor or custom highlight code is needed. The player cycles
/// between UNLOCKED buttons only (locked ones are deactivated, not just
/// dimmed) using WASD/left stick, and confirms with E/A to teleport there.
///
/// Cycling uses Input.GetButtonDown("Horizontal"/"Vertical") rather than raw
/// GetAxisRaw — Unity's Input Manager already debounces analog stick input
/// into a discrete "just pressed" edge when queried this way, same as a real
/// button, so no manual threshold-crossing tracking is needed here.
///
/// Requires an EventSystem in the scene, same as any other Unity UI Button.
/// </summary>
public class TravelMapController : MonoBehaviour
{
    [System.Serializable]
    public class TravelLocation
    {
        public string locationName;
        [Tooltip("The pin itself, as a UI Button — also used for highlighting the current selection " +
                 "via its own built-in Selected color/sprite state, no separate cursor needed.")]
        public Button icon;
        [Tooltip("The player teleports to this Transform's position/rotation when selected.")]
        public Transform destination;
        [Tooltip("This location becomes selectable once the story reaches this phase (or later).")]
        public StoryPhase unlockPhase = StoryPhase.BedRest;
    }

    [Header("Locations")]
    [SerializeField] private List<TravelLocation> locations = new List<TravelLocation>();

    [Header("UI")]
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private TMP_Text locationNameText;

    [Header("Player")]
    [SerializeField] private PlayerMovement player;

    private int currentIndex = -1;
    private bool isOpen;

    private void Start()
    {
        if (mapPanel != null) mapPanel.SetActive(false);
        RefreshIconVisibility();
    }

    public void OpenMap()
    {
        if (isOpen) return;

        RefreshIconVisibility();
        currentIndex = FindNextUnlockedIndex(0, 1);

        if (currentIndex == -1)
        {
            Debug.Log("[Travel] No locations unlocked yet — nowhere to go.");
            return;
        }

        isOpen = true;
        UpdateSelectionVisual();

        if (mapPanel != null) mapPanel.SetActive(true);
        if (player != null) player.movementLocked = true;
    }

    public void CloseMap()
    {
        if (!isOpen) return;

        isOpen = false;
        if (mapPanel != null) mapPanel.SetActive(false);
        if (player != null) player.movementLocked = false;
    }

    private void Update()
    {
        if (!isOpen) return;

        HandleCycling();

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            SelectCurrentLocation();
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetButtonDown("Cancel"))
            CloseMap();
    }

    private void HandleCycling()
    {
        // GetButtonDown gives a genuine "just pressed" edge even for a continuous
        // stick axis — Unity's Input Manager handles the threshold-crossing
        // debounce internally, so this behaves like a single discrete press per
        // flick without any manual tracking needed here.
        if (Input.GetButtonDown("Horizontal"))
        {
            float h = Input.GetAxisRaw("Horizontal");
            Cycle(h > 0f ? 1 : -1);
        }
        else if (Input.GetButtonDown("Vertical"))
        {
            float v = Input.GetAxisRaw("Vertical");
            Cycle(v > 0f ? -1 : 1);
        }
    }

    private void Cycle(int direction)
    {
        int next = FindNextUnlockedIndex(currentIndex + direction, direction);
        if (next == -1) return;
        currentIndex = next;
        UpdateSelectionVisual();
    }

    private int FindNextUnlockedIndex(int startIndex, int direction)
    {
        if (locations.Count == 0) return -1;

        int index = ((startIndex % locations.Count) + locations.Count) % locations.Count;
        for (int i = 0; i < locations.Count; i++)
        {
            if (IsUnlocked(locations[index])) return index;
            index = ((index + direction) % locations.Count + locations.Count) % locations.Count;
        }
        return -1;
    }

    private bool IsUnlocked(TravelLocation location)
    {
        return StoryManager.instance != null && StoryManager.instance.CurrentPhase >= location.unlockPhase;
    }

    private void RefreshIconVisibility()
    {
        foreach (TravelLocation location in locations)
        {
            if (location.icon == null) continue;

            bool unlocked = IsUnlocked(location);
            location.icon.gameObject.SetActive(unlocked);
            location.icon.interactable = unlocked;
        }
    }

    private void UpdateSelectionVisual()
    {
        if (currentIndex < 0 || currentIndex >= locations.Count) return;
        TravelLocation location = locations[currentIndex];

        if (location.icon != null)
            location.icon.Select();

        if (locationNameText != null)
            locationNameText.text = location.locationName;
    }

    private void SelectCurrentLocation()
    {
        if (currentIndex < 0 || currentIndex >= locations.Count) return;
        TravelLocation location = locations[currentIndex];

        if (!IsUnlocked(location) || location.destination == null) return;

        TeleportPlayer(location.destination);
        CloseMap();
    }

    private void TeleportPlayer(Transform destination)
    {
        if (player == null) return;

        // Same disable-move-re-enable pattern used elsewhere (GameManager.ResetPlayerPos) —
        // CharacterController fights direct transform writes unless briefly disabled.
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        if (controller != null) controller.enabled = true;
    }
}