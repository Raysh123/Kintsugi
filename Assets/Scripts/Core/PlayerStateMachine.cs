using System.Collections;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerStates
    {
        Idle,
        Move,
        Attack,
        Dodge,
        Block,
        Hurt
    };
    public PlayerStates currentState;
    public static PlayerStateMachine instance;
    public PlayerController player;
    [SerializeField]
    private Animator animator;
    private void Awake()
    {
        if (instance != null)
        {
            instance = this;
        }
    }
    private void Start()
    {
        ChangeState(PlayerStates.Idle);
    }
    private void Update()
    {
        HandleStates();
        if (player.isHurt)
        { currentState = PlayerStates.Hurt; }
    }
    public void ChangeState(PlayerStates newState)
    {
        currentState = newState;
    }
    private void HandleStates()
    {
        switch (currentState)
        {
            case PlayerStates.Idle:
                HandleIdle();
                break;
            case PlayerStates.Move:
                HandleMove();
                break;
            case PlayerStates.Attack:
                HandleAttacks();
                break;
            case PlayerStates.Dodge:
                HandleDodge();
                break;
            case PlayerStates.Block:
                HandleBlock();
                break;
            case PlayerStates.Hurt:
                HandleHurt();
                break;
        }
    }

    private void HandleHurt()
    {
        if (player.hurtFace)
        {
            StartCoroutine(FaceHurt());
        }
        else if (player.hurtBody)
        {
            StartCoroutine(BodyHurt());
        }
    }
    IEnumerator FaceHurt()
    {
        animator.Play("Hurt_Face");
        player.hurtFace = false;
        yield return new WaitForSeconds(.5f);
        player.isHurt = false;
        ChangeState(PlayerStates.Idle);
    }
    IEnumerator BodyHurt()
    {
        animator.Play("Hurt_Balls");
        player.hurtBody = false;
        yield return new WaitForSeconds(.5f);
        player.isHurt = false;
        ChangeState(PlayerStates.Idle);
    }

    private void HandleBlock()
    {
        if (!player.isHurt)
        {
            if (InputHandler.instance.block == false)
            {
                ChangeState(PlayerStates.Idle);
            }
        }
    }

    private void HandleDodge()
    {
        if (!player.isHurt)
        {
            if (InputHandler.instance.dodge == false)
            {
                ChangeState(PlayerStates.Idle);
            }
        }
    }

    private void HandleAttacks()
    {
        if (!player.isHurt)
        {


            if (InputHandler.instance.punch == false & InputHandler.instance.kick == false)
            {
                ChangeState(PlayerStates.Idle);
            }
            else if (InputHandler.instance.kick == false & InputHandler.instance.punch == true)
            {
                animator.Play("Kizami_Tsuki");
            }
            else if (InputHandler.instance.kick == true & InputHandler.instance.punch == false)
            {
                animator.Play("YokoGeri");
            }

        }

    }

    private void HandleMove()
    {
        if (!player.isHurt)
        {
            if (player.move.magnitude == 0)
            {
                ChangeState(PlayerStates.Idle);
                animator.Play("Idle");
            }
        }

    }

    private void HandleIdle()
    {
        if (!player.isHurt)
        {

            if (player.move.magnitude > 0)
            {
                ChangeState(PlayerStates.Move);
                if (player.facingRight)
                {
                    if (player.move.x > 0)
                    {
                        animator.Play("Walk_Forward");
                    }
                    else if (player.move.x < 0)
                    {
                        animator.Play("Walk_Backward");
                    }
                }
                else if (!player.facingRight)
                {
                    if (player.move.x > 0)
                    {
                        animator.Play("Walk_Backward");
                    }
                    else if (player.move.x < 0)
                    {
                        animator.Play("Walk_Forward");
                    }
                }

            }
            if (InputHandler.instance.punch || InputHandler.instance.kick)
            {
                ChangeState(PlayerStates.Attack);
            }
            if (InputHandler.instance.dodge)
            {
                ChangeState(PlayerStates.Dodge);
                animator.Play("Dodge_2");
            }
            if (InputHandler.instance.block)
            {
                ChangeState(PlayerStates.Block);
                animator.Play("Lower_Block");
            }

        }

    }

}
