using System.Threading;
using UnityEngine;

public class BallShooter : MonoBehaviour
{
    public float currentTime;
    public float cooldown = 3f;
    public GameObject ball;
    public Transform spawnPoint;
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
            Instantiate(ball, spawnPoint.position, Quaternion.identity);
            Debug.Log("ball");
            currentTime = 0;
        }
    }
}
