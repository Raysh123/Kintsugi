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
    [Range(0.1f, 1f)]
    [Tooltip("Walking AWAY from the opponent is this fraction of the forward walking speed (0.75 = 75%).")]
    [SerializeField] private float backwardSpeedMultiplier = 0.75f;

    public CharacterController controller;
    private float moveX;
    public Vector3 velocity;
    public Vector3 move;
    public bool facingRight = true;
    public CapsuleCollider capCollider;
    public float dodgeTime;
    [Tooltip("How far the backward dash actually travels, over Dodge Time seconds.")]
    [SerializeField] private float dodgeDistance = 2f;
    public LayerMask excludeMask;
    public LayerMask nothingLayer;
    public bool isHurt;
    public bool hurtFace;
    public bool hurtBody;
    public Vector3 PlayerInitPos;
    public MatchTimer matchTimer;

    /// <summary>Scales walking speed. 1 = normal. PlayerStateMachine lowers it during a moving punch / kick.</summary>
    [System.NonSerialized] public float moveSpeedMultiplier = 1f;

    [Header("Opponent is solid")]
    [Tooltip("ON = you can't walk into or through the opponent; you stop at Min Body Gap. (Use the swap to get past.)")]
    [SerializeField] private bool blockWalkingThroughOpponent = true;
    [Tooltip("Closest you can walk up to the opponent, measured along X between the two pivots. Raise it if " +
             "the models still overlap when you stop, lower it if there's a visible gap.")]
    [SerializeField] private float minBodyGap = 0.8f;

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
        if (matchTimer == null || matchTimer.matchStopped)
        {
            return;
        }
        // Stunned: no walking, no dodging (input is also locked in InputHandler, this is the backstop).
        bool stunned = PlayerStateMachine.instance != null && PlayerStateMachine.instance.IsStunned;
        bool locked = stunned || PlayerSwap.Swapping;
        if (!locked) HandleMovement();
        if (!PlayerSwap.Swapping) HandleFacing();   // the swap turns the player itself
        HandleAttack();
        if (!locked) HandleDodge();
    }

    private void HandleDodge()
    {
        // Edge-detected (just pressed), not held - checking InputHandler.instance.dodge
        // directly every frame (the old version) restarted CollisionOnOff every single frame
        // the button stayed held, stacking multiple overlapping coroutines.
        bool dodgePressed = InputHandler.instance.dodge && !dodgeWasHeld;
        dodgeWasHeld = InputHandler.instance.dodge;

        if (dodgePressed && (PlayerStateMachine.instance == null || PlayerStateMachine.instance.TryConsumeDodge()))
            StartDodgeDash();
    }

    /// <summary>Starts the backward dash. Public so PlayerSwap can release a dodge it held back.</summary>
    public void StartDodgeDash()
    {
        controller.excludeLayers = excludeMask;

        if (dodgeRoutine != null) StopCoroutine(dodgeRoutine);
        dodgeRoutine = StartCoroutine(DodgeDashRoutine());
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
            Vector3 delta = move * speed * moveSpeedMultiplier * Time.deltaTime;

            // Walking away from the opponent is slower than walking toward him.
            if (opponent != null)
            {
                float toward = Mathf.Sign(opponent.position.x - transform.position.x);
                if (delta.x * toward < 0f) delta.x *= backwardSpeedMultiplier;
            }

            // Solid opponent: can't walk into / through him. Walking away is always allowed.
            if (blockWalkingThroughOpponent && opponent != null)
            {
                float towardSign = Mathf.Sign(opponent.position.x - transform.position.x);
                float gap = Mathf.Abs(opponent.position.x - transform.position.x);
                if (delta.x * towardSign > 0f)
                {
                    float allowed = Mathf.Max(0f, gap - minBodyGap);
                    if (Mathf.Abs(delta.x) > allowed) delta.x = towardSign * allowed;
                }
            }
            controller.Move(delta);
        }

    }
    private void HandleFacing()
    {
        if (opponent == null) return;

        // Face along the mat (left / right) only. Using the full direction made two fighters whose colliders overlap
        // swing round to face each other along the depth axis and circle each other.
        float dx = opponent.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return;   // practically on top of each other: keep the current facing
        Vector3 dir = new Vector3(Mathf.Sign(dx), 0f, 0f);

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