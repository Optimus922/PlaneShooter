using UnityEngine;

/// <summary>
/// 让一个 SpriteRenderer 的颜色在两色之间周期性脉动(闪烁),提高可见度。
///
/// 用途:boss 的敌方子弹原本暗红色不易看清,挂上本组件让它在 暗色↔亮色 间闪,更醒目。
///
/// 设计要点:
/// - 用 unscaledDeltaTime 驱动正弦脉动,即使将来某处改了 timeScale 也照闪。
/// - 只改 SpriteRenderer.color;OnEnable 重置相位(对象池复用时每次取出都从同一相位起),
///   保证每颗子弹闪烁一致、且复用不残留上一颗的颜色。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlash : MonoBehaviour
{
    [Tooltip("每秒闪烁次数(脉动频率)。")]
    [SerializeField] private float flashesPerSecond = 4f;

    [Tooltip("亮色(脉动峰值,通常偏白/高亮)。")]
    [SerializeField] private Color brightColor = new Color(1f, 0.95f, 0.6f, 1f);

    [Tooltip("暗色(脉动谷值,子弹本体色)。")]
    [SerializeField] private Color dimColor = new Color(1f, 0.35f, 0.35f, 1f);

    private SpriteRenderer sr;
    private float timer;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        timer = 0f;
        if (sr != null) sr.color = brightColor;   // 从亮色起步,出膛即醒目
    }

    private void Update()
    {
        if (sr == null) return;
        timer += Time.unscaledDeltaTime * flashesPerSecond;
        // cos 从 1 起步:t=0 时为 brightColor,在 bright↔dim 间平滑往返
        float w = (Mathf.Cos(timer * Mathf.PI * 2f) + 1f) * 0.5f;
        sr.color = Color.Lerp(dimColor, brightColor, w);
    }
}
