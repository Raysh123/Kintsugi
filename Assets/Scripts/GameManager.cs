using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public PlayerController player;
    public EnemyStateMachine enemy;
    public Posture playerPosture;
    public Posture enemyPosture;
    [SerializeField] float resetDuration = 0.6f;

    Vector3 playerStartPosition;
    Vector3 enemyStartPosition;
    Coroutine resetRoutine;
    [SerializeField] Animator canvasAnim;
    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
    }
    //public void ResetPos()
    //{
    //    player.controller.enabled = false;
    //    player.transform.position = player.PlayerInitPos;
    //    player.controller.enabled = true;
    //    enemy.transform.Translate(enemy.enemyInitPos);

    //    //enemy.transform.position = enemy.enemyInitPos;
    //}
    public void ResetPos()
    {
        enemy.StopForRoundReset();
        InjuryMeter.instance.injuryMeterValue.value = 0;
        enemyPosture.postureSlider.value = 0;
        playerPosture.postureSlider.value = 0;
        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        canvasAnim.Play("FadeIn");
        resetRoutine = StartCoroutine(ReturnFighters());
    }

    IEnumerator ReturnFighters()
    {
        Vector3 playerFrom = player.transform.position;
        Vector3 enemyFrom = enemy.transform.position;

        float elapsed = 0f;

        while (elapsed < resetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / resetDuration);

            Vector3 nextPlayerPosition =
                Vector3.Lerp(playerFrom, playerStartPosition, t);

            // Player has a CharacterController: move through it.
            player.controller.Move(nextPlayerPosition - player.transform.position);
            PlayerStateMachine.instance.animator.Play("Walk_Backward");

            enemy.transform.position =
                Vector3.Lerp(enemyFrom, enemyStartPosition, t);
            enemy.anim.Play("Walk_Backward");
            canvasAnim.Play("FadeOut");
            yield return null;
        }

        // Ensure both finish exactly at their starting locations.
        player.controller.Move(playerStartPosition - player.transform.position);
        enemy.transform.position = enemyStartPosition;

        resetRoutine = null;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerStartPosition = player.transform.position;
        enemyStartPosition = enemy.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
