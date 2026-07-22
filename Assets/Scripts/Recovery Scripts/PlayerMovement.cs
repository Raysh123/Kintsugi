using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float inputDeadzone = 0.1f;

    [Header("State")]
    public bool interacting;
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
        if (!interacting)
        {
            Move();
            Interact();
        }
    }

    private void Move()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 moveDir = new Vector3(horizontalInput, 0f, verticalInput);
        float inputMagnitude = moveDir.magnitude;

        // Guard: animator's GameObject is disabled during interactions, skip SetFloat then.
        if (animator != null && animator.gameObject.activeInHierarchy)
            animator.SetFloat("MoveSpeed", inputMagnitude);

        if (inputMagnitude > inputDeadzone)
        {
            Vector3 motion = moveDir.normalized * moveSpeed * Time.deltaTime;
            controller.Move(motion);

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