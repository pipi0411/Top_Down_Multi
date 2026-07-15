public class EnemyChaseState : IEnemyState
{
    readonly EnemyStateMachine enemy;

    public EnemyChaseState(EnemyStateMachine enemy)
    {
        this.enemy = enemy;
    }

    public void Enter() { }

    public void Tick()
    {
        if (!enemy.TryFindTarget())
        {
            enemy.GoIdle();
            return;
        }

        if (enemy.TargetInAttackRange())
            enemy.AttackTarget();
    }

    public void FixedTick()
    {
        if (enemy.Target != null)
            enemy.MoveTowards(enemy.Target.transform.position);
    }

    public void Exit()
    {
        enemy.StopMoving();
    }
}
