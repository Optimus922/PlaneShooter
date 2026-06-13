using UnityEngine;

/// <summary>
/// 关卡数据(阶段8)。用 ScriptableObject 定义一关由哪些波次组成,
/// 策划可在 Project 里建多个 .asset 关卡资产、不改代码即可调关卡/加关卡。
///
/// 设计:
/// - 一个 LevelData = 若干 WaveData 顺序执行。
/// - 每波:生成 enemyCount 架敌机,每架间隔 spawnInterval 秒;
///   本波敌机【全部被击毁】后,停顿 delayAfterClear 秒再进下一波。
/// - 敌机预制体在波次里指定(留空则用 EnemySpawner 的默认预制体),方便以后混入不同敌机种类。
/// </summary>
[CreateAssetMenu(fileName = "Level", menuName = "PlaneShooter/Level Data", order = 0)]
public class LevelData : ScriptableObject
{
    [System.Serializable]
    public class WaveData
    {
        [Tooltip("本波生成的敌机数量。")]
        public int enemyCount = 5;

        [Tooltip("本波内每架敌机之间的生成间隔(秒)。")]
        public float spawnInterval = 0.8f;

        [Tooltip("本波敌机全部被击毁后,进入下一波前的停顿(秒)。")]
        public float delayAfterClear = 1.5f;

        [Tooltip("本波使用的敌机预制体。留空则用 EnemySpawner 的默认 enemyPrefab。")]
        public Enemy enemyOverride;
    }

    [Tooltip("关卡名称(用于通关面板/调试显示)。")]
    public string levelName = "Level 1";

    [Tooltip("按顺序执行的波次列表。")]
    public WaveData[] waves;

    [Tooltip("本关 boss(可空)。所有波次清完后生成,击杀 boss 才算过关。留空则无 boss。")]
    public GameObject bossPrefab;
}
