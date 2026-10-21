using UnityEngine;

public class ComboTriggers : MonoBehaviour
{
    private EnemyStateMachine enemy;
    private void Awake()
    {
        enemy = GetComponentInParent<EnemyStateMachine>();
    }
    public void DealDamage(int attackType)
    {
        enemy.OpenEnemyHitWindow(attackType);
    }
    public void CancelDamage()
    {
        enemy.CloseEnemyHitWindow();
    }
    public void Move()
    {
        enemy.MoveBetCombo();
    }
    // Animation Event "AnticipationHold": the float is the hold length in seconds (0 = the Inspector value on the enemy)
    public void AnticipationHold(float seconds)
    {
        enemy.AnticipationHold(seconds);
    }
}