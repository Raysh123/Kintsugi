using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public bool kick;
    public bool punch;
    public float move;
    public float speed;
    public bool dodge;
    public bool block;

    public static InputHandler instance;
    private void Awake()
    {
        instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void onMove(InputAction.CallbackContext ctx)
    {
        move = ctx.ReadValue<float>();
    }
    public void onPunch(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            punch = true;
        }
        else if (ctx.canceled)
        {
            punch = false;
        }
    }
    public void onKick(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            kick = true;
        }
        else if (ctx.canceled)
        {
            kick = false;
        }
    }
    public void onDodge(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            dodge = true;
        }
        else if (ctx.canceled)
        {
            dodge = false;
        }
    }
    public void onBlock(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            block = true;
        }
        else if (ctx.canceled)
        {
            block = false;
        }
    }

}
