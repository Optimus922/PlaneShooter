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
    [Tooltip("上下起伏的幅度(世界单位,0=不起伏)。")]
    [SerializeField] private float bobAmplitude = 0.8f;
    [Tooltip("上下起伏的频率(每秒周期数)。")]
    [SerializeField] private float bobFrequency = 0.5f;

    [Header("开火")]
    [Tooltip("敌方子弹预制体。")]
    [SerializeField] private EnemyBullet enemyBulletPrefab;
    [Tooltip("每个炮台两次开火的间隔(秒)。")]
    [SerializeField] private float fireInterval = 1.6f;
    [Tooltip("炮口相对炮台中心向下的偏移(子弹生成点)。")]
    [SerializeField] private float muzzleOffset = 0.6f;
    [Tooltip("主炮(parts[0])扇形齐射的子弹数(奇数最佳,1=单发)。")]
    [SerializeField] private int mainGunFanCount = 5;
    [Tooltip("主炮扇形齐射的总张角(度)。")]
    [SerializeField] private float mainGunFanAngle = 50f;
    [Tooltip("主炮循环里扇形齐射前的单发次数。")]
    [SerializeField] private int mainGunSingleShots = 3;
    [Tooltip("主炮单发之间的间隔(秒)。")]
    [SerializeField] private float mainGunSingleGap = 0.35f;
    [Tooltip("主炮循环里每个阶段之间的停顿(秒)。")]
    [SerializeField] private float mainGunPause = 1f;

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
    private float bobTime;

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
            if (Mathf.Approximately(pos.y, hoverY))
            {
                entered = true;
                StartCoroutine(MainGunRoutine());   // 进场完成后启动主炮循环节奏
            }
            return;
        }

        Wander();
        SubGunLoop();
    }

    private void Wander()
    {
        Vector3 pos = transform.position;

        // 横向往返
        pos.x += wanderDir * wanderSpeed * Time.deltaTime;
        if (pos.x <= leftLimit) { pos.x = leftLimit; wanderDir = 1; }
        else if (pos.x >= rightLimit) { pos.x = rightLimit; wanderDir = -1; }

        // 上下起伏:绕 hoverY 做正弦摆动,叠加横移走出波浪轨迹
        bobTime += Time.deltaTime;
        pos.y = hoverY + Mathf.Sin(bobTime * bobFrequency * 2f * Mathf.PI) * bobAmplitude;

        transform.position = pos;
    }

    /// <summary>副炮(parts[1..])定时单发瞄准玩家。主炮的开火由 MainGunRoutine 协程单独控制。</summary>
    private void SubGunLoop()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;
        fireTimer = fireInterval;

        for (int i = 1; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p == null || p.IsDead) continue;
            FireFan(p.transform.position, 1, 0f);
        }
    }

    /// <summary>主炮开火节奏:N 发单发 → 停顿 → 1 次扇形齐射 → 停顿 → 循环。</summary>
    private System.Collections.IEnumerator MainGunRoutine()
    {
        while (!defeated)
        {
            BossPart main = (parts != null && parts.Length > 0) ? parts[0] : null;

            // 主炮已被击破则停止该循环(副炮仍照常)
            if (main == null || main.IsDead) yield break;

            // 阶段一:N 发单发瞄准
            for (int s = 0; s < Mathf.Max(1, mainGunSingleShots); s++)
            {
                if (defeated || main.IsDead) yield break;
                FireFan(main.transform.position, 1, 0f);
                yield return new WaitForSeconds(mainGunSingleGap);
            }

            // 停顿
            yield return new WaitForSeconds(mainGunPause);
            if (defeated || main.IsDead) yield break;

            // 阶段二:1 次扇形齐射
            FireFan(main.transform.position, Mathf.Max(1, mainGunFanCount), mainGunFanAngle);

            // 停顿后再循环
            yield return new WaitForSeconds(mainGunPause);
        }
    }

    /// <summary>从炮口朝玩家方向发射 count 发子弹,在 spreadAngle 总张角内均匀分布。</summary>
    private void FireFan(Vector3 turretPos, int count, float spreadAngle)
    {
        if (enemyBulletPrefab == null) return;

        Vector3 muzzle = turretPos + Vector3.down * muzzleOffset;
        Vector2 aim = Vector2.down;
        if (player != null)
            aim = ((Vector2)(player.position - muzzle)).normalized;

        // 以 aim 为中心,在 [-spread/2, +spread/2] 内均匀取 count 个方向
        for (int k = 0; k < count; k++)
        {
            float t = (count == 1) ? 0.5f : (float)k / (count - 1);
            float ang = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t);
            Vector2 dir = Rotate(aim, ang);

            EnemyBullet b = bulletPool.Get();
            b.transform.position = muzzle;
            b.transform.rotation = Quaternion.identity;
            b.Launch(dir);
        }
    }

    /// <summary>把二维向量旋转 degrees 度。</summary>
    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
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
