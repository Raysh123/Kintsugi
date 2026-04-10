using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public bool punch;
    public float move;
    public float speed;
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
        punch = ctx.ReadValue<bool>();
    }
}
