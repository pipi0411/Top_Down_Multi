public class EnemyDeadState : IEnemyState
{
    readonly EnemyStateMachine enemy;

    public EnemyDeadState(EnemyStateMachine enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        enemy.StopMoving();
        enemy.SetTrigger(enemy.DeathTrigger);
    }

    public void Tick() { }
    public void FixedTick() { }
    public void Exit() { }
}
