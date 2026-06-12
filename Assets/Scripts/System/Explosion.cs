using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 单个爆炸特效:从对象池取出后逐帧播放序列帧,播完回收(不 Destroy)。
///
/// 设计要点:
/// - 与子弹/敌机一致走对象池,移动端性能友好。
/// - 帧序列在 ExplosionManager 上配置并传入;本组件只管播放与回收。
/// - 用无缩放时间也可,但默认用 Time.deltaTime;GameOver 暂停(timeScale=0)时爆炸也会停,符合预期。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Explosion : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite[] frames;
    private float frameInterval;
    private float timer;
    private int index;
    private IObjectPool<Explosion> pool;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>由管理器在生成时调用,注入帧序列与播放速度。</summary>
    public void Init(Sprite[] seq, float fps, int sortingOrder)
    {
        frames = seq;
        frameInterval = fps > 0f ? 1f / fps : 0.05f;
        sr.sortingOrder = sortingOrder;
    }

    public void SetPool(IObjectPool<Explosion> ownerPool) => pool = ownerPool;

    private void OnEnable()
    {
        // 每次取出重置播放进度
        index = 0;
        timer = 0f;
        if (frames != null && frames.Length > 0)
            sr.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            Recycle();
            return;
        }

        timer += Time.deltaTime;
        if (timer >= frameInterval)
        {
            timer -= frameInterval;
            index++;
            if (index >= frames.Length)
            {
                Recycle();
                return;
            }
            sr.sprite = frames[index];
        }
    }

    private void Recycle()
    {
        if (pool != null) pool.Release(this);
        else gameObject.SetActive(false);
    }
}
