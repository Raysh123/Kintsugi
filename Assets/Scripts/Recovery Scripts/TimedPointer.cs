using UnityEngine;

public class TimedPointer : MonoBehaviour
{
    private Rigidbody pointerRb;
    public bool canHit;
    public bool won;
    public bool injured;
    public int winCount;
    public int loseCount;
    public int currentSuccessNumber;
    public int currentFailNumber;
    [SerializeField]
    private Recovery_Interaction_Type_B recovery;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Winstate"))
        {
            canHit = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Winstate"))
        {
            canHit = false;
        }
    }
    void Start()
    {
        canHit = false;
        won = false;
        injured = false;
        pointerRb = GetComponent<Rigidbody>();
        currentSuccessNumber = 0;
        currentFailNumber = 0;
    }

    // Update is called once per frame
    void Update()
    {
        Tag();
        if(currentSuccessNumber == winCount)
        {
            won = true;
        }
        else if (currentFailNumber == loseCount)
        {
            injured = true;
        }
    }

    public void Tag()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Cancel"))
        {
            
            if (!canHit)
            {
                currentFailNumber += 1;
            }
            else if (canHit)
            {
                currentSuccessNumber += 1;
            }
        }
    }
}
