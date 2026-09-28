using System.Collections;
using UnityEngine;

public class Recovery_Interaction_Type_B : MonoBehaviour
{
    [SerializeField]
    private PlayerMovement player;
    public bool lost;
    [SerializeField]
    public bool ending;
    [SerializeField]
    private GameObject meter;
    [SerializeField]
    private TimedPointer timedPointer;
    private bool canInteract;
    [SerializeField]
    private GameObject timedPointerObj;

    private bool meterActive = false;
    [SerializeField]
    private HealthAndMotivation hnM;

    [SerializeField]
    private float winHealth;
    [SerializeField]
    private float winMotivation;
    [SerializeField]
    private float loseHealth;
    [SerializeField]
    private float loseMotivation;
    public string relaventAnimation;
    public string actionAnimation;
    public Animator playerAnim; 


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            canInteract = true;
            player = other.gameObject.GetComponent<PlayerMovement>();
            playerAnim = player.gameObject.GetComponentInChildren<Animator>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            canInteract = false;
            player = null;
            playerAnim = null;
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        lost = false;
        meter.gameObject.SetActive(false);
        ending = false;
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

        if (timedPointer.won || timedPointer.injured)
        {
            ending = true;
            if (ending)
            {
                EndMinigame();
            }
        }

    }

    private void BeginInteraction()
    {
        meterActive = true;
        meter.gameObject.SetActive(true);
        StartCoroutine(Interaction());
    }

    public void EndMinigame()
    {
        StopAllCoroutines();
        StartCoroutine(EndInteraction());
        ending = false;
    }
    IEnumerator Interaction()
    {
        yield return new WaitForSeconds(20);
        if (timedPointer.currentSuccessNumber != timedPointer.winCount)
        {
            lost = true;
        }
        StartCoroutine(EndInteraction());
    }

    IEnumerator EndInteraction()
    {
        if (timedPointer.won)
        {
            Debug.Log("Won");
            hnM.ModifyHealth(winHealth);
            hnM.ModifyMotivation(winMotivation);
        }
        if (lost)
        {
            Debug.Log("Lost");
            hnM.ModifyHealth(loseHealth);
            hnM.ModifyMotivation(loseMotivation);
        }
        if (timedPointer.injured)
        {
            Debug.Log("Injured");
        }
        timedPointer.won = false;
        timedPointer.injured = false;
        timedPointer.currentFailNumber = 0;
        timedPointer.currentSuccessNumber = 0;
        yield return new WaitForSeconds(0.01f);
        lost = false;
        meter.gameObject.SetActive(false);
        player.interacting = false;
        meterActive = false;
    }
}
