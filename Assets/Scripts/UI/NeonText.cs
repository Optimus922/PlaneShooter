using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 给 Legacy UGUI Text 套上"科幻霓虹/街机"风格:霓虹发光描边 + 投影 + 加粗高亮色。
///
/// 设计要点(与 HealthBar 同思路):
/// - 运行时在 Awake 给同物体的 Text 自动加上若干层 Outline(模拟外发光)+ 一层 Shadow(投影),
///   场景里只需挂这个组件、调几个颜色参数即可,不用手写多个 effect 的 YAML。
/// - 不接管文本内容 —— 分数/结算文本仍由 GameHUD 动态赋值,本组件只负责"长相"。
/// - glowLayers 用多层 Outline、距离递增、alpha 递减,叠出霓虹外发光的层次感。
/// - 用 [ExecuteAlways] 也能在编辑器预览,但为避免编辑器里反复堆叠 effect,仅运行时构建。
/// </summary>
[RequireComponent(typeof(Text))]
public class NeonText : MonoBehaviour
{
    [Header("主体")]
    [Tooltip("文字主色(高亮霓虹色)。")]
    [SerializeField] private Color textColor = new Color(0f, 1f, 0.85f, 1f); // 青色霓虹
    [Tooltip("是否强制加粗。")]
    [SerializeField] private bool bold = true;

    [Header("霓虹发光")]
    [Tooltip("发光颜色(通常与主色同色系、偏暗)。")]
    [SerializeField] private Color glowColor = new Color(0f, 0.7f, 1f, 1f);
    [Tooltip("发光层数:越多越柔和饱满(性能开销随之上升,建议 2~3)。")]
    [SerializeField] private int glowLayers = 3;
    [Tooltip("最外层发光的扩散距离(像素)。")]
    [SerializeField] private float glowDistance = 4f;

    [Header("投影")]
    [Tooltip("是否加暗色投影,增强浮起感。")]
    [SerializeField] private bool dropShadow = true;
    [Tooltip("投影颜色。")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.75f);
    [Tooltip("投影偏移(像素,右下为正)。")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(3f, -3f);

    private void Awake()
    {
        Apply();
    }

    /// <summary>把霓虹样式应用到本物体的 Text(运行时构建 effect)。</summary>
    public void Apply()
    {
        var text = GetComponent<Text>();
        if (text == null) return;

        text.color = textColor;
        if (bold)
            text.fontStyle = (text.fontStyle == FontStyle.Italic)
                ? FontStyle.BoldAndItalic : FontStyle.Bold;

        // 投影先加(在组件顺序里靠前 = 先绘制,处于发光下层)
        if (dropShadow)
        {
            var shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = shadowOffset;
            shadow.useGraphicAlpha = true;
        }

        // 多层 Outline 叠出外发光:外层距离大、alpha 小
        int layers = Mathf.Max(1, glowLayers);
        for (int i = 0; i < layers; i++)
        {
            float t = (i + 1f) / layers;               // 0..1,越往外 t 越大
            float dist = glowDistance * t;
            var outline = gameObject.AddComponent<Outline>();
            Color c = glowColor;
            c.a = glowColor.a * (1f - 0.55f * t);       // 外层更淡
            outline.effectColor = c;
            outline.effectDistance = new Vector2(dist, dist);
            outline.useGraphicAlpha = true;
        }
    }
}
