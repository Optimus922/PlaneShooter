using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家血量(阶段5)。在阶段4基础上补:无敌帧、受伤闪烁、死亡触发 Game Over、血量事件供 UI。
///
/// 设计要点:
/// - 受伤后进入 invincibleDuration 秒无敌,期间忽略一切 TakeDamage,避免一次贴脸被连扣致死。
/// - 无敌期间 sprite 闪烁(交替显隐)给玩家视觉反馈。
/// - 血量变化广播 OnHealthChanged(current, max),GameHUD 订阅刷新。
/// - 死亡通知 GameManager.OnPlayerDied 进入 Game Over,不再只是隐藏自己。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("血量")]
    [Tooltip("玩家最大生命值。")]
    [SerializeField] private int maxHealth = 3;

    [Header("无敌帧")]
    [Tooltip("受伤后无敌持续时间(秒)。")]
    [SerializeField] private float invincibleDuration = 1.2f;

    [Tooltip("无敌期间闪烁的间隔(秒)。")]
    [SerializeField] private float blinkInterval = 0.1f;

    private int currentHealth;
    private bool isInvincible;
    private SpriteRenderer sr;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    /// <summary>血量变化事件(当前血量, 最大血量)。UI 订阅刷新。</summary>
    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // 初始广播一次,让 UI 显示满血
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>受到伤害。无敌期间忽略。</summary>
    public void TakeDamage(int damage)
    {
        if (isInvincible || currentHealth <= 0) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (SfxManager.Instance != null) SfxManager.Instance.PlayHurt();
            StartCoroutine(InvincibilityRoutine());
        }
    }

    /// <summary>无敌 + 闪烁协程。</summary>
    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < invincibleDuration)
        {
            visible = !visible;
            if (sr != null) sr.enabled = visible;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        if (sr != null) sr.enabled = true;   // 结束后恢复显示
        isInvincible = false;
    }

    /// <summary>死亡:通知 GameManager 进入 Game Over。</summary>
    private void Die()
    {
        StopAllCoroutines();
        if (sr != null) sr.enabled = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied();
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] 场景缺少 GameManager,无法触发 Game Over。");
        }

        if (SfxManager.Instance != null) SfxManager.Instance.PlayGameOver();

        // 隐藏飞机本体,停止继续被撞
        gameObject.SetActive(false);
    }
}
