using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy", menuName = "Enemies/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string EnemyName;
    public Sprite Icon;

    [Header("Stats")]
    public float MaxHealth = 5f;
    public float MoveSpeed = 2.2f;

    [Header("Detection")]
    public float DetectionRange = 6f;
    public float AttackRange = 1.15f;

    [Header("Idle")]
    public float IdleWanderRadius = 1.2f;
    public float IdleWanderInterval = 1.4f;

    [Header("Attack")]
    public float AttackDamage = 1f;
    public float AttackCooldown = 1f;

    [Header("Death")]
    public float DestroyDelay = 0.8f;
}
