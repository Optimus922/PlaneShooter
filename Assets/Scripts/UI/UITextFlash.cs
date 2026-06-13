using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 让一个 UGUI Text 在"显示 / 不显示"之间硬切换,产生闪烁(blink)效果,使横幅更醒目。
///
/// 设计要点:
/// - 方波闪烁:每隔 onDuration 秒可见、offDuration 秒隐藏,交替进行(不是渐变呼吸)。
/// - 通过开关 Text 组件的 enabled 控制可见性 —— 一并影响 NeonText 加的 Outline/Shadow
///   (它们随 Text 的 CanvasRenderer 一起绘制),所以发光描边会和字一起整体显隐。
/// - 用 unscaledDeltaTime 驱动 —— 横幅常出现在关卡切换的停顿时刻,即便 timeScale 变化也照闪。
/// - OnEnable 重置为"可见"起步,保证每次横幅出现先亮;OnDisable 还原 enabled=true,避免复用残留隐藏态。
/// </summary>
[RequireComponent(typeof(Text))]
public class UITextFlash : MonoBehaviour
{
    [Tooltip("每次可见持续的秒数。")]
    [SerializeField] private float onDuration = 0.35f;

    [Tooltip("每次隐藏持续的秒数。")]
    [SerializeField] private float offDuration = 0.2f;

    private Text text;
    private float timer;
    private bool visible;

    private void Awake()
    {
        text = GetComponent<Text>();
    }

    private void OnEnable()
    {
        timer = 0f;
        visible = true;
        if (text != null) text.enabled = true;
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;

        float phase = visible ? onDuration : offDuration;
        if (timer >= phase)
        {
            timer -= phase;
            visible = !visible;
            if (text != null) text.enabled = visible;
        }
    }

    private void OnDisable()
    {
        if (text != null) text.enabled = true;   // 还原,避免下次复用时残留隐藏
    }
}

