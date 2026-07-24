using System.Collections.Generic;
using UnityEngine;

public class DmgDetection : MonoBehaviour
{
    public Dictionary<string, int> punchOrKick;
    public Animator anim;
    public Dictionary<string, string> detect;
    [SerializeField] private MatchTimer matchTimer;
    [SerializeField] private PlayerStateMachine playerStateMachine;
    [SerializeField] private EnemyStateMachine enemyStateMachine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        punchOrKick = new Dictionary<string, int>();
        punchOrKick.Add("Punch", 1);
        punchOrKick.Add("Kick", 2);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(
            $"Trigger: {gameObject.tag} hit {other.tag} | " +
            $"Player: {playerStateMachine.currentState} | " +
            $"Enemy: {enemyStateMachine.currentState}"
        );
        if (other.transform.tag == "Body" && gameObject.tag == "Punch" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            anim.Play("Hurt_Balls");
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        }
        else if(other.transform.tag == "Body" && gameObject.tag == "Kick" && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Attack)
        {
            anim.Play("Hurt_Balls");
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        }
        else if(other.transform.tag == "pBody" && gameObject.tag == "ePunch" && enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
        {
            if(playerStateMachine!=null && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Block)
            {
                Debug.Log("Blocked");
                return;
            }
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        }
        else if( other.transform.tag == "pBody" && gameObject.tag == "ekick" && enemyStateMachine.currentState == EnemyStateMachine.EnemyState.Attack)
        {
            if (playerStateMachine != null && playerStateMachine.currentState == PlayerStateMachine.PlayerStates.Block)
            {
                Debug.Log("Blocked");
                return;
            }
            matchTimer.RegisterHit();
            GameManager.instance.ResetPlayerPos();
        } 
    }
}
