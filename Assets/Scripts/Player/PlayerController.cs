using UnityEngine;

/// <summary>
/// 玩家飞机控制器（移动端触屏拖动 + 编辑器鼠标测试）
///
/// 设计要点：
/// - 目标平台是 iOS / Android，所以用「手指按住拖动，飞机跟随手指」的方式移动。
/// - 为了方便在 Unity 编辑器里用鼠标测试，这里同时支持鼠标（鼠标即视为单点触摸）。
/// - 飞机的 Rigidbody2D 应设为 Kinematic：位置完全由本脚本控制，不受重力/碰撞力影响。
/// - 飞机被限制在屏幕可见范围内，不会跑出画面。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("拖动跟随的平滑度。值越大，飞机越快贴合手指；太小会显得拖沓。")]
    [SerializeField] private float followSpeed = 20f;

    [Tooltip("是否让飞机中心精确对齐手指。关闭时飞机会与手指保持初始相对偏移，\n手指不会挡住飞机，手感更好（推荐开启偏移）。")]
    [SerializeField] private bool keepFingerOffset = true;

    [Header("初始位置")]
    [Tooltip("开局时把飞机自动摆到屏幕底部居中(避免预制体位置跑到屏幕外)。")]
    [SerializeField] private bool snapToBottomOnStart = true;

    [Tooltip("距屏幕底部的高度(单位)。值越大飞机离底边越远。")]
    [SerializeField] private float startBottomMargin = 1.5f;

    // 摄像机引用，用于屏幕坐标 <-> 世界坐标转换
    private Camera mainCamera;

    // 飞机自身的碰撞体范围（用于计算屏幕边界，防止飞机一半飞出画面）
    private Vector2 halfSize;

    // 当前是否正在拖动
    private bool isDragging = false;

    // 手指/鼠标按下时，飞机与触点之间的世界坐标偏移
    private Vector3 touchOffset;

    private void Awake()
    {
        mainCamera = Camera.main;

        // 用 SpriteRenderer 的尺寸估算飞机的一半宽高，做边界限制
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            halfSize = sr.bounds.size * 0.5f;
        }
    }

    private void Start()
    {
        if (snapToBottomOnStart)
        {
            SnapToBottomCenter();
        }
    }

    /// <summary>把飞机摆到屏幕底部居中,避免预制体初始位置跑到屏幕外。</summary>
    private void SnapToBottomCenter()
    {
        float camHalfHeight = mainCamera.orthographicSize;
        Vector3 camCenter = mainCamera.transform.position;
        float y = camCenter.y - camHalfHeight + startBottomMargin;
        transform.position = new Vector3(camCenter.x, y, 0f);
    }

    private void Update()
    {
        HandleInput();
    }

    /// <summary>
    /// 统一处理触摸和鼠标输入。
    /// 在真机上读 Input.touches；在编辑器里读鼠标，方便测试。
    /// </summary>
    private void HandleInput()
    {
        // ---- 真机触摸 ----
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            Vector3 worldPos = ScreenToWorld(touch.position);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    BeginDrag(worldPos);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (isDragging) MoveTo(worldPos);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isDragging = false;
                    break;
            }
            return;
        }

        // ---- 编辑器/PC 鼠标（仅用于测试）----
        if (Input.GetMouseButtonDown(0))
        {
            BeginDrag(ScreenToWorld(Input.mousePosition));
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            MoveTo(ScreenToWorld(Input.mousePosition));
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    /// <summary>开始拖动，记录手指与飞机的偏移。</summary>
    private void BeginDrag(Vector3 touchWorldPos)
    {
        isDragging = true;
        // 记录偏移：让飞机不会瞬移到手指正下方，而是保持按下时的相对位置
        touchOffset = keepFingerOffset ? (transform.position - touchWorldPos) : Vector3.zero;
    }

    /// <summary>把飞机平滑移动到目标位置，并限制在屏幕内。</summary>
    private void MoveTo(Vector3 touchWorldPos)
    {
        Vector3 target = touchWorldPos + touchOffset;
        target.z = 0f; // 2D 游戏保持 z 为 0

        // 平滑跟随，避免生硬瞬移
        Vector3 newPos = Vector3.Lerp(transform.position, target, followSpeed * Time.deltaTime);

        // 限制在屏幕可见范围内
        transform.position = ClampToScreen(newPos);
    }

    /// <summary>屏幕坐标转世界坐标。</summary>
    private Vector3 ScreenToWorld(Vector3 screenPos)
    {
        // 飞机所在平面与摄像机的距离（正交相机下用其与相机 z 的差即可）
        screenPos.z = Mathf.Abs(mainCamera.transform.position.z);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }

    /// <summary>把位置限制在摄像机可见区域内（考虑飞机自身大小）。</summary>
    private Vector3 ClampToScreen(Vector3 worldPos)
    {
        // 正交相机的可见半高/半宽
        float camHalfHeight = mainCamera.orthographicSize;
        float camHalfWidth = camHalfHeight * mainCamera.aspect;

        Vector3 camCenter = mainCamera.transform.position;

        float minX = camCenter.x - camHalfWidth + halfSize.x;
        float maxX = camCenter.x + camHalfWidth - halfSize.x;
        float minY = camCenter.y - camHalfHeight + halfSize.y;
        float maxY = camCenter.y + camHalfHeight - halfSize.y;

        worldPos.x = Mathf.Clamp(worldPos.x, minX, maxX);
        worldPos.y = Mathf.Clamp(worldPos.y, minY, maxY);
        worldPos.z = 0f;
        return worldPos;
    }
}
