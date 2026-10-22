using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small cycling selection menu for picking who to spar against — same cycle-with-
/// stick/confirm-with-E-or-A/cancel-with-Space-or-B pattern as TravelMapController, just
/// choosing a fight instead of a destination (see that class for the input-handling
/// rationale). Confirming hands off to FightSessionManager.BeginFight — the same call
/// FightTrigger makes directly — this is just a UI in front of it, so it can offer more
/// than one opponent/scene at a single interaction point (e.g. the sensei).
///
/// Requires an EventSystem in the scene, same as any other Unity UI Button / TravelMapController.
/// </summary>
public enum SparringPartner { None, PartnerA, PartnerB }

public class FightSelectionMenu : MonoBehaviour
{
    /// <summary>Who the fight that is about to start is against. Read (and cleared) by
    /// PostFightHandler when the player comes back with a win. Static, so it survives the
    /// scene change to the fight and back.</summary>
    public static SparringPartner pendingPartner = SparringPartner.None;

    [System.Serializable]
    public class FightOption
    {
        public string opponentName;
        [Tooltip("The option's own UI Button — also used for highlighting the current selection " +
                 "via its built-in Selected color/sprite state, no separate cursor needed.")]
        public Button icon;
        [Tooltip("Build index or scene name of the fight scene (File > Build Settings).")]
        public string fightSceneName;
        [Tooltip("Identifier matched against PostFightHandler's spawn point list, so the " +
                 "player reappears in the right spot back in this scene.")]
        public string returnSpawnPointId;
        [Tooltip("Whatever identifier your fight scene uses to pick the opponent/difficulty.")]
        public string opponentId;
        [Tooltip("Is this one of the two sparring partners that count towards the tournament? " +
                 "Beating BOTH ends the game immediately. None = a normal fight.")]
        public SparringPartner partner = SparringPartner.None;
    }

    [Header("Options")]
    [SerializeField] private List<FightOption> options = new List<FightOption>();

    [Header("UI")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private TMP_Text opponentNameText;

    [Header("Player")]
    [SerializeField] private PlayerMovement player;

    private int currentIndex = -1;

    /// <summary>Whoever opened this (e.g. SenseiInteraction) can poll this to know when to
    /// resume — there's no separate "closed without fighting" event, closing for any reason
    /// (cancel, or a fight actually starting) just flips this false.</summary>
    public bool IsOpen { get; private set; }
    [Header("Selection highlight")]
    [Tooltip("The selected button grows and turns this colour. Added automatically to each button that has no SelectableHighlight of its own.")]
    [SerializeField] private float highlightScale = 1.2f;
    [SerializeField] private Color highlightColor = new Color(1f, 0.84f, 0.3f);

    private int openedFrame = -1;

    private void Start()
    {
        foreach (var item in options)
        {
            if (item.icon == null || item.icon.GetComponent<SelectableHighlight>() != null) continue;
            item.icon.gameObject.AddComponent<SelectableHighlight>().Setup(highlightScale, highlightColor);
        }
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    public void Open()
    {
        if (IsOpen) return;

        if (options.Count == 0)
        {
            Debug.LogWarning("[FightSelectionMenu] No fight options configured - nothing to open.");
            return;
        }

        IsOpen = true;
        openedFrame = Time.frameCount;
        currentIndex = 0;
        UpdateSelectionVisual();

        if (menuPanel != null) menuPanel.SetActive(true);
        if (player != null) player.movementLocked = true;
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (player != null) player.movementLocked = false;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Whatever button opened this menu (e.g. SenseiInteraction's Space/B) is still
        // "down" for the rest of this same frame as far as Input.GetKeyDown is concerned -
        // without this guard, Close() below would fire on that same leftover press the
        // instant the menu opens, before a single frame ever renders.
        if (Time.frameCount == openedFrame) return;

        HandleCycling();

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit"))
            SelectCurrentOption();
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetButtonDown("Cancel"))
            Close();
    }

    private bool horizontalArmed = true;
    private bool verticalArmed = true;
    private const float cycleThreshold = 0.5f;

    /// <summary>
    /// Same manual debounce as TravelMapController.HandleCycling - deliberately not using
    /// Input.GetButtonDown, which can miss its single-frame edge with some controllers even
    /// though the underlying GetAxisRaw value is fine.
    /// </summary>
    private void HandleCycling()
    {
        if (options.Count == 0) return;

        float h = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(h) < cycleThreshold)
        {
            horizontalArmed = true;
        }
        else if (horizontalArmed)
        {
            horizontalArmed = false;
            Cycle(h > 0f ? 1 : -1);
            return;
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
        if (options.Count == 0) return;
        currentIndex = ((currentIndex + direction) % options.Count + options.Count) % options.Count;
        UpdateSelectionVisual();
    }

    private void UpdateSelectionVisual()
    {
        if (currentIndex < 0 || currentIndex >= options.Count) return;
        FightOption option = options[currentIndex];

        if (option.icon != null) option.icon.Select();
        if (opponentNameText != null) opponentNameText.text = option.opponentName;
    }

    private void SelectCurrentOption()
    {
        if (currentIndex < 0 || currentIndex >= options.Count) return;
        FightOption option = options[currentIndex];

        if (string.IsNullOrEmpty(option.fightSceneName))
        {
            Debug.LogWarning($"[FightSelectionMenu] Option '{option.opponentName}' has no Fight Scene Name set — can't start it.");
            return;
        }

        Close();
        pendingPartner = option.partner;
        FightSessionManager.BeginFight(option.fightSceneName, option.returnSpawnPointId, option.opponentId);
    }
}