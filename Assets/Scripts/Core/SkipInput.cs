using UnityEngine;

/// <summary>
/// Shared "skip/advance/dismiss" input check, used across every dialogue and
/// blocking UI wait in the project (DialogueRunner, ConversationRunner,
/// onboarding popups, etc). Explicitly checks gamepad A (JoystickButton0)
/// directly rather than relying solely on Unity's "Submit" virtual button
/// being correctly configured in the Input Manager, plus Enter as the
/// keyboard equivalent — so this works consistently regardless of Input
/// Manager setup.
/// </summary>
public static class SkipInput
{
    public static bool Pressed()
    {
        return Input.GetKeyDown(KeyCode.JoystickButton0)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetButtonDown("Submit");
    }
}