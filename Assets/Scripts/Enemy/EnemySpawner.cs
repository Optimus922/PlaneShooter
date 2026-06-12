using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 敌机生成器：用协程定时在屏幕顶部随机 X 位置生成敌机。
///
/// 设计要点：
/// - 这是阶段 3 的「简单随机刷怪」版本，用来跑通敌机生成/移动/回收。
///   阶段 8 会升级为按波次配置的关卡系统。
/// - 敌机走对象池，与子弹同样的复用机制，移动端性能友好。
/// - 在顶部生成，X 取屏幕左右边界之间的随机值（留出边距，避免半架敌机贴边）。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("敌机")]
    [Tooltip("敌机 Prefab（需挂 Enemy 脚本）。")]
    [SerializeField] private Enemy enemyPrefab;

    [Header("生成节奏")]
    [Tooltip("每隔多少秒生成一架敌机。")]
    [SerializeField] private float spawnInterval = 1.2f;

    [Tooltip("敌机生成时距屏幕顶部的额外高度（在画面外一点生成，飞入更自然）。")]
    [SerializeField] private float spawnHeightOffset = 1f;

    [Tooltip("左右两侧留出的边距，避免敌机贴边生成。")]
    [SerializeField] private float horizontalPadding = 0.8f;

    [Header("对象池设置")]
    [SerializeField] private int defaultPoolSize = 10;
    [SerializeField] private int maxPoolSize = 50;

    private IObjectPool<Enemy> enemyPool;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;

        enemyPool = new ObjectPool<Enemy>(
            createFunc: CreateEnemy,
            actionOnGet: OnGetEnemy,
            actionOnRelease: OnReleaseEnemy,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: false,
            defaultCapacity: defaultPoolSize,
            maxSize: maxPoolSize
        );
    }

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    /// <summary>持续按间隔生成敌机的协程。</summary>
    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            enemyPool.Get();   // 取出会自动调用 OnGetEnemy 摆好位置
        }
    }

    /// <summary>计算一个屏幕顶部的随机生成位置。</summary>
    private Vector3 GetRandomTopPosition()
    {
        float camHalfHeight = mainCamera.orthographicSize;
        float camHalfWidth = camHalfHeight * mainCamera.aspect;
        Vector3 camCenter = mainCamera.transform.position;

        float minX = camCenter.x - camHalfWidth + horizontalPadding;
        float maxX = camCenter.x + camHalfWidth - horizontalPadding;
        float x = Random.Range(minX, maxX);
        float y = camCenter.y + camHalfHeight + spawnHeightOffset;

        return new Vector3(x, y, 0f);
    }

    // ----- 对象池四个回调 -----

    private Enemy CreateEnemy()
    {
        Enemy e = Instantiate(enemyPrefab);
        e.SetPool(enemyPool);
        return e;
    }

    private void OnGetEnemy(Enemy e)
    {
        e.transform.position = GetRandomTopPosition();
        e.transform.rotation = Quaternion.identity;
        e.gameObject.SetActive(true);
    }

    private void OnReleaseEnemy(Enemy e)
    {
        e.gameObject.SetActive(false);
    }

    private void OnDestroyEnemy(Enemy e)
    {
        Destroy(e.gameObject);
    }
}
