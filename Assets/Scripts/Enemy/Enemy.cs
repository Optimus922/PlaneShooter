using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 敌机脚本：向下移动 + 血量/受伤/死亡 + 撞击玩家。
///
/// 设计要点：
/// - 走对象池，不 Destroy 而是 Release 回收。
/// - 持有血量，提供 TakeDamage 供子弹调用；血量归零则死亡（回收 + 后续接特效/计分）。
/// - 每次从池取出时血量会重置（OnEnable），避免复用到残血敌机。
/// - 撞到玩家时通过 OnTriggerEnter2D 检测并通知玩家受伤。
/// </summary>
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("移动")]
    [Tooltip("下降速度（单位/秒）。")]
    [SerializeField] private float speed = 3f;

    [Tooltip("飞出屏幕底部多远后回收（额外余量）。")]
    [SerializeField] private float screenMargin = 1.5f;

    [Header("战斗")]
    [Tooltip("最大血量。被子弹击中按子弹伤害减血。")]
    [SerializeField] private int maxHealth = 3;

    [Tooltip("撞到玩家时对玩家造成的伤害。")]
    [SerializeField] private int contactDamage = 1;

    [Tooltip("被击毁时给玩家加的分数。")]
    [SerializeField] private int scoreValue = 10;

    private int currentHealth;
    private IObjectPool<Enemy> pool;
    private Camera mainCamera;
    private float bottomLimit;

    // 阶段8:被回收时通知生成器(用于波次存活计数)。spawner 注入。
    private System.Action<Enemy> onReturned;
    private bool counted;   // 防止一架敌机被重复计数回收

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        // 每次从池取出时重置血量与回收边界
        currentHealth = maxHealth;
        counted = false;

        float camHalfHeight = mainCamera.orthographicSize;
        float camCenterY = mainCamera.transform.position.y;
        bottomLimit = camCenterY - camHalfHeight - screenMargin;
    }

    private void Update()
    {
        transform.position += Vector3.down * speed * Time.deltaTime;

        if (transform.position.y < bottomLimit)
        {
            ReturnToPool();
        }
    }

    /// <summary>受到伤害。血量归零则死亡。</summary>
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>死亡处理。计分 +(后续阶段)爆炸特效与音效。</summary>
    private void Die()
    {
        // 阶段6:通知 GameManager 加分
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreValue);
        }
        // 阶段7:爆炸视觉特效 + 音效(在回收前用当前位置)
        if (ExplosionManager.Instance != null) ExplosionManager.Instance.SpawnAt(transform.position);
        if (SfxManager.Instance != null) SfxManager.Instance.PlayExplosion();
        ReturnToPool();
    }

    /// <summary>撞到玩家：让玩家受伤，自己也回收（同归于尽式的接触伤害）。</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 通过组件判断对方是不是玩家（配合 Layer 碰撞矩阵，这里能进来的基本就是玩家）
        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(contactDamage);
            ReturnToPool();
        }
    }

    public void SetPool(IObjectPool<Enemy> ownerPool)
    {
        pool = ownerPool;
    }

    /// <summary>阶段8:由 EnemySpawner 注入,敌机离场(击毁/出屏/撞玩家)时回调一次,用于波次存活计数。</summary>
    public void SetOnReturned(System.Action<Enemy> callback)
    {
        onReturned = callback;
    }

    public void ReturnToPool()
    {
        // 先通知生成器(每架敌机一次),再回收
        if (!counted)
        {
            counted = true;
            onReturned?.Invoke(this);
        }

        if (pool != null)
        {
            pool.Release(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
