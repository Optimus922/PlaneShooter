using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏总管(阶段5 核心)。单例,负责:游戏状态、分数、Game Over 流程、重新开始。
///
/// 设计要点:
/// - 单例 Instance 方便 Enemy/PlayerHealth 等直接调用,无需到处拖引用。
/// - 用 C# 事件(OnScoreChanged / OnStateChanged)广播变化,UI 订阅即可,GameManager 不依赖 UI(解耦)。
/// - Game Over 时把 Time.timeScale 设 0 暂停玩法;Restart 时恢复并重载当前场景。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, GameOver }

    [Header("初始状态")]
    [Tooltip("游戏开始时的分数。")]
    [SerializeField] private int startScore = 0;

    public GameState State { get; private set; }
    public int Score { get; private set; }

    // ---- 事件:UI 订阅这些来刷新显示 ----
    /// <summary>分数变化时触发,参数为新分数。</summary>
    public event Action<int> OnScoreChanged;
    /// <summary>游戏状态变化时触发,参数为新状态。</summary>
    public event Action<GameState> OnStateChanged;

    private void Awake()
    {
        // 标准单例:重复实例直接销毁
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 确保从暂停场景重载后时间恢复正常
        Time.timeScale = 1f;
        Score = startScore;
        SetState(GameState.Playing);
        OnScoreChanged?.Invoke(Score);
    }

    /// <summary>加分(阶段6:敌机死亡时调用)。GameOver 后不再计分。</summary>
    public void AddScore(int amount)
    {
        if (State != GameState.Playing) return;
        Score += amount;
        OnScoreChanged?.Invoke(Score);
    }

    /// <summary>玩家死亡时由 PlayerHealth 调用,进入 Game Over。</summary>
    public void OnPlayerDied()
    {
        if (State == GameState.GameOver) return;
        SetState(GameState.GameOver);
        Time.timeScale = 0f;   // 暂停玩法(敌机/子弹/刷怪都用 deltaTime,会停)
    }

    /// <summary>重新开始:恢复时间并重载当前场景。</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void SetState(GameState newState)
    {
        State = newState;
        OnStateChanged?.Invoke(State);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
