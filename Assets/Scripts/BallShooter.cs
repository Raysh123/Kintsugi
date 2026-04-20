using System.Threading;
using UnityEngine;

public class BallShooter : MonoBehaviour
{
    public float currentTime;
    public float cooldown = 3f;
    public GameObject ball;
    public GameObject ball2;
    public Transform[] spawnPoints;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        currentTime += Time.deltaTime;
        if(currentTime >= cooldown)
        {
            int index = Random.Range(0, spawnPoints.Length);
            Transform chosenSpawn = spawnPoints[index];
            if (chosenSpawn == spawnPoints[0])
            {
                Instantiate(ball, chosenSpawn.position, Quaternion.identity);
                currentTime = 0;
            }
            else
            {
                Instantiate(ball2, chosenSpawn.position, Quaternion.identity);
                currentTime = 0;
            }

        }
    }
}
