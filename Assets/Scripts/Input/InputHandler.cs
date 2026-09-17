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

    private bool inputEnabled = true;

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
    public void SetInput(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            move = 0;
            speed = 0;
            kick = false;
            punch = false;
            dodge = false;
            block = false;
        }
    }
    public void onMove(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled)
        {
            return;
        }
        move = ctx.ReadValue<float>();
    }
    public void onPunch(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled)
        {
            return;
        }
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
        if (!inputEnabled)
        {
            return;
        }
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
        if (!inputEnabled)
        {
            return;
        }
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
        if (!inputEnabled)
        {
            return;
        }
        if (ctx.performed)
        {
            block = true;
        }
        else if (ctx.canceled)
        {
            block = false;
        }
    }
    public void onLowerKick(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled)
        {
            return;
        }
        if (ctx.performed)
        {
            //kick = true;
            Debug.Log("LK");
        }
        else if (ctx.canceled)
        {
            //kick = false;
        }
    }
    public void onUpperPunch(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled)
        {
            return;
        }
        if (ctx.performed)
        {
            //kick = true;
            Debug.Log("UP");
        }
        else if (ctx.canceled)
        {
            //kick = false;
        }
    }
    public void onUpperKick(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled)
        {
            return;
        }
        if (ctx.performed)
        {
            //kick = true;
            Debug.Log("UK");
        }
        else if (ctx.canceled)
        {
            //kick = false;
        }
    }

}
