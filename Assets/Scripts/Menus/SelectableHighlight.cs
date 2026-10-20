using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes the menu button you're on obvious. Put it on each button of the main menu / pause menu. While the button is
/// selected (stick / d-pad / arrow keys) or hovered with the mouse it grows a little, its text/graphic changes colour,
/// and an optional marker object (an arrow, a glow, a brush stroke) switches on. Works while the game is paused.
/// </summary>
public class SelectableHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [Tooltip("Scale while selected (1 = no change).")]
    [SerializeField] private float selectedScale = 1.12f;
    [SerializeField] private float speed = 14f;
    [Tooltip("Optional graphic to tint (the plank image or the label). Blank = the button's own target graphic.")]
    [SerializeField] private Graphic tintTarget;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.84f, 0.3f);
    [Tooltip("Optional: an object (arrow, glow...) that is only visible while this button is selected.")]
    [SerializeField] private GameObject marker;

    private bool selected;
    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
        if (tintTarget == null)
        {
            Button b = GetComponent<Button>();
            if (b != null) tintTarget = b.targetGraphic;
        }
        Apply(1f);
    }

    private void OnEnable()
    {
        // If the menu opens with this button already selected, show it straight away.
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        Apply(1f);
    }

    private void Update()
    {
        // Catches selection changes made in code (e.g. the menu selecting its first button).
        bool nowSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        if (nowSelected != selected) selected = nowSelected;
        Apply(Mathf.Clamp01(speed * Time.unscaledDeltaTime));
    }

    private void Apply(float k)
    {
        Vector3 target = baseScale * (selected ? selectedScale : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, target, k);
        if (tintTarget != null) tintTarget.color = Color.Lerp(tintTarget.color, selected ? selectedColor : normalColor, k);
        if (marker != null && marker.activeSelf != selected) marker.SetActive(selected);
    }

    /// <summary>Used by menus that add this to their buttons in code: keeps the graphic's current colour as the normal one.</summary>
    public void Setup(float scale, Color highlightColor)
    {
        selectedScale = scale;
        selectedColor = highlightColor;
        if (tintTarget == null)
        {
            Button b = GetComponent<Button>();
            if (b != null) tintTarget = b.targetGraphic;
        }
        if (tintTarget != null) normalColor = tintTarget.color;
    }

    public void OnSelect(BaseEventData eventData) { selected = true; }
    public void OnDeselect(BaseEventData eventData) { selected = false; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
