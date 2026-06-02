using UnityEngine;
using UnityEngine.VFX;

public class TriggerZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            Transform enemy = transform.parent;
            enemy.position = new Vector3(other.transform.position.x, enemy.position.y, enemy.position.z);
        }
    }
}
