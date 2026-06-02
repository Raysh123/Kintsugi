using UnityEngine;
public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed;
    public bool interacting;
    [SerializeField]
    private bool canInteract;
    [SerializeField]
    private GameObject interactable;
    private CharacterController controller;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private string recoveryAnim;
    private string actionAnim;
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
            recoveryAnim = interact.relaventAnimation.ToString();
        }
        else if (other.gameObject.CompareTag("Interact2"))
        {
            typeB = true;
            interactable = other.gameObject;
            canInteract = true;
            interactTypeB = other.GetComponent<Recovery_Interaction_Type_B>();
            recoveryAnim = interactTypeB.relaventAnimation.ToString();
            actionAnim = interactTypeB.actionAnimation.ToString();
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
            recoveryAnim = null;
        }
        else if (other.gameObject.CompareTag("Interact2"))
        {
            typeB = false;
            interactable = null;
            canInteract = false;
            interactTypeB = null;
            recoveryAnim = null;
            actionAnim = null;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!interacting)
        {
            Move();
            Interact();
        }
        if (interacting && typeB)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
            {
                //Debug.Log("Push Up");
                animator.Play(actionAnim);
            }
        }
    }

    private void Move()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        if (Input.GetKey(KeyCode.A))
        {
            animator.Play("Walk_Left");
            transform.position = transform.position += transform.right * -moveSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            animator.Play("Walk_Right");
            transform.position = transform.position += transform.right * moveSpeed * Time.deltaTime;
        }
        else if (!interacting && horizontalInput == 0)
        {
            animator.Play("Idle");
        }
        if (horizontalInput != 0)
        {
            transform.position += transform.right * horizontalInput * moveSpeed * Time.deltaTime;
            if (horizontalInput < 0)
            {
                animator.Play("Walk_Left");
            }
            else if (horizontalInput > 0)
            {
                animator.Play("Walk_Right");
            }
        }

    }

    private void Interact()
    {
        if (typeA && !typeB && canInteract && (Input.GetKey(KeyCode.E) || Input.GetButtonDown("Submit")))
        {
            interacting = true;
            Debug.Log("Begin interaction");
            animator.Play(recoveryAnim);
        }
        if (typeB && !typeA && canInteract && (Input.GetKey(KeyCode.E) || Input.GetButtonDown("Submit")))
        {
            interacting = true;
            Debug.Log("Begin interaction");
            animator.Play(recoveryAnim);
        }
    }
}
