public class EnemyAttackState : IEnemyState
{
    readonly EnemyStateMachine enemy;

    public EnemyAttackState(EnemyStateMachine enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        enemy.StopMoving();
    }

    public void Tick()
    {
        if (!enemy.TryFindTarget())
        {
            enemy.GoIdle();
            return;
        }

        if (!enemy.TargetInAttackRange())
        {
            enemy.ChaseTarget();
            return;
        }

        enemy.DealAttackDamage();
    }

    public void FixedTick() { }
    public void Exit() { }
}
