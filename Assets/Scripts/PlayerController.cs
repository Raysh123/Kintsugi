using System;
using UnityEngine;
using UnityEngine.InputSystem;
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
    }
  
    private void HandleMovement()
    {
        move = new Vector3(InputHandler.instance.move, 0f, 0f);
        controller.Move(move * speed * Time.deltaTime);
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