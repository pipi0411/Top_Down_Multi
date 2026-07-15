using UnityEngine;

public class EnemyIdleState : IEnemyState
{
    readonly EnemyStateMachine enemy;
    Vector2 wanderDirection;
    float nextWanderTime;

    public EnemyIdleState(EnemyStateMachine enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        enemy.StopMoving();
        PickWanderDirection();
    }

    public void Tick()
    {
        if (enemy.TryFindTarget())
        {
            enemy.ChaseTarget();
            return;
        }

        if (Time.time >= nextWanderTime)
            PickWanderDirection();
    }

    public void FixedTick()
    {
        enemy.MoveInDirection(wanderDirection);
    }

    public void Exit()
    {
        enemy.StopMoving();
    }

    void PickWanderDirection()
    {
        wanderDirection = Random.insideUnitCircle.normalized * Random.Range(0f, enemy.IdleWanderRadius);
        nextWanderTime = Time.time + enemy.IdleWanderInterval;
    }
}
