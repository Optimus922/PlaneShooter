using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 爆炸特效管理器(阶段7)。单例 + 对象池,提供 SpawnAt(pos) 在指定位置播放一次爆炸。
///
/// 设计要点:
/// - 与子弹/敌机同样的 ObjectPool 复用,避免频繁 Instantiate/Destroy。
/// - 帧序列、播放速度、缩放、排序在本管理器集中配置,Explosion 实例只负责播放。
/// - Enemy.Die 等通过 ExplosionManager.Instance.SpawnAt(transform.position) 触发。
/// </summary>
public class ExplosionManager : MonoBehaviour
{
    public static ExplosionManager Instance { get; private set; }

    [Header("爆炸帧")]
    [Tooltip("按顺序拖入爆炸序列帧(explosion_0..7)。")]
    [SerializeField] private Sprite[] frames;

    [Header("播放设置")]
    [Tooltip("播放帧率(帧/秒)。8 帧约 0.5 秒可设 16。")]
    [SerializeField] private float fps = 16f;

    [Tooltip("爆炸 sprite 的缩放(相对 1 单位)。")]
    [SerializeField] private float scale = 1f;

    [Tooltip("排序层级。应高于敌机/玩家,让爆炸盖在上面。")]
    [SerializeField] private int sortingOrder = 5;

    [Header("对象池")]
    [SerializeField] private int defaultPoolSize = 10;
    [SerializeField] private int maxPoolSize = 40;

    private IObjectPool<Explosion> pool;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        pool = new ObjectPool<Explosion>(
            createFunc: CreateExplosion,
            actionOnGet: e => e.gameObject.SetActive(true),
            actionOnRelease: e => e.gameObject.SetActive(false),
            actionOnDestroy: e => Destroy(e.gameObject),
            collectionCheck: false,
            defaultCapacity: defaultPoolSize,
            maxSize: maxPoolSize
        );
    }

    private Explosion CreateExplosion()
    {
        GameObject go = new GameObject("Explosion");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<SpriteRenderer>();
        Explosion e = go.AddComponent<Explosion>();
        e.Init(frames, fps, sortingOrder);
        e.SetPool(pool);
        return e;
    }

    /// <summary>在世界坐标 pos 播放一次爆炸。</summary>
    public void SpawnAt(Vector3 pos)
    {
        if (frames == null || frames.Length == 0) return;
        Explosion e = pool.Get();
        e.transform.position = pos;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
