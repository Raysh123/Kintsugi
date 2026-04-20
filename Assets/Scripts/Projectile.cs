using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Rigidbody rb;
    public float projectileSpeed = 10f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.AddForce(-transform.right * projectileSpeed, ForceMode.Impulse);
    }

    // Update is called once per frame
    void Update()
    {
        Destroy(gameObject, 3f);
    }
}
