# PlaneShooter — 项目说明 / 进展记录

> 本文件用于在 AI 助手记忆丢失(如重装)时快速恢复项目上下文。请持续维护,把重要进展、决策、改动写进来。仅本地保存,不强制提交 git。
> 最近更新:2026-06-12

## 项目概览

- **名称:** PlaneShooter
- **类型:** Unity 2D 打飞机(纵版射击)游戏
- **目标平台:** iOS / Android 移动端
- **路径:** `F:\Projects\unity_projects\plane_shooting_game_2d\PlaneShooter`
- **引擎:** Unity 2022 系(使用自带 `UnityEngine.Pool.ObjectPool`)
- **关键包:** 2D Feature(2d.sprite / 2d.animation / 2d.tilemap / vectorgraphics)、ugui

## 架构约定

- **对象池复用:** 子弹和敌机都通过 `ObjectPool` 复用,出屏/命中/死亡时 `Release` 回收,**不要 `Destroy`**。这是为移动端性能做的核心设计。
- **碰撞检测:** 靠 Layer 碰撞矩阵 + `OnTriggerEnter2D`。能进入回调的对象基本就是预期目标(子弹↔敌机,敌机↔玩家)。
- **移动端输入:** 玩家飞机用「手指按住拖动跟随」;编辑器里用鼠标模拟单点触摸方便测试。
- **自动开火:** 移动端玩家无法边拖动边按键,所以射击是按固定射速自动持续开火。

## 已实现脚本(`Assets/Scripts/`)

- **`Player/PlayerController.cs`** — 玩家飞机移动。手指拖动跟随(Kinematic 刚体),平滑 `Lerp`,`ClampToScreen` 限制在屏幕内;支持鼠标测试。可调:`followSpeed`、`keepFingerOffset`(手指与机身偏移,避免手指挡飞机)。
- **`Player/PlayerShooter.cs`** — 自动开火 + 子弹对象池。可调:`fireRate`、池大小。`firePoint` 留空则用飞机自身位置。
- **`Player/Bullet.cs`** — 子弹向上飞,`OnTriggerEnter2D` 命中敌机调用 `enemy.TakeDamage` 后回收;出屏回收。可调:`speed`、`damage`。
- **`Player/PlayerHealth.cs`** — 阶段5 版血量。`TakeDamage` 扣血;受伤后 `invincibleDuration` 无敌帧 + sprite 闪烁(防贴脸连扣);血量变化广播 `OnHealthChanged(current,max)` 事件供 HUD;死亡调用 `GameManager.Instance.OnPlayerDied()` 进入 Game Over。暴露 `CurrentHealth`/`MaxHealth`。
- **`Enemy/Enemy.cs`** — 敌机向下移动、血量/受伤/死亡、撞玩家造成 `contactDamage`、对象池回收。`OnEnable` 重置血量(避免复用到残血)。`Die()` 调用 `GameManager.AddScore(scoreValue)` 计分。可调:`speed`、`maxHealth`、`contactDamage`、`scoreValue`。
- **`Enemy/EnemySpawner.cs`** — 协程定时在屏幕顶部随机 X 刷怪(阶段3 简单版)。可调:`spawnInterval`、边距。
- **`System/GameManager.cs`** — 单例。游戏状态(Playing/GameOver)、分数、Game Over 流程、Restart(重载场景)。事件 `OnScoreChanged`/`OnStateChanged` 供 UI 订阅(解耦)。`OnPlayerDied()` 把 `Time.timeScale` 设 0 暂停玩法。
- **`UI/GameHUD.cs`** — 订阅 GameManager/PlayerHealth 事件刷新分数/血量;GameOver 时显示结算面板 + 最终分数 + 重新开始按钮。用 Legacy `UnityEngine.UI.Text`(非 TMP)。引用全 SerializeField,PlayerHealth 留空会自动找。
- **`System/ScrollingBackground.cs`** — 见下"滚动背景"。
- **`System/SfxManager.cs`** — 单例 + AudioSource 池(voiceCount 个声部轮流 PlayOneShot,连射不打断)。语义化方法 `PlayShoot/PlayHit/PlayExplosion/PlayHurt/PlayGameOver`;可调 masterVolume、sfxEnabled。clip 在 Inspector 拖入。
- **`System/Explosion.cs`** — 单个爆炸:对象池取出后逐帧播序列帧,播完回收。`Init(frames,fps,sortingOrder)` 由管理器注入。
- **`System/ExplosionManager.cs`** — 单例 + 对象池。`SpawnAt(pos)` 在指定位置播一次爆炸。Inspector 配 frames(explosion_0..7)、fps、scale、sortingOrder。
- **`Player/PlayerBanking.cs`** — 见下"玩家机倾斜帧"。

## 美术资源(`Assets/Sprites/`)

- 4 张 sprite:`player_ship`(256²)、`enemy_ship`(256²)、`player_bullet`(64×128)、`enemy_bullet`(64²),透明背景。
- **风格:科幻霓虹**(2026-06-12 由扁平低多边形升级而来)。玩家机青/蓝霓虹+引擎喷焰,敌机品红 V 形+红色能量核心,子弹为发光能量束/能量球。暗色/星空背景下最出彩。
- 生成方式:Pillow 程序化绘制,脚本在项目根 `outputs_tmp_neon/`(`neon_common.py` + `gen_ships.py` + `gen_bullets.py`,4x 超采样)。要再生成/调整风格时复用这些脚本。
- 旧的扁平风原图备份在 `Sprites/_backup_flat/`,可回退。
- 替换时只改了 png 内容,未动 `.meta`,所以 Unity 导入设置(PPU/Pivot 等)保留,引用自动更新。

## 滚动背景(2026-06-12 新增)

- **贴图:** `Sprites/bg_stars_far.png` + `bg_stars_near.png`(均 1024²,纵向无缝平铺)。远景=深空底+星云+密集小星(不透明);近景=稀疏亮星+霓虹彩星(透明,叠在上层)。生成脚本:`outputs_tmp_neon/gen_starfield.py`(星点环绕补画、星云环绕模糊保证无缝)。已配好 `.meta`(Sprite 模式 / PPU 100)。
- **脚本:** `Assets/Scripts/System/ScrollingBackground.cs`。挂到一个空物体上,在 Inspector 配置多层(每层 sprite + scrollSpeed + sortingOrder),远景慢、近景快形成视差。每层用两张贴图首尾相接、leapfrog 回顶实现无限循环;运行时自动按相机宽度缩放铺满屏幕。
- **用法:** 建空物体"Background"挂脚本 → layers 设 2 层(far: speed 小、order -20;near: speed 大、order -10)→ 背景 sorting order 要低于飞机/子弹。
- **已修复的坑(2026-06-12):**
  - *Tint 透明黑*:`Layer.tint = Color.white` 的 C# 初始值对 Inspector 新增数组元素**不生效**,Unity 填的是 `(0,0,0,0)` 全透明 → tile 看不见。新增层后必须手动把 Tint 设白、Alpha=255。
  - *开局下方空白*:原来 tileA 中心放屏幕中心,贴图按宽度缩放后高度不够覆盖下半屏。已改为 tileA 底边对齐屏幕底部、tileB 接其上方,从底往上铺满。
  - *卷动色差*:`gen_starfield.py` 原来 4 团星云用了蓝/紫/品红/青 4 种色相,纵向有色相漂移,循环卷动时表现为"变色"。已统一为同一蓝青色系(低浓度),纵向色调均匀(顶/底段色差 ΔRGB≈0)。
  - *移动亮带(二次修复)*:统一色相后仍残留"中间亮、上下暗"的亮度分布,亮区随卷动周期性扫过屏幕(经过 tile 接缝暗区时"消失"),表现为上半浅蓝、滚到中间变深蓝。已把星云 alpha 大幅压淡(~40→~15)、底色略提(6,8,20→8,12,28),纵向蓝通道最亮-最暗差从 ~11 降到 ~3,通体均匀深蓝。**经验:无缝平铺不仅要接缝连续,整张图的大尺度亮度/色相也要均匀,否则循环时会看到移动的亮/色带。**

## 玩家开局定位(2026-06-12 修复)

- `PlayerController` 加了开局自动定位:`Start()` 调 `SnapToBottomCenter()` 把飞机摆到屏幕底部居中,避免预制体初始位置在屏幕外。可调 `snapToBottomOnStart`(开关)、`startBottomMargin`(离底边高度,默认 1.5)。

## 玩家机倾斜帧(2026-06-12 新增)

- **帧图:** `Sprites/player_bank_0..4.png`(各 256²,透明)= 大左/左/正中/右/大右 5 档倾斜姿态;另有 `player_bank_sheet.png` 横向大图(1280×256)仅作预览,工程里用 5 张独立帧。生成脚本 `outputs_tmp_neon/gen_player_bank.py`(基于 `gen_ships.make_player()`,做旋转±16°+横向压缩模拟滚转)。已配好 `.meta`。
- **脚本:** `Assets/Scripts/Player/PlayerBanking.cs`。挂在玩家飞机本体(与 SpriteRenderer 同物体),`LateUpdate` 按实际 x 位移推断水平速度→平滑→映射到 5 帧切换 sprite。不改 PlayerController。可调:`bankSprites`(拖 5 帧)、`maxTiltSpeed`(到满倾角的速度阈值)、`smoothing`(平滑度)。
- **用法:** 玩家机加 `PlayerBanking` 组件 → bankSprites 数组按 0..4 顺序拖入 5 帧 → 注意正中帧(player_bank_2)其实就是原 player_ship。

## 音效(`Assets/Audio/`,2026-06-12 新增)

- **资源:** 5 个程序化合成的街机/霓虹风音效(16-bit 单声道 44.1kHz WAV):`sfx_shoot`(激光下扫)、`sfx_hit`(命中短哔)、`sfx_explore`(爆炸,噪声+低频)、`sfx_hurt`(受伤下行双音)、`sfx_gameover`(下行音阶)。另有 `sfx_all_preview.wav` 仅作试听(5 个拼接,勿用于工程)。生成脚本 `outputs_tmp_neon/gen_sfx.py`(numpy 波形合成,要调音色改这里重生成)。
- **脚本:** `Assets/Scripts/System/SfxManager.cs`(单例 + AudioSource 池)。已接到游戏事件:`PlayerShooter.Fire`→PlayShoot、`Bullet` 命中→PlayHit、`Enemy.Die`→PlayExplosion、`PlayerHealth` 受伤→PlayHurt、死亡→PlayGameOver。
- **用法:** 建空物体"SfxManager"挂脚本 → 把 5 个 wav 拖到对应 clip 槽位 → 调 masterVolume。所有调用都做了 `SfxManager.Instance != null` 判空,场景没挂也不报错(只是没声)。
- **注:** 验证脚本配平时,用数 `{}` 的方法对含中文注释/字符串内插的 C# 文件**不可靠**(标点和 `$"{x}"` 会被算进去),应直接读文件确认结构。

## 爆炸特效(`Assets/Sprites/explosion/`,2026-06-12 新增)

- **帧图:** 8 帧霓虹爆炸序列 `explosion_0..7.png`(各 128²,透明):白热核心闪光→膨胀橙/青火球→碎片扩散→消散(第7帧近全透明)。生成脚本 `outputs_tmp_neon/gen_explosion.py`(Pillow,4x 超采样+辉光模糊)。已配 `.meta`(8 个唯一 GUID)。`explosion_preview.png` 仅预览勿用。
- **脚本:** `System/Explosion.cs`(逐帧播放+回收)+ `System/ExplosionManager.cs`(单例+对象池,`SpawnAt(pos)`)。已接 `Enemy.Die()`(回收前用 transform.position 生成)。
- **用法:** 建空物体"ExplosionManager"挂脚本 → frames 数组按 0..7 拖入 8 帧 → 调 fps(建议16)、scale、sortingOrder(应高于敌机让爆炸盖在上面)。判空保护:没挂也不报错只是没特效。

## 开发路线与进度

**当前进度:阶段 7 完成(音效 + 爆炸特效),下一步阶段 8。**

- [x] 阶段1:玩家移动(触屏拖动)
- [x] 阶段2:玩家射击 + 子弹对象池
- [x] 阶段3:敌机生成 / 移动 / 回收
- [x] 阶段4:碰撞 + 基础血量
- [x] 阶段5:玩家无敌帧、死亡触发 Game Over、PlayerHealth 与 UI/GameManager 联动
- [~] 阶段6:击杀计分 —— `Enemy.Die()` 已调用 `GameManager.AddScore(scoreValue)`;计分系统已工作。后续可做连击/分数倍率等扩展
- [x] 阶段7:爆炸特效与音效 —— 5 个程序化音效 + SfxManager;8 帧霓虹爆炸序列 + Explosion/ExplosionManager 池化播放,已接 Enemy.Die
- [ ] **阶段8:把 EnemySpawner 升级为按波次配置的关卡系统**

## 已验证

- 阶段5 实测:分数、血量 HUD 正常显示,GameOver 面板正常弹出(2026-06-12)。UI 文本用 Legacy `UnityEngine.UI.Text`,与 GameHUD 脚本匹配。
- 整套画面已在 Unity 跑通(2026-06-12):霓虹玩家机停屏幕底部居中(开局定位生效)、敌机/子弹/爆炸/音效、滚动星空背景,色差与移动亮带均已修复。
- **待确认:** 运行时 HUD 的 ScoreText/HealthText 若仍显示默认 "New Text"(而非"分数: 0"/"HP: 3/3"),说明 GameHUD 的 `scoreText`/`healthText` 槽位没拖引用 —— 检查挂 GameHUD 物体的 Inspector,把对应 Text 拖进去。

## 下次继续:阶段8 设计草案

把 `EnemySpawner` 的随机刷怪升级为可配置波次关卡:
- 用 `ScriptableObject` 或可序列化数组定义波次(每波敌机数量、阵型、生成间隔、波次间停顿)。
- `EnemySpawner` 按波次表执行(替换当前无限随机协程),保留对象池。
- 接 GameManager:全部波次清完触发通关(或循环/进下一关)。
- 注意:敌机仍走对象池;新增敌机种类时给 Enemy 加可配置 `scoreValue`/`maxHealth`/`speed`。
