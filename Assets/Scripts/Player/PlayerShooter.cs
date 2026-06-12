using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 玩家射击：移动端自动持续开火。按固定射速从飞机头部发射子弹。
///
/// 设计要点：
/// - 移动端无法让玩家一边拖动一边按键，所以采用「自动开火」：游戏中持续按 fireRate 间隔发射。
/// - 内置对象池（Unity 2022 自带 ObjectPool）复用子弹，避免频繁 Instantiate/Destroy 引发 GC 卡顿。
/// - 子弹从 firePoint（机头空物体）位置生成；可设多个发射点实现多管齐射（这里先单管）。
/// </summary>
public class PlayerShooter : MonoBehaviour
{
    [Header("子弹与发射点")]
    [Tooltip("子弹的 Prefab（需挂 Bullet 脚本）。")]
    [SerializeField] private Bullet bulletPrefab;

    [Tooltip("发射位置。一般是飞机头部的一个空子物体；留空则用飞机自身位置。")]
    [SerializeField] private Transform firePoint;

    [Header("射击设置")]
    [Tooltip("每发子弹的间隔（秒）。0.15 约等于每秒 6~7 发。")]
    [SerializeField] private float fireRate = 0.15f;

    [Header("对象池设置")]
    [Tooltip("池初始预热数量。")]
    [SerializeField] private int defaultPoolSize = 20;
    [Tooltip("池的最大容量上限。")]
    [SerializeField] private int maxPoolSize = 100;

    private IObjectPool<Bullet> bulletPool;
    private float fireTimer;

    private void Awake()
    {
        // 创建对象池：传入「如何创建/取出/归还/销毁」四个回调
        bulletPool = new ObjectPool<Bullet>(
            createFunc: CreateBullet,
            actionOnGet: OnGetBullet,
            actionOnRelease: OnReleaseBullet,
            actionOnDestroy: OnDestroyBullet,
            collectionCheck: false,        // 关闭重复归还检查，发布版省性能
            defaultCapacity: defaultPoolSize,
            maxSize: maxPoolSize
        );
    }

    private void Update()
    {
        // 按射速自动开火
        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            Fire();
            fireTimer = fireRate;
        }
    }

    private void Fire()
    {
        // 从池中取一颗子弹（会自动调用 OnGetBullet 摆好位置）
        bulletPool.Get();

        if (SfxManager.Instance != null) SfxManager.Instance.PlayShoot();
    }

    // ----- 对象池四个回调 -----

    private Bullet CreateBullet()
    {
        Bullet b = Instantiate(bulletPrefab);
        b.SetPool(bulletPool);   // 告诉子弹它该还给哪个池
        return b;
    }

    private void OnGetBullet(Bullet b)
    {
        // 摆到发射点位置再激活
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        b.transform.position = spawnPos;
        b.transform.rotation = Quaternion.identity;
        b.gameObject.SetActive(true);
    }

    private void OnReleaseBullet(Bullet b)
    {
        b.gameObject.SetActive(false);  // 归还时隐藏，等待复用
    }

    private void OnDestroyBullet(Bullet b)
    {
        Destroy(b.gameObject);          // 池超出上限时才真正销毁
    }
}
