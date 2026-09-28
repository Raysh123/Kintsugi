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
        bool locked = movementLocked || ActiveDialogueTracker.AnyActive;

        if (!interacting && !locked)
        {
            Move();
            Interact();
        }
        else if (!interacting)
        {
            // Still allow interactions even when movement is locked (bed exercises)
            Interact();
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
        bool pressedInteract = Input.GetKey(KeyCode.E) || Input.GetButtonDown("Submit");

        if (canInteract && pressedInteract && (typeA || typeB || typeC))
        {
            interacting = true;
            Debug.Log("Begin interaction");
        }
    }
}