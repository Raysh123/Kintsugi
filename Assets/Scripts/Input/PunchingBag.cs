using System.ComponentModel;
using UnityEngine;

public class PunchingBag : MonoBehaviour
{
    public Animator animator;
    public Transform opponent;
    [SerializeField]
    private float facingRotationSpeed = 200f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Punch"))
        {
            animator.Play("Punched");
        }

        if(other.CompareTag("Kick"))
        {
            animator.Play("Kicked");
        }
    }

    private void HandleFacing()
    {
        if (opponent == null) return;

        Vector3 dir = opponent.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            facingRotationSpeed * Time.deltaTime
        );
    }
}
