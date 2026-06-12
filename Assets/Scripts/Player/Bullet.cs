using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 子弹脚本：向上飞行 + 打中敌机造成伤害 + 出屏/命中后回收。
///
/// 设计要点：
/// - 配合对象池：命中或出屏都用 ReturnToPool 回收，不 Destroy。
/// - 命中检测用 OnTriggerEnter2D；配合 Layer 碰撞矩阵，能进来的基本就是敌机。
/// - 一颗子弹命中一次即回收（穿透留作后续道具扩展）。
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("子弹设置")]
    [Tooltip("飞行速度（单位/秒）。正值向上。")]
    [SerializeField] private float speed = 14f;

    [Tooltip("命中敌机造成的伤害。")]
    [SerializeField] private int damage = 1;

    [Tooltip("飞出屏幕多远后回收（额外余量）。")]
    [SerializeField] private float screenMargin = 1.5f;

    private IObjectPool<Bullet> pool;
    private Camera mainCamera;
    private float topLimit;
    private float bottomLimit;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        float camHalfHeight = mainCamera.orthographicSize;
        float camCenterY = mainCamera.transform.position.y;
        topLimit = camCenterY + camHalfHeight + screenMargin;
        bottomLimit = camCenterY - camHalfHeight - screenMargin;
    }

    private void Update()
    {
        transform.position += Vector3.up * speed * Time.deltaTime;

        if (transform.position.y > topLimit || transform.position.y < bottomLimit)
        {
            ReturnToPool();
        }
    }

    /// <summary>命中检测：碰到敌机就造成伤害并回收自己。</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            if (SfxManager.Instance != null) SfxManager.Instance.PlayHit();
            ReturnToPool();
        }
    }

    public void SetPool(IObjectPool<Bullet> ownerPool)
    {
        pool = ownerPool;
    }

    public void ReturnToPool()
    {
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
