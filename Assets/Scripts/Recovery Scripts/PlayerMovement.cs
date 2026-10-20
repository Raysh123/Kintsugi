using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float inputDeadzone = 0.1f;
    [SerializeField] private Transform cameraTransform;

    [Header("Gravity")]
    [Tooltip("Negative value — how strongly the player is pulled down each second while airborne.")]
    [SerializeField] private float gravity = -20f;
    [Tooltip("Small constant downward force applied while grounded, keeping CharacterController.isGrounded " +
             "reliably true instead of flickering on slopes/stairs. Should stay a small negative number.")]
    [SerializeField] private float groundedStickForce = -2f;
    private float verticalVelocity;

    [Header("State")]
    public bool interacting;
    public bool movementLocked;
    [Tooltip("Separate from movementLocked - StoryEnvironmentController owns movementLocked " +
             "and unconditionally overwrites it based on StoryPhase alone (locked only during " +
             "BedRest), so anything else that needs to lock movement for a reason unrelated to " +
             "story phase (e.g. ComicCutscene) would get silently un-locked the moment that " +
             "phase-driven check next runs. This flag is OR'd in below instead, the same way " +
             "ActiveDialogueTracker.AnyActive already is, so it can't be stomped by that system.")]
    public bool cutsceneLocked;
    [SerializeField] private bool canInteract;
    [SerializeField] private GameObject interactable;

    private CharacterController controller;
    [Tooltip("Animator on the player's mesh child - used only for movement blend. " +
             "Separate from the mesh child toggle so disabling the model during " +
             "activities doesn't cause null reference errors here. Call SetAnimator() " +
             "to point this at a different model's Animator (e.g. DojoAppearanceZone " +
             "swapping in the dojo outfit's rig) - movement blend keeps working on " +
             "whichever model is actually visible.")]
    [SerializeField] private Animator animator;

    public void SetAnimator(Animator newAnimator) => animator = newAnimator;

    private RecoveryInteraction interact;
    private Recovery_Interaction_Type_B interactTypeB;
    private Recovery_Interaction_Type_C interactTypeC;
    public bool typeA;
    public bool typeB;
    public bool typeC;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Interact"))
        {
            typeA = true;
            interactable = other.gameObject;
            canInteract = true;
            interact = other.GetComponent<RecoveryInteraction>();
        }
        else if (other.gameObject.CompareTag("Interact2"))
        {
            typeB = true;
            interactable = other.gameObject;
            canInteract = true;
            interactTypeB = other.GetComponent<Recovery_Interaction_Type_B>();
        }
        else if (other.gameObject.CompareTag("Interact3"))
        {
            typeC = true;
            interactable = other.gameObject;
            canInteract = true;
            interactTypeC = other.GetComponent<Recovery_Interaction_Type_C>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Interact"))
        {
            typeA = false;
            interactable = null;
            canInteract = false;
            interact = null;
        }
        else if (other.gameObject.CompareTag("Interact2"))
        {
            typeB = false;
            interactable = null;
            canInteract = false;
            interactTypeB = null;
        }
        else if (other.gameObject.CompareTag("Interact3"))
        {
            typeC = false;
            interactable = null;
            canInteract = false;
            interactTypeC = null;
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // ActiveDialogueTracker.AnyActive covers ANY DialogueRunner/ConversationRunner
        // currently on screen, anywhere - including dialogue that fires automatically
        // (e.g. OneTimePhaseDialogueTrigger) with no interaction script wrapping it to
        // set movementLocked manually. movementLocked itself is left in place for
        // non-dialogue locks (e.g. the fight selection menu being open).
        bool locked = movementLocked || cutsceneLocked || ActiveDialogueTracker.AnyActive;

        if (!interacting && !locked)
        {
            Move();
            Interact();
        }
        else
        {
            // Move() is what feeds the walk blend, so while it isn't being called the Animator
            // keeps whatever speed it last had - he'd keep walking on the spot (and, with it,
            // the footsteps) the whole time a menu or activity is open. Zero it here.
            if (animator != null && animator.gameObject.activeInHierarchy)
                animator.SetFloat("MoveSpeed", 0f);

            // Still allow interactions even when movement is locked (bed exercises)
            if (!interacting) Interact();
        }
    }

    private void Move()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        if (Mathf.Abs(horizontalInput) < inputDeadzone) horizontalInput = 0f;
        if (Mathf.Abs(verticalInput) < inputDeadzone) verticalInput = 0f;

        // Get camera forward and right projected flat onto the ground plane
        // so the move direction is always relative to where the camera is looking,
        // regardless of orbit angle — but Y is stripped so we never move up/down
        Transform cam = cameraTransform != null ? cameraTransform : Camera.main.transform;
        Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

        Vector3 moveDir = (camForward * verticalInput + camRight * horizontalInput);
        float inputMagnitude = moveDir.magnitude;

        if (animator != null && animator.gameObject.activeInHierarchy)
            animator.SetFloat("MoveSpeed", inputMagnitude);

        if (inputMagnitude > 0.01f)
        {
            // Rotate player to face the direction they are moving
            Quaternion targetRotation = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // Gravity. CharacterController already handles stepping up stairs (via its own
        // Step Offset setting in the Inspector) and blocking on solid colliders — this
        // just supplies the missing vertical component so the controller actually falls
        // when the ground drops away, instead of floating at a fixed height.
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 horizontalMove = moveDir.normalized * moveSpeed;
        Vector3 finalMove = new Vector3(horizontalMove.x, verticalVelocity, horizontalMove.z);
        controller.Move(finalMove * Time.deltaTime);
    }

    private void Interact()
    {
        // Never while a dialogue is on screen: the press that advances / skips a line must not also start an interaction.
        if (ActiveDialogueTracker.AnyActive) return;

        bool pressedInteract = Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Submit");
        if (!canInteract || !pressedInteract) return;

        // Only start an interaction if the thing in front of us really has an interaction script that is ready
        // to run it - otherwise 'interacting' would be set and nothing would ever clear it (stuck player).
        bool target =
            (typeA && interact != null && interact.isActiveAndEnabled) ||
            (typeB && interactTypeB != null && interactTypeB.isActiveAndEnabled) ||
            (typeC && interactTypeC != null && interactTypeC.isActiveAndEnabled) ||
            CatReady();

        if (target) interacting = true;
    }

    // The cat is tagged "Interact" too but has its own script instead of a Recovery one.
    private bool CatReady()
    {
        if (interactable == null) return false;
        CatPlayInteraction cat = interactable.GetComponent<CatPlayInteraction>();
        return cat != null && cat.isActiveAndEnabled;
    }
}