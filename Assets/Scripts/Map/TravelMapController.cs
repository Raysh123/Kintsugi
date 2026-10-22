using System.Collections;
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
/// Cycling manually debounces raw GetAxisRaw values rather than using
/// Input.GetButtonDown - see HandleCycling for why.
///
/// Requires an EventSystem in the scene, same as any other Unity UI Button.
/// </summary>
public class TravelMapController : MonoBehaviour
{
    [System.Serializable]
    public class TravelLocation
    {
        public string locationName;
        [Tooltip("The pin itself, as a UI Button - also used for highlighting the current selection " +
                 "via its own built-in Selected color/sprite state, no separate cursor needed.")]
        public Button icon;
        [Tooltip("The player teleports to this Transform's position/rotation when selected.")]
        public Transform destination;
        [Tooltip("This location becomes selectable once the story reaches this phase (or later).")]
        public StoryPhase unlockPhase = StoryPhase.BedRest;
        [Tooltip("Tick to make this location unavailable for the whole of one story phase (e.g. Park during " +
                 "NewDojoJoin). It is selectable again as soon as the story moves past that phase.")]
        public bool lockDuringPhase;
        public StoryPhase lockedPhase = StoryPhase.NewDojoJoin;
        [Tooltip("Which times of day this location can be traveled to. Leave EMPTY for no " +
                 "restriction (selectable any time, once unlocked) - e.g. Park: Morning + " +
                 "Evening only, Dojo: Morning + Afternoon + Evening (no Night).")]
        public List<DayPhase> allowedTimesOfDay = new List<DayPhase>();
        [Tooltip("Optional: a component implementing ITravelArrivalHandler. Its OnArrival() is " +
                 "called automatically right after the player is teleported here - this is how " +
                 "location-specific behavior (like ParkActivity starting its run sequence) plugs " +
                 "in without TravelMapController needing to know about it directly.")]
        public MonoBehaviour arrivalHandler;
    }

    [Header("Locations")]
    [SerializeField] private List<TravelLocation> locations = new List<TravelLocation>();

    [Header("UI")]
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private TMP_Text locationNameText;

    [Header("Travel Transition")]
    [Tooltip("Shown between teleporting the player and calling the destination's arrival " +
             "handler - masks the follow-camera's catch-up time and forces arrival-triggered " +
             "gameplay (e.g. ParkActivity) to wait until it finishes. Optional - if left " +
             "unassigned, arrival happens immediately after teleporting, same as before.")]
    [SerializeField] private TravelLoadingScreen loadingScreen;

    [Header("Player")]
    [SerializeField] private PlayerMovement player;

    private int currentIndex = -1;
    private bool isOpen;

    [Tooltip("Index into Locations matching wherever the player actually starts the game " +
             "(e.g. Home). Leave at -1 if that starting spot isn't one of the travel " +
             "locations - in that case nothing is excluded until the player's first trip.")]
    [SerializeField] private int startingLocationIndex = -1;
    private int currentLocationIndex;

    /// <summary>True while the map is locked by the story - OpenMap does nothing, so doors can't open it.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>Locks or unlocks the whole map. Locking while it's open closes it.</summary>
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (locked && isOpen) CloseMap();
    }

    [Header("Selection highlight")]
    [Tooltip("The selected pin grows by this factor (1 = no growth). Sprite is left to the Button's own Sprite Swap.")]
    [SerializeField] private float highlightScale = 1.04f;
    [SerializeField] private float highlightSpeed = 14f;
    private readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();

    private void Start()
    {
        foreach (var item in locations)
        {
            if (item.icon == null) continue;
            baseScales[item.icon.transform] = item.icon.transform.localScale;
            SpriteState ss = item.icon.spriteState;
            if (ss.highlightedSprite != null)
            {
                ss.selectedSprite = ss.highlightedSprite;
                item.icon.spriteState = ss;
            }
        }
        currentLocationIndex = startingLocationIndex;
        RestoreCurrentLocation();
        if (mapPanel != null) mapPanel.SetActive(false);
        RefreshIconVisibility();
    }

    private static string LocationFlag(int index) => "MapLocation_" + index;

    /// <summary>This scene object is rebuilt on every scene load (e.g. coming back from a fight), so where the
    /// player last travelled to is remembered in PersistentGameFlags.</summary>
    private void SaveCurrentLocation()
    {
        if (PersistentGameFlags.instance == null) return;

        for (int i = 0; i < locations.Count; i++)
            PersistentGameFlags.instance.ClearFlag(LocationFlag(i));

        PersistentGameFlags.instance.SetFlag(LocationFlag(currentLocationIndex));
    }

    private void RestoreCurrentLocation()
    {
        if (PersistentGameFlags.instance == null) return;

        for (int i = 0; i < locations.Count; i++)
        {
            if (!PersistentGameFlags.instance.HasFlag(LocationFlag(i))) continue;
            currentLocationIndex = i;
            return;
        }
    }

    public void OpenMap()
    {
        if (isOpen || IsLocked) return;

        RefreshIconVisibility();
        currentIndex = FindNextUnlockedIndex(0, 1);
        isOpen = true;

        if (currentIndex == -1)
        {
            // Nothing selectable right now (everything's locked, wrong time of day, or the
            // only unlocked spot is wherever the player already is) - still open the map so
            // the player can see that for themselves, rather than the map silently refusing
            // to open with no feedback at all.
            if (locationNameText != null) locationNameText.text = "Nowhere to go right now";
        }
        else
        {
            UpdateSelectionVisual();
        }

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

    private void UpdatePinScales()
    {
        float k = Mathf.Clamp01(highlightSpeed * Time.unscaledDeltaTime);
        for (int i = 0; i < locations.Count; i++)
        {
            Button icon = locations[i].icon;
            if (icon == null || !baseScales.TryGetValue(icon.transform, out Vector3 baseScale)) continue;
            bool selected = isOpen && i == currentIndex && icon.interactable;
            icon.transform.localScale = Vector3.Lerp(icon.transform.localScale, baseScale * (selected ? highlightScale : 1f), k);
        }
    }

    private void Update()
    {
        UpdatePinScales();
        if (!isOpen) return;

        HandleCycling();

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            SelectCurrentLocation();
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetButtonDown("Cancel"))
            CloseMap();
    }

    private bool horizontalArmed = true;
    private bool verticalArmed = true;
    private const float cycleThreshold = 0.5f;

    /// <summary>
    /// Deliberately NOT using Input.GetButtonDown here - that relies on Unity's own
    /// single-frame "just pressed" edge detection, which can miss with some
    /// controller/driver combinations even though the underlying GetAxisRaw value itself
    /// is fine (confirmed by PlayerMovement, which reads the same axes continuously and
    /// works). Instead this polls the raw value directly every frame and debounces it
    /// manually: once a direction fires, that axis is "disarmed" until it comes back
    /// past cycleThreshold toward neutral, so holding the stick over doesn't spam-cycle.
    /// </summary>
    private void HandleCycling()
    {
        float h = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(h) < cycleThreshold)
        {
            horizontalArmed = true;
        }
        else if (horizontalArmed)
        {
            horizontalArmed = false;
            Cycle(h > 0f ? 1 : -1);
            return; // one cycle per frame max, even if both axes are held past threshold
        }

        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(v) < cycleThreshold)
        {
            verticalArmed = true;
        }
        else if (verticalArmed)
        {
            verticalArmed = false;
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
            if (IsSelectable(index)) return index;
            index = ((index + direction) % locations.Count + locations.Count) % locations.Count;
        }
        return -1;
    }

    private bool IsUnlocked(TravelLocation location)
    {
        if (StoryManager.instance == null) return false;
        StoryPhase current = StoryManager.instance.CurrentPhase;
        if (location.lockDuringPhase && current == location.lockedPhase) return false;
        return current >= location.unlockPhase;
    }

    /// <summary>Empty allowedTimesOfDay means "no restriction" - always allowed, once unlocked.</summary>
    private bool IsTimeAllowed(TravelLocation location)
    {
        if (location.allowedTimesOfDay == null || location.allowedTimesOfDay.Count == 0) return true;
        return CalendarManager.instance != null &&
               location.allowedTimesOfDay.Contains(CalendarManager.instance.CurrentPhase);
    }

    /// <summary>The full gate for "can the player actually travel here right now": story-unlocked,
    /// the right time of day, and not the location the player is already standing at.</summary>
    private bool IsSelectable(int index)
    {
        if (index == currentLocationIndex) return false;
        TravelLocation location = locations[index];
        return IsUnlocked(location) && IsTimeAllowed(location);
    }

    [Header("Unavailable locations")]
    [Tooltip("Locked or wrong-time-of-day pins stay on the map, tinted this colour, instead of being hidden.")]
    [SerializeField] private Color unavailableColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private readonly Dictionary<UnityEngine.UI.Graphic, Color> originalIconColors = new Dictionary<UnityEngine.UI.Graphic, Color>();

    private void RefreshIconVisibility()
    {
        for (int i = 0; i < locations.Count; i++)
        {
            TravelLocation location = locations[i];
            if (location.icon == null) continue;

            // Unlocked-but-currently-unselectable (wrong time of day, or it's where the
            // player already is) still shows the pin - just not interactable - rather than
            // hiding it outright, so it doesn't look like it vanished from the map.
            bool unlocked = IsUnlocked(location);
            location.icon.gameObject.SetActive(true);
            location.icon.interactable = unlocked && IsSelectable(i);

            // Pins that aren't available (locked, or the wrong time of day) are greyed out rather than hidden.
            bool unavailable = !unlocked || !IsTimeAllowed(location);
            foreach (UnityEngine.UI.Graphic g in location.icon.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (!originalIconColors.ContainsKey(g)) originalIconColors[g] = g.color;
                Color o = originalIconColors[g];
                float lum = o.grayscale;
                g.color = unavailable ? new Color(lum, lum, lum, o.a) * unavailableColor : o;
            }
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

        if (!IsSelectable(currentIndex) || location.destination == null) return;

        StartCoroutine(TravelSequence(location));
    }

    /// <summary>
    /// Teleport -> (optional) loading screen -> arrival handler, in that order, with player
    /// movement held locked for the whole sequence. The loading screen isn't just cosmetic
    /// here: it's what the destination's OnArrival() waits on, so e.g. ParkActivity's run
    /// minigame can't start mid camera-catch-up.
    /// </summary>
    private IEnumerator TravelSequence(TravelLocation location)
    {
        TeleportPlayer(location.destination);
        currentLocationIndex = currentIndex;
        SaveCurrentLocation();

        // CloseMap() unconditionally unlocks movement (it's also the normal Cancel/close
        // path, where that's exactly what should happen) - immediately re-lock right after
        // so the player can't move during the loading screen below.
        CloseMap();
        if (player != null) player.movementLocked = true;

        if (loadingScreen != null)
            yield return loadingScreen.Play();

        if (player != null) player.movementLocked = false;

        // Ambience follows the location the player just arrived at (area name = location name).
        RecoverySoundscape.SetArea(location.locationName);

        // This is the hook: whatever component was dragged into arrivalHandler gets
        // its OnArrival() called here, as long as it implements ITravelArrivalHandler.
        if (location.arrivalHandler is ITravelArrivalHandler handler)
            handler.OnArrival();
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

        Physics.SyncTransforms();
        if (controller != null) controller.enabled = true;
        PlayerFallSafety.MarkSafe(destination.position, destination.rotation);
    }
}