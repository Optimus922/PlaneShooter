using System.Collections;
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
    [Tooltip("居中关卡横幅文本(显示「第 X 关」「第 X 关通过」)。可留空。")]
    [SerializeField] private Text bannerText;

    [Header("Game Over 面板")]
    [Tooltip("结算面板根物体(整体显隐)。GameOver 与 Victory 复用同一面板。")]
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("结算面板标题文本(显示「游戏结束」或「通关!」)。可留空。")]
    [SerializeField] private Text titleText;
    [Tooltip("结算面板上的最终分数文本。")]
    [SerializeField] private Text finalScoreText;
    [Tooltip("重新开始按钮。")]
    [SerializeField] private Button restartButton;
    [Tooltip("退出游戏按钮。")]
    [SerializeField] private Button quitButton;

    private void Start()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged += HandleScoreChanged;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            GameManager.Instance.OnBanner += HandleBanner;
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

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (bannerText != null)
            bannerText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // 退订,避免场景重载时野引用
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnBanner -= HandleBanner;
        }
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        if (restartButton != null)
            restartButton.onClick.RemoveListener(OnRestartClicked);
        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    private void HandleScoreChanged(int score)
    {
        if (scoreText != null) scoreText.text = $"分数: {score}";
    }

    private Coroutine bannerRoutine;

    /// <summary>显示居中关卡横幅:文本 + duration 秒后隐藏。</summary>
    private void HandleBanner(string text, float duration)
    {
        if (bannerText == null) return;
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(text, duration));
    }

    private IEnumerator BannerRoutine(string text, float duration)
    {
        bannerText.text = text;
        bannerText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        bannerText.gameObject.SetActive(false);
        bannerRoutine = null;
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (healthText != null) healthText.text = $"HP: {current}/{max}";
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        bool gameOver = state == GameManager.GameState.GameOver;
        bool victory = state == GameManager.GameState.Victory;
        bool show = gameOver || victory;

        if (gameOverPanel != null) gameOverPanel.SetActive(show);

        if (show && GameManager.Instance != null)
        {
            if (titleText != null)
                titleText.text = victory ? "通关!" : "游戏结束";
            if (finalScoreText != null)
                finalScoreText.text = $"最终分数\n{GameManager.Instance.Score}";
        }
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }

    private void OnQuitClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.QuitGame();
    }
}
