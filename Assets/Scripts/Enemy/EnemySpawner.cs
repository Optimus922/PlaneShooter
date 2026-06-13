using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 敌机生成器(阶段8:波次关卡系统 + 多关串联 + 关卡横幅)。
///
/// - 按 levels(多个 LevelData)顺序执行:每关开始前居中提示「第 X 关」2 秒,
///   关内按 LevelData.waves 顺序刷怪(击毁全部才过波),整关清完后提示「第 X 关通过」2 秒,再进下一关;
/// - 所有关卡清完 → 通知 GameManager 通关(Victory)。
///
/// 设计要点:
/// - 横幅期间【不冻结 timeScale】(那会连玩家移动/背景滚动都停),只是 spawner 协程在这 2 秒不刷怪、
///   且此刻屏幕上本就没有敌机,等效于「暂停刷怪」,且玩家仍可移动,体验更好。
/// - 仍走对象池;支持每波不同敌机预制体,故用「按预制体分桶」的多个对象池。
/// - 敌机离场通过 Enemy.SetOnReturned 回调计数,生成器据此判断本波是否清空。
/// - GameOver/Victory 后停止生成(检查 GameManager 状态)。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("关卡")]
    [Tooltip("本场景按顺序执行的关卡列表(每个 LevelData = 一关)。")]
    [SerializeField] private LevelData[] levels;

    [Tooltip("每关开始/通过横幅的显示秒数。")]
    [SerializeField] private float bannerDuration = 2f;

    [Tooltip("boss 出场前的警告横幅显示秒数。")]
    [SerializeField] private float bossWarningDuration = 2f;

    [Tooltip("「第X关通过」与下一关「第X关」横幅之间的间隔(秒)。")]
    [SerializeField] private float betweenLevelsGap = 1f;

    [Header("敌机")]
    [Tooltip("默认敌机 Prefab(波次未指定 enemyOverride 时用它)。")]
    [SerializeField] private Enemy enemyPrefab;

    [Header("生成位置")]
    [Tooltip("敌机生成时距屏幕顶部的额外高度(在画面外一点生成,飞入更自然)。")]
    [SerializeField] private float spawnHeightOffset = 1f;

    [Tooltip("左右两侧留出的边距,避免敌机贴边生成。")]
    [SerializeField] private float horizontalPadding = 0.8f;

    [Header("对象池设置")]
    [SerializeField] private int defaultPoolSize = 10;
    [SerializeField] private int maxPoolSize = 50;

    // 按预制体分桶的对象池(支持多种敌机)
    private readonly Dictionary<Enemy, IObjectPool<Enemy>> pools = new Dictionary<Enemy, IObjectPool<Enemy>>();
    private Camera mainCamera;

    private int aliveCount;        // 当前在场(未离场)的本波敌机数
    private int spawnedThisWave;   // 本波已生成数
    private bool waveActive;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Start()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] 未配置 levels(关卡列表),不会刷怪。");
            return;
        }
        StartCoroutine(RunCampaign());
    }

    /// <summary>按关卡列表顺序执行整场:每关前后插入横幅提示。</summary>
    private IEnumerator RunCampaign()
    {
        // 等一帧,确保 GameHUD.Start 已订阅 OnBanner(Start 执行顺序不确定),否则首条「第1关」横幅可能丢失
        yield return null;

        for (int i = 0; i < levels.Length; i++)
        {
            if (IsGameEnded()) yield break;

            LevelData level = levels[i];
            if (level == null || level.waves == null || level.waves.Length == 0)
                continue;   // 跳过空关

            int levelNumber = i + 1;

            // 关卡开始横幅:「第 X 关」,显示 bannerDuration 秒(此刻无敌机,等效暂停刷怪)
            ShowBanner($"第 {levelNumber} 关", bannerDuration);
            yield return new WaitForSeconds(bannerDuration);
            if (IsGameEnded()) yield break;

            // 执行本关所有波次
            yield return StartCoroutine(RunLevel(level));
            if (IsGameEnded()) yield break;

            // 本关 boss(若配置):先警告横幅,再生成并等待被击杀
            if (level.bossPrefab != null)
            {
                ShowBanner("警告!Boss 来袭", bossWarningDuration);
                yield return new WaitForSeconds(bossWarningDuration);
                if (IsGameEnded()) yield break;

                yield return StartCoroutine(RunBoss(level.bossPrefab));
                if (IsGameEnded()) yield break;
            }

            // 关卡通过横幅:「第 X 关通过」
            ShowBanner($"第 {levelNumber} 关通过", bannerDuration);
            yield return new WaitForSeconds(bannerDuration);
            if (IsGameEnded()) yield break;

            // 与下一关「第X关」横幅之间留间隔,避免两段闪烁紧贴
            if (i < levels.Length - 1 && betweenLevelsGap > 0f)
                yield return new WaitForSeconds(betweenLevelsGap);
        }

        // 全部关卡清完 → 通关胜利
        if (!IsGameEnded() && GameManager.Instance != null)
            GameManager.Instance.OnLevelCleared();
    }

    private void ShowBanner(string text, float duration)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowBanner(text, duration);
    }

    /// <summary>按波次表执行一整关。</summary>
    private IEnumerator RunLevel(LevelData level)
    {
        for (int w = 0; w < level.waves.Length; w++)
        {
            // GameOver 时中止
            if (IsGameEnded()) yield break;

            LevelData.WaveData wave = level.waves[w];
            Enemy prefab = wave.enemyOverride != null ? wave.enemyOverride : enemyPrefab;

            yield return StartCoroutine(RunWave(wave, prefab));

            if (IsGameEnded()) yield break;

            // 本波清空后的停顿
            if (wave.delayAfterClear > 0f)
                yield return new WaitForSeconds(wave.delayAfterClear);
        }
    }

    /// <summary>生成本关 boss,等待其被击杀。</summary>
    private IEnumerator RunBoss(GameObject bossPrefab)
    {
        // 在屏幕顶部中央上方生成,boss 自己会移动进场
        float camHalfH = mainCamera.orthographicSize;
        Vector3 spawnPos = new Vector3(
            mainCamera.transform.position.x,
            mainCamera.transform.position.y + camHalfH + 2f,
            0f);

        GameObject bossGO = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        bool defeated = false;

        TankBoss boss = bossGO.GetComponent<TankBoss>();
        if (boss != null)
            boss.Init(() => defeated = true);
        else
            defeated = true;   // 没有 TankBoss 脚本则跳过,避免卡死

        // 等到 boss 被击杀(或游戏结束)
        while (!defeated && !IsGameEnded())
            yield return null;
    }

    /// <summary>执行单波:逐架生成,然后等到本波敌机全部离场。</summary>
    private IEnumerator RunWave(LevelData.WaveData wave, Enemy prefab)
    {
        waveActive = true;
        aliveCount = 0;
        spawnedThisWave = 0;

        for (int i = 0; i < wave.enemyCount; i++)
        {
            if (IsGameEnded()) { waveActive = false; yield break; }

            SpawnOne(prefab);
            spawnedThisWave++;

            if (i < wave.enemyCount - 1 && wave.spawnInterval > 0f)
                yield return new WaitForSeconds(wave.spawnInterval);
        }

        // 等到本波生成完毕且场上敌机全部离场(击毁全部才过波)
        while (aliveCount > 0 && !IsGameEnded())
            yield return null;

        waveActive = false;
    }

    private void SpawnOne(Enemy prefab)
    {
        IObjectPool<Enemy> pool = GetPool(prefab);
        pool.Get();   // 取出会触发 OnGetEnemy 摆位 + 激活
        aliveCount++;
    }

    /// <summary>敌机离场回调(击毁/出屏/撞玩家),减少存活计数。</summary>
    private void HandleEnemyReturned(Enemy e)
    {
        if (waveActive || aliveCount > 0)
            aliveCount = Mathf.Max(0, aliveCount - 1);
    }

    private bool IsGameEnded()
    {
        return GameManager.Instance != null
            && GameManager.Instance.State != GameManager.GameState.Playing;
    }

    /// <summary>取得(或创建)某预制体对应的对象池。</summary>
    private IObjectPool<Enemy> GetPool(Enemy prefab)
    {
        if (pools.TryGetValue(prefab, out var existing))
            return existing;

        IObjectPool<Enemy> pool = null;
        pool = new ObjectPool<Enemy>(
            createFunc: () =>
            {
                Enemy e = Instantiate(prefab);
                e.SetPool(pool);
                e.SetOnReturned(HandleEnemyReturned);
                return e;
            },
            actionOnGet: OnGetEnemy,
            actionOnRelease: e => e.gameObject.SetActive(false),
            actionOnDestroy: e => Destroy(e.gameObject),
            collectionCheck: false,
            defaultCapacity: defaultPoolSize,
            maxSize: maxPoolSize
        );
        pools[prefab] = pool;
        return pool;
    }

    private void OnGetEnemy(Enemy e)
    {
        e.transform.position = GetRandomTopPosition();
        e.transform.rotation = Quaternion.identity;
        e.gameObject.SetActive(true);
    }

    /// <summary>计算一个屏幕顶部的随机生成位置。</summary>
    private Vector3 GetRandomTopPosition()
    {
        float camHalfHeight = mainCamera.orthographicSize;
        float camHalfWidth = camHalfHeight * mainCamera.aspect;
        Vector3 camCenter = mainCamera.transform.position;

        float minX = camCenter.x - camHalfWidth + horizontalPadding;
        float maxX = camCenter.x + camHalfWidth - horizontalPadding;
        float x = Random.Range(minX, maxX);
        float y = camCenter.y + camHalfHeight + spawnHeightOffset;

        return new Vector3(x, y, 0f);
    }
}
