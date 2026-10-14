using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputHandler : MonoBehaviour
{
    public bool kick;
    public bool punch;
    public float move;
    public float speed;
    public bool dodge;
    public bool block;
    public bool swap;

    private bool inputEnabled = true;

    public static InputHandler instance;
    private void Awake()
    {
        instance = this;
    }

    private float diagTimer;
    private bool diagReported;

    private void Start()
    {
        // Build diagnostics: shows which PlayerInput owns the devices in THIS scene.
        PlayerInput[] all = FindObjectsByType<PlayerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[InputHandler] Scene '{gameObject.scene.name}': {all.Length} PlayerInput component(s) found.");
        foreach (PlayerInput pi in all)
        {
            string devices = "";
            foreach (var d in pi.devices) devices += d.displayName + ", ";
            Debug.Log($"[InputHandler]  - '{pi.name}' (scene '{pi.gameObject.scene.name}') enabled={pi.enabled} " +
                      $"active={pi.gameObject.activeInHierarchy} actions={(pi.actions != null ? pi.actions.name : "NONE")} " +
                      $"map={(pi.currentActionMap != null ? pi.currentActionMap.name : "NONE")} " +
                      $"scheme={pi.currentControlScheme} devices=[{devices}] notification={pi.notificationBehavior}");
        }
        if (all.Length == 0)
            Debug.LogWarning("[InputHandler] No PlayerInput in the scene - nothing will ever call onMove/onPunch.");
    }

    private void Update()
    {
        // One-time: report what the keyboard does at the raw level, 3s in.
        diagTimer += Time.unscaledDeltaTime;
        if (!diagReported && diagTimer > 3f)
        {
            diagReported = true;
            Debug.Log($"[InputHandler] After 3s: inputEnabled={inputEnabled}, timeScale={Time.timeScale}, " +
                      $"Keyboard.current={(Keyboard.current != null ? Keyboard.current.displayName : "NULL")}");
        }
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame && Time.frameCount % 1 == 0)
            Debug.Log($"[InputHandler] key pressed. inputEnabled={inputEnabled} move={move} punch={punch} kick={kick} block={block} dodge={dodge}");
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
            swap = false;
        }
    }
    /// <summary>Wire this to the "Swap" action in the PlayerInput Invoke Unity Events list.</summary>
    public void onSwap(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled) return;
        if (ctx.performed) swap = true;
        else if (ctx.canceled) swap = false;
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