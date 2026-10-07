using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("How far the backward dash actually travels, over Dodge Time seconds.")]
    [SerializeField] private float dodgeDistance = 6f;
    public LayerMask excludeMask;
    public LayerMask nothingLayer;
    public bool isHurt;
    public bool hurtFace;
    public bool hurtBody;
    public Vector3 PlayerInitPos;
    public MatchTimer matchTimer;

    private bool dodgeWasHeld;
    private Coroutine dodgeRoutine;

    //private void OnCollisionEnter(Collision collision)
    //{
    //    if(collision.gameObject.CompareTag("Face") || collision.gameObject.CompareTag("Body"))
    //    {
    //        isHurt = true;
    //        if(collision.gameObject.CompareTag("Face"))
    //        {
    //            hurtFace = true;
    //        }
    //        else if (collision.gameObject.CompareTag("Body"))
    //        {
    //            hurtBody = true;
    //        }
    //    }
    //}
    void Start()
    {
        PlayerInitPos = transform.position;
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (matchTimer.matchStopped)
        {
            return;
        }
        HandleMovement();
        HandleFacing();
        HandleAttack();
        HandleDodge();
    }

    private void HandleDodge()
    {
        // Edge-detected (just pressed), not held - checking InputHandler.instance.dodge
        // directly every frame (the old version) restarted CollisionOnOff every single frame
        // the button stayed held, stacking multiple overlapping coroutines.
        bool dodgePressed = InputHandler.instance.dodge && !dodgeWasHeld;
        dodgeWasHeld = InputHandler.instance.dodge;

        if (dodgePressed)
        {
            controller.excludeLayers = excludeMask;

            if (dodgeRoutine != null) StopCoroutine(dodgeRoutine);
            dodgeRoutine = StartCoroutine(DodgeDashRoutine());
        }
    }

    /// <summary>Actually moves the player a short distance directly away from the opponent
    /// over dodgeTime seconds, via the CharacterController (so it respects collisions the
    /// same way normal movement does) - rather than just playing the dodge animation in
    /// place. Also restores excludeLayers at the end, replacing the old separate
    /// CollisionOnOff coroutine.</summary>
    IEnumerator DodgeDashRoutine()
    {
        float awayDir = opponent != null
            ? Mathf.Sign(transform.position.x - opponent.position.x)
            : (facingRight ? -1f : 1f);
        if (awayDir == 0f) awayDir = facingRight ? -1f : 1f; // directly overlapping opponent on X - fall back to facing

        float elapsed = 0f;
        while (elapsed < dodgeTime)
        {
            elapsed += Time.deltaTime;
            float step = (dodgeDistance / dodgeTime) * Time.deltaTime;
            controller.Move(new Vector3(awayDir * step, 0f, 0f));
            yield return null;
        }

        controller.excludeLayers = nothingLayer;
        dodgeRoutine = null;
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
        if (!isHurt)
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