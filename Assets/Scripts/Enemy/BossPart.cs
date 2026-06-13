using UnityEngine;

/// <summary>
/// Boss 可受击部位(阶段9)。主炮/副炮共用此脚本,血量在 Inspector 配。
///
/// 设计要点:
/// - 实现 IDamageable:玩家子弹命中即扣血,与普通敌机走同一套命中逻辑。
/// - 被击中闪白(SpriteRenderer 短暂变白)给打击反馈。
/// - 头顶一条世界空间小血条:运行时用 1x1 白贴图建两个 SpriteRenderer(底+填充),
///   按血量比例横向缩放填充,不依赖任何贴图资源,自给自足。
/// - 血量归零:隐藏自己(连同碰撞体停用)并回调 boss(onDestroyed),由 boss 统计是否全灭。
/// - 本体只有"部位"挂碰撞体且在 Enemy 层,boss 车体不挂可受击碰撞体 → 只有炮台能被打。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BossPart : MonoBehaviour, IDamageable
{
    [Header("血量")]
    [Tooltip("本部位最大血量(主炮建议50,副炮建议20)。")]
    [SerializeField] private int maxHealth = 50;

    [Header("受击反馈")]
    [Tooltip("被击中闪白的持续时间(秒)。")]
    [SerializeField] private float hitFlashDuration = 0.06f;
    [Tooltip("闪白颜色。")]
    [SerializeField] private Color hitFlashColor = Color.white;

    [Header("血条")]
    [Tooltip("血条相对部位中心的纵向偏移(世界单位,正=上方)。")]
    [SerializeField] private float barYOffset = 0.7f;
    [Tooltip("血条尺寸(世界单位)。")]
    [SerializeField] private Vector2 barSize = new Vector2(1.1f, 0.16f);
    [Tooltip("血条排序层级(要盖在 boss 之上)。")]
    [SerializeField] private int barSortingOrder = 50;

    [Header("满血/濒死颜色")]
    [SerializeField] private Color fullColor = new Color(1f, 0.3f, 0.35f, 1f);
    [SerializeField] private Color lowColor = new Color(1f, 0.85f, 0.2f, 1f);

    private int currentHealth;
    private SpriteRenderer sr;
    private Color baseColor;
    private float flashTimer;

    private Transform barRoot;
    private Transform barFill;
    private SpriteRenderer barFillSr;

    private System.Action<BossPart> onDestroyed;
    private bool dead;

    public bool IsDead => dead;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        currentHealth = maxHealth;
        BuildHealthBar();
        UpdateBar();
    }

    /// <summary>由 TankBoss 注入死亡回调。</summary>
    public void Init(System.Action<BossPart> destroyedCallback)
    {
        onDestroyed = destroyedCallback;
    }

    public void TakeDamage(int damage)
    {
        if (dead) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        // 闪白反馈
        sr.color = hitFlashColor;
        flashTimer = hitFlashDuration;

        UpdateBar();

        if (currentHealth <= 0)
            Destroyed();
    }

    private void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && !dead)
                sr.color = baseColor;
        }
    }

    private void Destroyed()
    {
        dead = true;
        // 爆炸 + 音效(用当前位置)
        if (ExplosionManager.Instance != null) ExplosionManager.Instance.SpawnAt(transform.position);
        if (SfxManager.Instance != null) SfxManager.Instance.PlayExplosion();

        // 隐藏部位与血条、停用碰撞体
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        sr.enabled = false;
        if (barRoot != null) barRoot.gameObject.SetActive(false);

        onDestroyed?.Invoke(this);
    }

    // ---------- 世界空间血条(运行时用 1x1 白贴图构建) ----------
    private void BuildHealthBar()
    {
        Sprite white = WhiteSprite();

        barRoot = new GameObject("PartHealthBar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0f, barYOffset, 0f);

        // 底框(深色,略大)
        var bg = new GameObject("BarBG", typeof(SpriteRenderer));
        bg.transform.SetParent(barRoot, false);
        var bgSr = bg.GetComponent<SpriteRenderer>();
        bgSr.sprite = white;
        bgSr.color = new Color(0.03f, 0.05f, 0.08f, 0.9f);
        bgSr.sortingOrder = barSortingOrder;
        bg.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);

        // 填充(锚到左端:用一个父物体定位左边缘,子物体右伸)
        var fillAnchor = new GameObject("FillAnchor").transform;
        fillAnchor.SetParent(barRoot, false);
        fillAnchor.localPosition = new Vector3(-barSize.x * 0.5f, 0f, 0f);
        barFill = fillAnchor;

        var fill = new GameObject("BarFill", typeof(SpriteRenderer));
        fill.transform.SetParent(fillAnchor, false);
        barFillSr = fill.GetComponent<SpriteRenderer>();
        barFillSr.sprite = white;
        barFillSr.color = fullColor;
        barFillSr.sortingOrder = barSortingOrder + 1;
        // 子物体以左端为原点向右伸:pivot 居中,故 localPosition.x = halfWidth
        float innerH = barSize.y * 0.7f;
        fill.transform.localScale = new Vector3(barSize.x, innerH, 1f);
        fill.transform.localPosition = new Vector3(barSize.x * 0.5f, 0f, 0f);
    }

    private void UpdateBar()
    {
        if (barFill == null || barFillSr == null) return;
        float ratio = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        // 缩放 FillAnchor 的 x 来裁切填充长度(左端固定)
        barFill.localScale = new Vector3(ratio, 1f, 1f);
        barFillSr.color = Color.Lerp(lowColor, fullColor, ratio);
    }

    private static Sprite cachedWhite;
    private static Sprite WhiteSprite()
    {
        if (cachedWhite != null) return cachedWhite;
        var tex = Texture2D.whiteTexture;
        cachedWhite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width); // pixelsPerUnit=宽 → 1 单位见方
        return cachedWhite;
    }
}
