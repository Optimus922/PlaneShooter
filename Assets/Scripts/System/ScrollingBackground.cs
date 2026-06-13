using System.Collections.Generic;
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
/// - 每层用「若干张」相同的 Sprite 首尾相接,持续向下移动;
///   当最下面那张完全移出屏幕下方,就把它"跳"回当前最高那张的正上方(leapfrog),
///   实现无缝无限循环。
/// - 贴图数量按屏幕高度自动计算:保证任意宽高比(尤其是竖屏 1080x1920)都能盖满,
///   不会出现"上半屏露出空白/纯色背景"的问题。
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
        [HideInInspector] public List<Transform> tiles = new List<Transform>();
        [HideInInspector] public float tileHeight;   // 单张贴图缩放后的世界高度
    }

    [Tooltip("背景层,从远到近排列。每层会自动生成若干首尾相接的贴图。")]
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

    /// <summary>为一层创建足够数量的相接贴图,并缩放铺满屏幕宽度。</summary>
    private void SetupLayer(Layer layer)
    {
        if (layer.sprite == null)
        {
            Debug.LogWarning($"[ScrollingBackground] 有一层没有指定 sprite,已跳过。");
            return;
        }

        layer.tiles.Clear();

        // 先建一张算出缩放后的高度,再决定需要多少张。
        Transform first = CreateTile(layer, "Tile0");
        layer.tileHeight = GetScaledHeight(first);
        layer.tiles.Add(first);

        // 需要的贴图数:盖满相机高度 + 1 张缓冲(回收时不漏空)。至少 2 张。
        float camHeight = camHalfHeight * 2f;
        int count = Mathf.CeilToInt(camHeight / Mathf.Max(layer.tileHeight, 0.0001f)) + 1;
        count = Mathf.Max(count, 2);

        for (int i = 1; i < count; i++)
            layer.tiles.Add(CreateTile(layer, $"Tile{i}"));

        // 从屏幕底部往上依次堆叠铺满(开局不露空白)。
        float camBottom = cam.transform.position.y - camHalfHeight;
        float camX = cam.transform.position.x;
        for (int i = 0; i < layer.tiles.Count; i++)
        {
            float centerY = camBottom + layer.tileHeight * (i + 0.5f);
            layer.tiles[i].position = new Vector3(camX, centerY, 0f);
        }
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
        float screenWidth = camHalfWidth * 2f;
        if (spriteWidth > 0f)
        {
            float scale = screenWidth / spriteWidth;
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
            if (layer.tiles == null || layer.tiles.Count == 0) continue;
            ScrollLayer(layer, dt);
        }
    }

    /// <summary>向下滚动一层,并在贴图移出屏幕底部后跳回最高一张的正上方。</summary>
    private void ScrollLayer(Layer layer, float dt)
    {
        Vector3 delta = Vector3.down * layer.scrollSpeed * dt;
        foreach (var tile in layer.tiles)
            tile.position += delta;

        float camBottom = cam.transform.position.y - camHalfHeight;

        // 把所有"整体移到屏幕下方"的贴图,接到当前最高那张的正上方。
        foreach (var tile in layer.tiles)
        {
            float tileTopY = tile.position.y + layer.tileHeight * 0.5f;
            if (tileTopY < camBottom)
            {
                float highestCenterY = GetHighestCenterY(layer);
                tile.position = new Vector3(
                    tile.position.x,
                    highestCenterY + layer.tileHeight,
                    tile.position.z);
            }
        }
    }

    /// <summary>返回该层当前所有贴图里最高的那张的中心 Y。</summary>
    private float GetHighestCenterY(Layer layer)
    {
        float maxY = float.NegativeInfinity;
        foreach (var tile in layer.tiles)
            if (tile.position.y > maxY) maxY = tile.position.y;
        return maxY;
    }
}
