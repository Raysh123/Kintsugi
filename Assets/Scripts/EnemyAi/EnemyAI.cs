using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public Transform player;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //if( Vector3.Distance(transform.position, player.position) < 1.5f)
        //{
        //    transform.position = new Vector3(Random.Range(1,3), 1, 0.6f);
        //}
        //else
        //{

        //}
        Vector3 RandomX = new Vector3(Random.Range(1, 3), 1, 0.6f);
        if(RandomX.x == 1)
        {
            transform.position = new Vector3(RandomX.x, 1, 0.6f);
        }
        else
        {
            transform.position = Vector3.zero;
        }
    }
}
