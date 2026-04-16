using UnityEngine;
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform opponent;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float facingRotationSpeed = 20f;

    private CharacterController controller;
    private float moveX;
    public Vector3 velocity;
    public Vector3 move;
    public bool facingRight = true;
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        HandleMovement();
        HandleFacing();
        HandleAttack();
    }

    private void HandleAttack()
    {
        if (InputHandler.instance.punch)
        {
            Debug.Log("punch");
        }
        if (InputHandler.instance.kick) 
        {
            Debug.Log("kick");
        }
    }

    private void HandleMovement()
    {
        if (gameObject.transform.rotation.y > 0.1f)
        { facingRight = true; }
        else if (gameObject.transform.rotation.y < -0.1f)
        { facingRight = false; }

        move = new Vector3(InputHandler.instance.move, 0f, 0f);
        controller.Move(move * speed * Time.deltaTime);
        Debug.Log(move.x);
    }
    private void HandleFacing()
    {
        if (opponent == null) return;

        Vector3 dir = opponent.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            facingRotationSpeed * Time.deltaTime
        );
    }
}