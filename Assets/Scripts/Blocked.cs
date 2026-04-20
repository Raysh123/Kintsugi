using UnityEngine;

public class Blocked : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField]
    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Punch"))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(transform.forward * 2, ForceMode.Impulse);
            GetComponent<Rigidbody>().useGravity = true;
            Destroy(this.gameObject, 2f);
        }

    }
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
}
