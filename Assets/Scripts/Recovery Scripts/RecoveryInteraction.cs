using System.Collections;
using UnityEngine;

public class RecoveryInteraction : MonoBehaviour
{
    [SerializeField]
    private PlayerMovement player;

    [SerializeField]
    private GameObject meter;
    [SerializeField]
    private Pointer pointer;
    private bool canInteract;
    [SerializeField]
    private GameObject pointerObj;
    private Rigidbody pointerRB;

    private bool meterActive = false;
    [SerializeField]
    private HealthAndMotivation hnM;

    [SerializeField]
    private float winHealth;
    [SerializeField]
    private float winMotivation;
    public string relaventAnimation;


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            canInteract = true;
            player = other.gameObject.GetComponent<PlayerMovement>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            canInteract = false;
            player = null;
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pointerRB = pointerObj.GetComponent<Rigidbody>();
        meter.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (canInteract)
        {
            if (player.interacting && !meterActive)
            {
                BeginInteraction();
            }
        }
    }

    private void BeginInteraction()
    {
        meterActive = true;
        meter.gameObject.SetActive(true);
        StartCoroutine(Interaction());
    }

    IEnumerator Interaction()
    {
        yield return new WaitForSeconds(10);
        StartCoroutine(EndInteraction());
    }

    IEnumerator EndInteraction()
    {
        if (pointer.won)
        {
            Debug.Log("Won");
            hnM.ModifyHealth(winHealth);
            hnM.ModifyMotivation(winMotivation);
        }
        if (pointer.lost)
        {
            Debug.Log("Lost");
        }
        if (pointer.injured)
        {
            Debug.Log("Injured");
        }
        yield return new WaitForSeconds(0.01f) ;
        meter.gameObject.SetActive(false);
        player.interacting = false;
        meterActive = false;
    }
}
