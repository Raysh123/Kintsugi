using System.Collections.Generic;
using UnityEngine;

public class DmgDetection : MonoBehaviour
{
    public Dictionary<string, int> punchOrKick;
    public Animator anim;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        punchOrKick = new Dictionary<string, int>();
        punchOrKick.Add("Punch", 1);
        punchOrKick.Add("Kick", 2);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "Body" && gameObject.tag == "Punch")
        {
            anim.Play("Hurt_Balls");
            GameManager.instance.ResetPlayerPos();
        }
        else if(other.transform.tag == "Body" && gameObject.tag == "Kick")
        {
            anim.Play("Hurt_Balls");
            GameManager.instance.ResetPlayerPos();
        }
    }
}
