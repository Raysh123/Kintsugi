using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float inputDeadzone = 0.1f;
    [SerializeField] private Transform cameraTransform;

    [Header("State")]
    public bool interacting;
    public bool movementLocked;
    [SerializeField] private bool canInteract;
    [SerializeField] private GameObject interactable;

    private CharacterController controller;
    [Tooltip("Animator on the player's mesh child — used only for movement blend. " +
             "Separate from the mesh child toggle so disabling the model during " +
             "activities doesn't cause null reference errors here.")]
    [SerializeField] private Animator animator;

    private RecoveryInteraction interact;
    private Recovery_Interaction_Type_B interactTypeB;
    public bool typeA;
    public bool typeB;

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
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!interacting && !movementLocked)
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
            // Move
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);

            // Rotate player to face the direction they are moving
            Quaternion targetRotation = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    private void Interact()
    {
        bool pressedInteract = Input.GetKey(KeyCode.E) || Input.GetButtonDown("Submit");

        if (canInteract && pressedInteract && (typeA || typeB))
        {
            interacting = true;
            Debug.Log("Begin interaction");
        }
    }
}