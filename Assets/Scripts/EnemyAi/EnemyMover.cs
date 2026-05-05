using UnityEngine;

public class EnemyMover : MonoBehaviour
{
    public Transform player;
    private CharacterController controller;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if( Vector3.Distance(transform.position, player.position) < 1.5f)
        {
            transform.position = new Vector3(3, 1, 0.6f);
        }
    }
}
