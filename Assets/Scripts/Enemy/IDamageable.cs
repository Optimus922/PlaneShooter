/// <summary>
/// 可受击物接口(阶段9 boss 引入)。
///
/// 任何能被玩家子弹打到、扣血的东西都实现它:普通敌机 Enemy、boss 的炮台部位 BossPart 等。
/// 这样 Bullet 命中时只需找 IDamageable,不必关心对方具体是什么 —— 解耦命中逻辑与受击对象类型。
/// </summary>
public interface IDamageable
{
    /// <summary>受到伤害。具体扣血/死亡由实现方处理。</summary>
    void TakeDamage(int damage);
}
