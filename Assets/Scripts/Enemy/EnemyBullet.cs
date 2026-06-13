using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 敌方子弹(阶段9 boss 引入)。由 boss 炮台发射,朝设定方向飞行,命中玩家扣血。
///
/// 设计要点:
/// - 走对象池(与玩家子弹同机制)。由发射方调用 Launch 设定方向与速度。
/// - 命中检测 OnTriggerEnter2D 找 PlayerHealth;放在 Enemy 层(与玩家碰撞已启用)。
/// - 出屏回收。任意方向飞行(boss 朝玩家瞄准),故按"离开相机范围+余量"判定回收。
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    [Header("子弹设置")]
    [Tooltip("默认飞行速度(单位/秒)。Launch 可覆盖方向,速度用本值。")]
    [SerializeField] private float speed = 7f;

    [Tooltip("命中玩家造成的伤害。")]
    [SerializeField] private int damage = 1;

    [Tooltip("飞出屏幕多远后回收(额外余量)。")]
    [SerializeField] private float screenMargin = 2f;

    private Vector2 direction = Vector2.down;
    private IObjectPool<EnemyBullet> pool;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    /// <summary>发射:设定飞行方向(会归一化)。速度用 Inspector 的 speed。</summary>
    public void Launch(Vector2 dir)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        if (IsOffScreen())
            ReturnToPool();
    }

    private bool IsOffScreen()
    {
        if (mainCamera == null) return false;
        float halfH = mainCamera.orthographicSize + screenMargin;
        float halfW = halfH * mainCamera.aspect + screenMargin;
        Vector3 c = mainCamera.transform.position;
        Vector3 p = transform.position;
        return p.y < c.y - halfH || p.y > c.y + halfH
            || p.x < c.x - halfW || p.x > c.x + halfW;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(damage);
            ReturnToPool();
        }
    }

    public void SetPool(IObjectPool<EnemyBullet> ownerPool)
    {
        pool = ownerPool;
    }

    public void ReturnToPool()
    {
        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }
}
