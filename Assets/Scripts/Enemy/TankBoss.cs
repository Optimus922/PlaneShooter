using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 第一关 boss:坦克本体(阶段9)。
///
/// 行为:
/// - 进场:从屏幕上方移动到上半屏的徘徊高度。
/// - 徘徊:在上半屏左右往返移动(撞到左右边界折返),直到被击杀。
/// - 开火:每个存活炮台按间隔朝玩家方向发射 EnemyBullet。
/// - 受击:本体不可受击,只有挂在 parts 里的 BossPart(主炮/副炮)能被打。
///   所有部位击破 → boss 死亡(爆炸/计分),回调 onDefeated 通知 spawner 关卡结束。
/// - 撞玩家不扣血(本体无接触伤害;按用户设定)。
///
/// 设计:炮台部位在 Inspector 里作为子物体引用进 parts[];boss 持有 EnemyBullet 对象池。
/// </summary>
public class TankBoss : MonoBehaviour
{
    [Header("部位(主炮+副炮)")]
    [Tooltip("所有可受击炮台。全部击破即 boss 被击杀。")]
    [SerializeField] private BossPart[] parts;

    [Header("进场")]
    [Tooltip("进场后徘徊的中心高度(世界 Y)。留 9999 表示自动取上半屏。")]
    [SerializeField] private float hoverY = 9999f;
    [Tooltip("进场移动速度。")]
    [SerializeField] private float enterSpeed = 3f;

    [Header("徘徊")]
    [Tooltip("左右徘徊速度。")]
    [SerializeField] private float wanderSpeed = 2.5f;
    [Tooltip("左右两侧留出的边距,避免 boss 出界。")]
    [SerializeField] private float horizontalPadding = 1.5f;

    [Header("开火")]
    [Tooltip("敌方子弹预制体。")]
    [SerializeField] private EnemyBullet enemyBulletPrefab;
    [Tooltip("每个炮台两次开火的间隔(秒)。")]
    [SerializeField] private float fireInterval = 1.6f;
    [Tooltip("炮口相对炮台中心向下的偏移(子弹生成点)。")]
    [SerializeField] private float muzzleOffset = 0.6f;

    [Header("击杀奖励")]
    [Tooltip("boss 被击杀给玩家加的分数。")]
    [SerializeField] private int scoreValue = 500;

    [Header("对象池")]
    [SerializeField] private int bulletPoolDefault = 10;
    [SerializeField] private int bulletPoolMax = 40;

    private Camera mainCamera;
    private int aliveParts;
    private bool entered;
    private int wanderDir = 1;
    private float fireTimer;
    private bool defeated;

    private IObjectPool<EnemyBullet> bulletPool;
    private System.Action onDefeated;

    private float leftLimit, rightLimit;
    private Transform player;

    private void Awake()
    {
        mainCamera = Camera.main;
        BuildBulletPool();
    }

    private void Start()
    {
        // 初始化部位回调与存活计数
        aliveParts = 0;
        foreach (var p in parts)
        {
            if (p == null) continue;
            p.Init(OnPartDestroyed);
            aliveParts++;
        }

        if (hoverY > 9000f)
        {
            // 自动:上半屏约 60% 高度
            float camHalfH = mainCamera.orthographicSize;
            hoverY = mainCamera.transform.position.y + camHalfH * 0.55f;
        }

        float camHalfW = mainCamera.orthographicSize * mainCamera.aspect;
        float camX = mainCamera.transform.position.x;
        leftLimit = camX - camHalfW + horizontalPadding;
        rightLimit = camX + camHalfW - horizontalPadding;

        var ph = FindObjectOfType<PlayerHealth>();
        if (ph != null) player = ph.transform;

        fireTimer = fireInterval;
    }

    /// <summary>由 spawner 注入:boss 被击杀时回调。</summary>
    public void Init(System.Action defeatedCallback)
    {
        onDefeated = defeatedCallback;
    }

    private void Update()
    {
        if (defeated) return;

        if (!entered)
        {
            // 进场:向下移动到 hoverY
            Vector3 pos = transform.position;
            pos.y = Mathf.MoveTowards(pos.y, hoverY, enterSpeed * Time.deltaTime);
            transform.position = pos;
            if (Mathf.Approximately(pos.y, hoverY)) entered = true;
            return;
        }

        Wander();
        FireLoop();
    }

    private void Wander()
    {
        Vector3 pos = transform.position;
        pos.x += wanderDir * wanderSpeed * Time.deltaTime;

        if (pos.x <= leftLimit) { pos.x = leftLimit; wanderDir = 1; }
        else if (pos.x >= rightLimit) { pos.x = rightLimit; wanderDir = -1; }

        transform.position = pos;
    }

    private void FireLoop()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;
        fireTimer = fireInterval;

        // 每个存活炮台各发一发,朝玩家方向(玩家不存在则向下)
        foreach (var p in parts)
        {
            if (p == null || p.IsDead) continue;
            FireFrom(p.transform.position);
        }
    }

    private void FireFrom(Vector3 turretPos)
    {
        if (enemyBulletPrefab == null) return;

        Vector3 muzzle = turretPos + Vector3.down * muzzleOffset;
        Vector2 dir = Vector2.down;
        if (player != null)
            dir = ((Vector2)(player.position - muzzle)).normalized;

        EnemyBullet b = bulletPool.Get();
        b.transform.position = muzzle;
        b.transform.rotation = Quaternion.identity;
        b.Launch(dir);
    }

    private void OnPartDestroyed(BossPart part)
    {
        aliveParts = Mathf.Max(0, aliveParts - 1);
        if (aliveParts <= 0)
            Defeat();
    }

    private void Defeat()
    {
        if (defeated) return;
        defeated = true;

        // 计分
        if (GameManager.Instance != null) GameManager.Instance.AddScore(scoreValue);

        // 多处爆炸 + 音效
        if (ExplosionManager.Instance != null)
        {
            ExplosionManager.Instance.SpawnAt(transform.position);
            foreach (var p in parts)
                if (p != null) ExplosionManager.Instance.SpawnAt(p.transform.position);
        }
        if (SfxManager.Instance != null) SfxManager.Instance.PlayExplosion();

        onDefeated?.Invoke();
        Destroy(gameObject);
    }

    // ---------- 敌方子弹对象池 ----------
    private void BuildBulletPool()
    {
        bulletPool = new ObjectPool<EnemyBullet>(
            createFunc: () =>
            {
                EnemyBullet b = Instantiate(enemyBulletPrefab);
                b.SetPool(bulletPool);
                return b;
            },
            actionOnGet: b => b.gameObject.SetActive(true),
            actionOnRelease: b => b.gameObject.SetActive(false),
            actionOnDestroy: b => Destroy(b.gameObject),
            collectionCheck: false,
            defaultCapacity: bulletPoolDefault,
            maxSize: bulletPoolMax
        );
    }
}
