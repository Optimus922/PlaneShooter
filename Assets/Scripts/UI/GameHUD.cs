using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏 HUD(阶段5)。订阅 GameManager 与 PlayerHealth 的事件,刷新分数/血量,
/// 并在 Game Over 时显示结算面板与重新开始按钮。
///
/// 设计要点:
/// - 用事件订阅而非每帧轮询,数据变化才刷新,省性能也更清晰。
/// - 用 Unity 自带 UGUI 的 Text(不依赖 TextMeshPro),工程无需额外包即可编译。
/// - 引用全部 SerializeField,在 Inspector 里拖好即可;PlayerHealth 可留空(自动找)。
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家血量组件。留空则在 Start 时自动查找场景中的 PlayerHealth。")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("游戏中 HUD")]
    [Tooltip("分数文本。")]
    [SerializeField] private Text scoreText;
    [Tooltip("血量文本。")]
    [SerializeField] private Text healthText;

    [Header("Game Over 面板")]
    [Tooltip("结算面板根物体(整体显隐)。")]
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("结算面板上的最终分数文本。")]
    [SerializeField] private Text finalScoreText;
    [Tooltip("重新开始按钮。")]
    [SerializeField] private Button restartButton;

    private void Start()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged += HandleScoreChanged;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            HandleScoreChanged(GameManager.Instance.Score);
            HandleStateChanged(GameManager.Instance.State);
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // 退订,避免场景重载时野引用
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        if (restartButton != null)
            restartButton.onClick.RemoveListener(OnRestartClicked);
    }

    private void HandleScoreChanged(int score)
    {
        if (scoreText != null) scoreText.text = $"分数: {score}";
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (healthText != null) healthText.text = $"HP: {current}/{max}";
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        bool over = state == GameManager.GameState.GameOver;
        if (gameOverPanel != null) gameOverPanel.SetActive(over);
        if (over && finalScoreText != null && GameManager.Instance != null)
            finalScoreText.text = $"最终分数\n{GameManager.Instance.Score}";
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }
}
