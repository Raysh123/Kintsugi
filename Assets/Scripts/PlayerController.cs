using NUnit.Framework.Constraints;
using System;
using System.Collections;
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform opponent;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float facingRotationSpeed = 50f;

    public CharacterController controller;
    private float moveX;
    public Vector3 velocity;
    public Vector3 move;
    public bool facingRight = true;
    public CapsuleCollider capCollider;
    public float dodgeTime;
    public LayerMask excludeMask;
    public LayerMask nothingLayer;
    public bool isHurt;
    public bool hurtFace;
    public bool hurtBody;
    public Vector3 PlayerInitPos;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Face") || collision.gameObject.CompareTag("Body"))
        {
            isHurt = true;
            if(collision.gameObject.CompareTag("Face"))
            {
                hurtFace = true;
            }
            else if (collision.gameObject.CompareTag("Body"))
            {
                hurtBody = true;
            }
        }
    }
    void Start()
    {
        PlayerInitPos = transform.position;
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        HandleMovement();
        HandleFacing();
        HandleAttack();
        HandleDodge();
    }

    private void HandleDodge()
    {
        if (InputHandler.instance.dodge)
        {
            controller.excludeLayers = excludeMask;
            StartCoroutine(CollisionOnOff());
        }
    }
    IEnumerator CollisionOnOff()
    {
        yield return new WaitForSeconds(dodgeTime);
        controller.excludeLayers = nothingLayer;
    }
    private void HandleAttack()
    {
        if (InputHandler.instance.punch)
        {
            //Debug.Log("punch");
        }
        if (InputHandler.instance.kick) 
        {
            //Debug.Log("kick");
        }
    }

    private void HandleMovement()
    {
        if(!isHurt)
        {
            if (gameObject.transform.rotation.y > 0.1f)
            { facingRight = true; }
            else if (gameObject.transform.rotation.y < -0.1f)
            { facingRight = false; }

            move = new Vector3(InputHandler.instance.move, 0f, 0f);
            controller.Move(move * speed * Time.deltaTime);
        }
      
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
    public void Hurt()
    {

    }
}