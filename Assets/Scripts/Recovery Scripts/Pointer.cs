using UnityEngine;

public class Pointer : MonoBehaviour
{
    private Rigidbody pointerRb;
    [SerializeField]
    private float jumpForce;
    public bool injured;
    public bool won;
    public bool lost;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Injury"))
        {
            injured = true;
        }
        if(other.gameObject.CompareTag("Winstate"))
        {
            won = true;
        }
        if(other.gameObject.CompareTag("LoseState"))
        {
            lost = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Injury"))
        {
            injured = false;
        }
        if (other.gameObject.CompareTag("Winstate"))
        {
            won = false;
        }
        if (other.gameObject.CompareTag("LoseState"))
        {
            lost = false;
        }
    }
    void Start()
    {
        pointerRb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        Jump();
    }

    private void Jump()
    {
        if(Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
        {
            pointerRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
}
