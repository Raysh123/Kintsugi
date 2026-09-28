using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public PlayerController player;
    public EnemyStateMachine enemy;
    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
    }
    public void ResetPlayerPos()
    {
        player.controller.enabled = false;
        player.transform.position = player.PlayerInitPos;
        player.controller.enabled = true;

        enemy.transform.position = enemy.enemyInitPos;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
