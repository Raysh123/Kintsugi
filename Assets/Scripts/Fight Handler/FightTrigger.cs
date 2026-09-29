using UnityEngine;

/// <summary>
/// Put this on the sparring partner / dojo challenge point. Standard E/A interact
/// pattern — press to start a match, which hands off to the separate fight scene
/// via FightSessionManager.
/// </summary>
public class FightTrigger : MonoBehaviour
{
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private float maxInteractDistance = 3f;

    [Header("Fight Setup")]
    [Tooltip("Build index or scene name of your fight scene (File > Build Settings).")]
    [SerializeField] private string fightSceneName;
    [Tooltip("Identifier matched against PostFightHandler's spawn point list, so the " +
             "player reappears in the right spot back in this scene.")]
    [SerializeField] private string returnSpawnPointId;
    [Tooltip("Whatever identifier your fight scene uses to pick the opponent/difficulty.")]
    [SerializeField] private string opponentId;

    private Transform playerTransform;
    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerTransform = other.transform;
        playerInRange = true;
        if (interactPrompt != null) interactPrompt.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (!playerInRange) return;

        if (playerTransform != null &&
            Vector3.Distance(playerTransform.position, transform.position) > maxInteractDistance)
        {
            playerInRange = false;
            if (interactPrompt != null) interactPrompt.SetActive(false);
            return;
        }

        bool pressedInteract = Input.GetKeyDown(KeyCode.E) ||
                                Input.GetKeyDown(KeyCode.JoystickButton0) ||
                                Input.GetButtonDown("Submit");

        if (!pressedInteract) return;

        if (string.IsNullOrEmpty(fightSceneName))
        {
            Debug.LogWarning("[FightTrigger] Fight Scene Name not set — can't start a match.");
            return;
        }

        FightSessionManager.BeginFight(fightSceneName, returnSpawnPointId, opponentId);
    }
}
