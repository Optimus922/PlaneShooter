using UnityEngine;

/// <summary>
/// 卷轴式滚动背景(纵版射击常用的无限向下滚动星空)。
///
/// 用法:
/// - 在场景里建一个空物体(如 "Background"),挂上本脚本。
/// - 在 Inspector 的 layers 里配置若干层,每层一个 Sprite(用无缝平铺的星空图)。
/// - 远景层 scrollSpeed 小、近景层大,自动形成视差纵深感。
///
/// 实现要点:
/// - 每层用两张相同的 Sprite 首尾相接,持续向下移动;
///   当上面那张完全移出屏幕下方,就把它"跳"回顶部(leapfrog),实现无缝无限循环。
/// - 运行时自动按相机宽度缩放,铺满整个屏幕宽度,适配不同分辨率的移动端。
/// - 背景应放在最低的 Sorting Order,确保在飞机/子弹之下。
/// </summary>
public class ScrollingBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        [Tooltip("该层使用的背景 Sprite(应为纵向无缝平铺图)。")]
        public Sprite sprite;

        [Tooltip("滚动速度(单位/秒)。远景小、近景大,形成视差。")]
        public float scrollSpeed = 1f;

        [Tooltip("该层的 Sorting Order(越小越靠后)。远景应比近景小。")]
        public int sortingOrder = -10;

        [Tooltip("颜色叠加(可用于压暗远景层,默认白=原色)。")]
        public Color tint = Color.white;

        // ----- 运行时内部状态 -----
        [HideInInspector] public Transform tileA;
        [HideInInspector] public Transform tileB;
        [HideInInspector] public float tileHeight;   // 单张贴图世界高度
    }

    [Tooltip("背景层,从远到近排列。每层会自动生成两张首尾相接的贴图。")]
    [SerializeField] private Layer[] layers;

    [Tooltip("背景使用的 Sorting Layer 名称(留空用 Default)。")]
    [SerializeField] private string sortingLayerName = "";

    private Camera cam;
    private float camHalfHeight;
    private float camHalfWidth;

    private void Start()
    {
        cam = Camera.main;
        camHalfHeight = cam.orthographicSize;
        camHalfWidth = camHalfHeight * cam.aspect;

        foreach (var layer in layers)
        {
            SetupLayer(layer);
        }
    }

    /// <summary>为一层创建两张相接的贴图,并缩放铺满屏幕宽度。</summary>
    private void SetupLayer(Layer layer)
    {
        if (layer.sprite == null)
        {
            Debug.LogWarning($"[ScrollingBackground] 有一层没有指定 sprite,已跳过。");
            return;
        }

        layer.tileA = CreateTile(layer, "TileA");
        layer.tileB = CreateTile(layer, "TileB");

        // 计算缩放后的世界高度。把 A 的底边对齐屏幕底部,B 接在 A 正上方,
        // 这样开局就从屏幕底往上铺满(避免下方露空白)。
        layer.tileHeight = GetScaledHeight(layer.tileA);
        float camBottom = cam.transform.position.y - camHalfHeight;
        float aCenterY = camBottom + layer.tileHeight * 0.5f;
        layer.tileA.position = new Vector3(cam.transform.position.x, aCenterY, 0f);
        layer.tileB.position = new Vector3(cam.transform.position.x, aCenterY + layer.tileHeight, 0f);
    }

    /// <summary>创建一张贴图 GameObject,缩放到铺满屏幕宽度。</summary>
    private Transform CreateTile(Layer layer, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = layer.sprite;
        sr.color = layer.tint;
        sr.sortingOrder = layer.sortingOrder;
        if (!string.IsNullOrEmpty(sortingLayerName))
            sr.sortingLayerName = sortingLayerName;

        // 缩放:让贴图宽度 >= 屏幕宽度(等比放大,避免两侧露空)
        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;
        float screenWidth = camHalfWidth * 2f;
        if (spriteWidth > 0f)
        {
            float scale = screenWidth / spriteWidth;
            // 至少铺满宽度;若高度因此不足一屏也无妨(两张相接覆盖)
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }
        return go.transform;
    }

    private float GetScaledHeight(Transform tile)
    {
        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        return sr.sprite.bounds.size.y * tile.localScale.y;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var layer in layers)
        {
            if (layer.tileA == null || layer.tileB == null) continue;
            ScrollLayer(layer, dt);
        }
    }

    /// <summary>向下滚动一层,并在贴图移出屏幕底部后跳回顶部。</summary>
    private void ScrollLayer(Layer layer, float dt)
    {
        Vector3 delta = Vector3.down * layer.scrollSpeed * dt;
        layer.tileA.localPosition += delta;
        layer.tileB.localPosition += delta;

        // 谁在下面、谁在上面由实际 y 决定(两张轮流领先)
        RecycleIfBelow(layer, layer.tileA, layer.tileB);
        RecycleIfBelow(layer, layer.tileB, layer.tileA);
    }

    /// <summary>
    /// 若 tile 已整体移到屏幕下方(顶边都低于相机底),
    /// 就把它移到 other 的正上方,实现无缝循环。
    /// </summary>
    private void RecycleIfBelow(Layer layer, Transform tile, Transform other)
    {
        // tile 顶边的世界 y(本物体在父物体下,父物体一般在原点)
        float tileTopY = tile.position.y + layer.tileHeight * 0.5f;
        float camBottom = cam.transform.position.y - camHalfHeight;

        if (tileTopY < camBottom)
        {
            // 跳到另一张的正上方
            tile.position = new Vector3(
                tile.position.x,
                other.position.y + layer.tileHeight,
                tile.position.z);
        }
    }
}
