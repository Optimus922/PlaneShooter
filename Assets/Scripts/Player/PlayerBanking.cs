using UnityEngine;

/// <summary>
/// 玩家机倾斜切帧(spritesheet 风格):根据飞机的水平移动方向,
/// 自动在 5 档倾斜 sprite 之间切换,模拟战机左右压坡度的动作。
///
/// 设计要点:
/// - 不修改 PlayerController:本脚本自己按帧记录飞机 x 位移,推断左右移动方向。
/// - 用平滑值(Lerp)驱动,避免方向抖动导致 sprite 频繁跳变。
/// - 帧映射:bankSprites[0..4] = 大左 / 左 / 正中 / 右 / 大右。
///   只需把 outputs 里的 player_bank_0..4 拖进数组即可。
/// - 挂在玩家飞机本体上(与 SpriteRenderer 同物体)。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerBanking : MonoBehaviour
{
    [Header("倾斜帧(0=大左 1=左 2=中 3=右 4=大右)")]
    [Tooltip("依次拖入 5 张倾斜 sprite。数量也可不为 5,会自动按比例映射。")]
    [SerializeField] private Sprite[] bankSprites;

    [Header("手感")]
    [Tooltip("达到最大倾角所需的水平速度(单位/秒)。越小越灵敏。")]
    [SerializeField] private float maxTiltSpeed = 6f;

    [Tooltip("倾斜响应平滑度。越大越跟手,越小越柔和。")]
    [SerializeField] private float smoothing = 8f;

    private SpriteRenderer sr;
    private float lastX;
    private float tilt;     // 平滑后的倾斜量,范围 -1..+1
    private int currentIndex = -1;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        lastX = transform.position.x;
        // 初始显示正中帧
        SetFrameByTilt(0f);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 本帧水平速度(由实际位移推断,兼容拖动跟随式移动)
        float vx = (transform.position.x - lastX) / dt;
        lastX = transform.position.x;

        // 归一化到 -1..+1
        float target = Mathf.Clamp(vx / maxTiltSpeed, -1f, 1f);
        // 平滑过渡,避免抖动
        tilt = Mathf.Lerp(tilt, target, 1f - Mathf.Exp(-smoothing * dt));

        SetFrameByTilt(tilt);
    }

    /// <summary>把 -1..+1 的倾斜量映射到 sprite 帧并切换。</summary>
    private void SetFrameByTilt(float t)
    {
        if (bankSprites == null || bankSprites.Length == 0) return;

        int n = bankSprites.Length;
        // t: -1 -> 0 号帧, 0 -> 中间帧, +1 -> 末帧
        float f = (t + 1f) * 0.5f * (n - 1);
        int idx = Mathf.Clamp(Mathf.RoundToInt(f), 0, n - 1);

        if (idx != currentIndex)
        {
            currentIndex = idx;
            sr.sprite = bankSprites[idx];
        }
    }
}
