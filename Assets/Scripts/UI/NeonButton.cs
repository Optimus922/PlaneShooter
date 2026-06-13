using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 给 UGUI Button 加"科幻霓虹按钮"的外观与按压反馈。
///
/// 设计要点:
/// - 外观:运行时给按钮的背景 Image 加一层霓虹 Outline(描边发光),让它一眼像可点的按钮。
/// - 反馈:实现 IPointerDown/Up/Exit,按下时整体缩放变小 + 背景提亮,抬起/移出恢复。
///   用"瞬时切换"而非协程动画 —— 结算界面 GameOver 时 Time.timeScale=0,
///   基于 deltaTime 的动画会停,瞬时切换则始终可见,保证移动端点下去一定有反馈。
/// - 与 NeonText 同思路:挂上组件即可,无需手写 effect / 动画 YAML。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class NeonButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("背景描边发光")]
    [Tooltip("要加霓虹描边的背景 Image(留空则取本物体上的 Image)。")]
    [SerializeField] private Image targetImage;
    [Tooltip("霓虹描边颜色。")]
    [SerializeField] private Color outlineColor = new Color(0f, 0.85f, 1f, 1f);
    [Tooltip("描边扩散距离(像素)。")]
    [SerializeField] private float outlineDistance = 3f;

    [Header("按压反馈")]
    [Tooltip("按下时的缩放比例。")]
    [SerializeField] private float pressedScale = 0.92f;
    [Tooltip("按下时背景颜色相乘的提亮系数(>1 提亮)。")]
    [SerializeField] private float pressedBrighten = 1.4f;

    private RectTransform rt;
    private Vector3 normalScale;
    private Color normalColor;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        normalScale = rt.localScale;

        if (targetImage == null) targetImage = GetComponent<Image>();
        if (targetImage != null) normalColor = targetImage.color;

        // 加霓虹描边(若已有 Outline 则不重复加)
        if (targetImage != null && GetComponent<Outline>() == null)
        {
            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(outlineDistance, outlineDistance);
            outline.useGraphicAlpha = true;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        rt.localScale = normalScale * pressedScale;
        if (targetImage != null)
        {
            Color c = normalColor * pressedBrighten;
            c.a = normalColor.a;   // 保持原透明度
            targetImage.color = c;
        }
    }

    public void OnPointerUp(PointerEventData eventData) => Restore();
    public void OnPointerExit(PointerEventData eventData) => Restore();

    private void Restore()
    {
        rt.localScale = normalScale;
        if (targetImage != null) targetImage.color = normalColor;
    }
}
