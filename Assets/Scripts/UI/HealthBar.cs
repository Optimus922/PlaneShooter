using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 霓虹风血条 HUD(替代纯数字血量显示)。
///
/// 设计要点:
/// - 订阅 PlayerHealth.OnHealthChanged(current, max),按比例驱动一条可填充的血条;
///   不每帧轮询,数据变才刷新。
/// - 为了把场景里的手工连线降到最低,本脚本在运行时自己创建 Track(底框)和
///   Fill(填充)两个子 Image —— 只需在 Inspector 拖入两张 Sprite 即可。
/// - Fill 用 Image.Type.Filled + Horizontal,fillAmount = current/max 实现左右裁切。
/// - 颜色随血量在 青绿(满)→ 黄(中)→ 红(濒死) 之间渐变,低血量危机感更强。
/// - 所有引用判空保护,PlayerHealth 留空则自动 FindObjectOfType。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HealthBar : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家血量组件。留空则在 Start 自动查找场景中的 PlayerHealth。")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("贴图")]
    [Tooltip("血条底框/轨道贴图(hp_track)。")]
    [SerializeField] private Sprite trackSprite;
    [Tooltip("血条填充贴图(hp_fill,运行时按血量 tint)。")]
    [SerializeField] private Sprite fillSprite;

    [Header("尺寸")]
    [Tooltip("血条整体尺寸(像素)。")]
    [SerializeField] private Vector2 barSize = new Vector2(320f, 60f);
    [Tooltip("填充相对轨道四周的内边距(像素),让边框露出来。")]
    [SerializeField] private float fillPadding = 10f;

    [Header("颜色(按血量比例渐变)")]
    [Tooltip("满血颜色。")]
    [SerializeField] private Color fullColor = new Color(0.1f, 1f, 0.6f, 1f);   // 青绿霓虹
    [Tooltip("半血颜色。")]
    [SerializeField] private Color midColor = new Color(1f, 0.85f, 0.2f, 1f);   // 黄
    [Tooltip("濒死颜色。")]
    [SerializeField] private Color lowColor = new Color(1f, 0.2f, 0.25f, 1f);   // 红

    [Header("动画")]
    [Tooltip("血量变化时填充平滑过渡的速度(0=瞬间)。")]
    [SerializeField] private float lerpSpeed = 8f;

    private Image fillImage;
    private float targetFill = 1f;

    private void Awake()
    {
        BuildVisuals();
    }

    private void Start()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
        else
        {
            Debug.LogWarning("[HealthBar] 场景找不到 PlayerHealth,血条不会更新。");
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    /// <summary>运行时创建 Track + Fill 两个子 Image。</summary>
    private void BuildVisuals()
    {
        RectTransform self = GetComponent<RectTransform>();
        self.sizeDelta = barSize;

        // --- Track(底框)---
        var trackGO = new GameObject("Track", typeof(RectTransform), typeof(Image));
        var trackRT = trackGO.GetComponent<RectTransform>();
        trackRT.SetParent(self, false);
        StretchFull(trackRT, Vector2.zero);
        var trackImg = trackGO.GetComponent<Image>();
        trackImg.sprite = trackSprite;
        trackImg.type = Image.Type.Simple;
        trackImg.raycastTarget = false;
        if (trackSprite == null) trackImg.color = new Color(0.03f, 0.05f, 0.11f, 0.86f);

        // --- Fill(填充)---
        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.SetParent(self, false);
        StretchFull(fillRT, new Vector2(fillPadding, fillPadding));
        fillImage = fillGO.GetComponent<Image>();
        fillImage.sprite = fillSprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.raycastTarget = false;
        fillImage.fillAmount = targetFill;
        fillImage.color = fullColor;
    }

    /// <summary>把 RectTransform 拉伸填满父级,四周留 padding。</summary>
    private static void StretchFull(RectTransform rt, Vector2 padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = padding;
        rt.offsetMax = -padding;
    }

    private void HandleHealthChanged(int current, int max)
    {
        targetFill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        // 颜色立即跟随(填充长度可平滑过渡)
        if (fillImage != null) fillImage.color = ColorForRatio(targetFill);
    }

    /// <summary>满→半→濒死 的双段线性渐变。</summary>
    private Color ColorForRatio(float r)
    {
        if (r >= 0.5f)
            return Color.Lerp(midColor, fullColor, (r - 0.5f) / 0.5f);
        return Color.Lerp(lowColor, midColor, r / 0.5f);
    }

    private void Update()
    {
        if (fillImage == null) return;
        if (lerpSpeed <= 0f)
        {
            fillImage.fillAmount = targetFill;
        }
        else if (!Mathf.Approximately(fillImage.fillAmount, targetFill))
        {
            // 用 unscaledDeltaTime,这样即使 GameOver 把 timeScale 设 0 也能补完动画
            fillImage.fillAmount = Mathf.MoveTowards(
                fillImage.fillAmount, targetFill, lerpSpeed * Time.unscaledDeltaTime);
        }
    }
}
