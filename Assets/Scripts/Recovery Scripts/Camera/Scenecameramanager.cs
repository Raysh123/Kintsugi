using UnityEngine;

/// <summary>
/// Scene-local (NOT persistent) singleton that handles switching from the main
/// camera to an activity-specific camera and back. Place one in each scene that
/// has activities with their own cameras (dojo, home, etc.) — it does NOT use
/// DontDestroyOnLoad, since each scene should have its own main camera anyway.
///
/// Only the main camera should have an AudioListener enabled. Don't add
/// AudioListener components to activity cameras — this avoids Unity's
/// "2 audio listeners" warning without needing to juggle that too.
/// </summary>
public class SceneCameraManager : MonoBehaviour
{
    public static SceneCameraManager instance;

    [Tooltip("The default top-down follow camera. Auto-assigned from Camera.main if left empty.")]
    [SerializeField] private Camera mainCamera;

    private Camera currentActivityCamera;

    private void Awake()
    {
        instance = this;
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    /// <summary>Switches from the main camera to the given activity camera.</summary>
    public void ActivateCamera(Camera activityCamera)
    {
        if (activityCamera == null) return;

        currentActivityCamera = activityCamera;
        mainCamera.gameObject.SetActive(false);
        activityCamera.gameObject.SetActive(true);
    }

    /// <summary>
    /// Switches back to the main camera. Guarded by identity check so an
    /// out-of-order or duplicate call (e.g. from a script that ended early)
    /// can't stomp on a different activity's camera that's now active.
    /// </summary>
    public void DeactivateCamera(Camera activityCamera)
    {
        if (activityCamera == null || currentActivityCamera != activityCamera) return;

        activityCamera.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);
        currentActivityCamera = null;
    }
}