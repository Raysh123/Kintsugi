using System;
using Unity.VisualScripting;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    public enum PlayerStates
    {
        Idle,
        Move,
        Attack
    };
    public PlayerStates currentState;
    public static StateMachine instance;
    public PlayerController player;
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
        }
    }

    private void HandleAttacks()
    {
        
        if (InputHandler.instance.punch == false)
        {
            ChangeState(PlayerStates.Idle);
        }
        else
        {
            Debug.Log("do attacks");
        }
    }

    private void HandleMove()
    {
        if(player.move.magnitude == 0)
        {
            ChangeState(PlayerStates.Idle);
        }
    }

    private void HandleIdle()
    {
        if (player.move.magnitude > 0)
        {
            ChangeState(PlayerStates.Move);
            Debug.Log("Moving");
        }
        if (InputHandler.instance.punch)
        {
            ChangeState(PlayerStates.Attack);
        }
        
    }

}
