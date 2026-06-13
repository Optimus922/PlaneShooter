# PlaneShooter — 项目说明 / 进展记录

> 本文件用于在 AI 助手记忆丢失(如重装/换机器)时快速恢复项目上下文。请持续维护,把重要进展、决策、改动写进来。仅本地保存,不强制提交 git。
> 最近更新:2026-06-13(第1关坦克 boss + 可受击部位系统)

## ⚠️ 助手工作约定(最高优先级,务必遵守)

- **本文件是跨机器的记忆载体。** 所有重要的更新、改动、技术决策,以及与用户交谈中产生的重要内容/约定,都必须及时写进这份 CLAUDE.md,以便换机器或记忆丢失后能完整唤回上下文。
- 不要只把进展留在对话里 —— 对话会丢,文件不会。完成一个子任务、做出非显而易见的决策、踩到坑并解决、或用户表达了偏好时,就更新本文件。
- 每次有实质改动后,顺手更新顶部"最近更新"日期。

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
- **Sorting Order 分层(2026-06-13 定):** 背景 far=-20/near=-10 → 普通敌机=0 → **坦克 boss 本体=5、炮台=6**(坦克是地面单位)→ 敌方子弹=8 → **玩家飞机=10、玩家子弹=10**(飞机/子弹在天上,盖在坦克之上)→ 爆炸=12(最上)→ 炮台头顶血条=50(始终最高)。**经验:加地面型 boss 后发现玩家子弹被坦克盖住,因子弹原 order=0 < 坦克 5/6。规则:天上的东西(飞机/双方子弹/爆炸)order 要高于地面 boss。**

## 已实现脚本(`Assets/Scripts/`)

- **`Player/PlayerController.cs`** — 玩家飞机移动。手指拖动跟随(Kinematic 刚体),平滑 `Lerp`,`ClampToScreen` 限制在屏幕内;支持鼠标测试。可调:`followSpeed`、`keepFingerOffset`(手指与机身偏移,避免手指挡飞机)。
- **`Player/PlayerShooter.cs`** — 自动开火 + 子弹对象池。可调:`fireRate`、池大小。`firePoint` 留空则用飞机自身位置。
- **`Player/Bullet.cs`** — 子弹向上飞,`OnTriggerEnter2D` 命中敌机调用 `enemy.TakeDamage` 后回收;出屏回收。可调:`speed`、`damage`。
- **`Player/PlayerHealth.cs`** — 阶段5 版血量。`TakeDamage` 扣血;受伤后 `invincibleDuration` 无敌帧 + sprite 闪烁(防贴脸连扣);血量变化广播 `OnHealthChanged(current,max)` 事件供 HUD;死亡调用 `GameManager.Instance.OnPlayerDied()` 进入 Game Over。暴露 `CurrentHealth`/`MaxHealth`。
- **`Enemy/Enemy.cs`** — 敌机向下移动、血量/受伤/死亡、撞玩家造成 `contactDamage`、对象池回收。`OnEnable` 重置血量(避免复用到残血)。`Die()` 调用 `GameManager.AddScore(scoreValue)` 计分。**(阶段8新增)** 加 `SetOnReturned(callback)`:离场(击毁/出屏/撞玩家)时回调生成器一次(`counted` 标志防重复计数),供波次存活计数。可调:`speed`、`maxHealth`、`contactDamage`、`scoreValue`。
- **`Enemy/EnemySpawner.cs`** — **阶段8:波次关卡系统**(替换阶段3 无限随机版)。见下"波次关卡系统"。
- **`System/LevelData.cs`** — **阶段8新增** ScriptableObject 关卡数据。见下"波次关卡系统"。
- **`System/GameManager.cs`** — 单例。游戏状态(Playing/GameOver/**Victory**)、分数、结算流程、Restart(重载场景)。事件 `OnScoreChanged`/`OnStateChanged` 供 UI 订阅(解耦)。`OnPlayerDied()` 进 GameOver、`OnLevelCleared()` 进 Victory,都把 `Time.timeScale` 设 0 暂停玩法。
- **`UI/GameHUD.cs`** — 订阅 GameManager/PlayerHealth 事件刷新分数;GameOver **或 Victory** 时显示同一结算面板(`titleText` 显示"游戏结束"/"通关!")+ 最终分数 + 重新开始按钮。血量改用 HealthBar(`healthText` 已弃用留空)。用 Legacy `UnityEngine.UI.Text`。引用全 SerializeField。
- **`System/ScrollingBackground.cs`** — 见下"滚动背景"。
- **`System/SfxManager.cs`** — 单例 + AudioSource 池(voiceCount 个声部轮流 PlayOneShot,连射不打断)。语义化方法 `PlayShoot/PlayHit/PlayExplosion/PlayHurt/PlayGameOver`;可调 masterVolume、sfxEnabled。clip 在 Inspector 拖入。
- **`System/Explosion.cs`** — 单个爆炸:对象池取出后逐帧播序列帧,播完回收。`Init(frames,fps,sortingOrder)` 由管理器注入。
- **`System/ExplosionManager.cs`** — 单例 + 对象池。`SpawnAt(pos)` 在指定位置播一次爆炸。Inspector 配 frames(explosion_0..7)、fps、scale、sortingOrder。
- **`Player/PlayerBanking.cs`** — 见下"玩家机倾斜帧"。
- **`UI/HealthBar.cs`** — 见下"霓虹血条 HUD"。
- **`UI/NeonText.cs`** — 见下"霓虹文字样式"。
- **`UI/NeonButton.cs`** — 见下"霓虹按钮反馈"。

## 美术资源(`Assets/Sprites/`)

- 4 张 sprite:`player_ship`(256²)、`enemy_ship`(256²)、`player_bullet`(64×128)、`enemy_bullet`(64²),透明背景。
- **风格:科幻霓虹**(2026-06-12 由扁平低多边形升级而来)。玩家机青/蓝霓虹+引擎喷焰,敌机品红 V 形+红色能量核心,子弹为发光能量束/能量球。暗色/星空背景下最出彩。
- 生成方式:Pillow 程序化绘制,脚本在项目根 `outputs_tmp_neon/`(`neon_common.py` + `gen_ships.py` + `gen_bullets.py`,4x 超采样)。要再生成/调整风格时复用这些脚本。
- 旧的扁平风原图备份在 `Sprites/_backup_flat/`,可回退。
- 替换时只改了 png 内容,未动 `.meta`,所以 Unity 导入设置(PPU/Pivot 等)保留,引用自动更新。

## 滚动背景(2026-06-12 新增)

- **贴图:** `Sprites/bg_stars_far.png` + `bg_stars_near.png`(均 1024²,纵向无缝平铺)。远景=深空底+星云+密集小星(不透明);近景=稀疏亮星+霓虹彩星(透明,叠在上层)。生成脚本:`outputs_tmp_neon/gen_starfield.py`(星点环绕补画、星云环绕模糊保证无缝)。已配好 `.meta`(Sprite 模式 / PPU 100)。
- **脚本:** `Assets/Scripts/System/ScrollingBackground.cs`。挂到一个空物体上,在 Inspector 配置多层(每层 sprite + scrollSpeed + sortingOrder),远景慢、近景快形成视差。每层用「按屏幕高度自动算出的若干张」贴图首尾相接、leapfrog 回顶实现无限循环;运行时自动按相机宽度缩放铺满屏幕。**(2026-06-13:贴图数量由原来固定 2 张改为动态计算,修复竖屏漏底,见下方"已修复的坑"。)**
- **用法:** 建空物体"Background"挂脚本 → layers 设 2 层(far: speed 小、order -20;near: speed 大、order -10)→ 背景 sorting order 要低于飞机/子弹。
- **已修复的坑(2026-06-12):**
  - *Tint 透明黑*:`Layer.tint = Color.white` 的 C# 初始值对 Inspector 新增数组元素**不生效**,Unity 填的是 `(0,0,0,0)` 全透明 → tile 看不见。新增层后必须手动把 Tint 设白、Alpha=255。
  - *开局下方空白*:原来 tileA 中心放屏幕中心,贴图按宽度缩放后高度不够覆盖下半屏。已改为 tileA 底边对齐屏幕底部、tileB 接其上方,从底往上铺满。
  - *卷动色差*:`gen_starfield.py` 原来 4 团星云用了蓝/紫/品红/青 4 种色相,纵向有色相漂移,循环卷动时表现为"变色"。已统一为同一蓝青色系(低浓度),纵向色调均匀(顶/底段色差 ΔRGB≈0)。
  - *移动亮带(二次修复)*:统一色相后仍残留"中间亮、上下暗"的亮度分布,亮区随卷动周期性扫过屏幕(经过 tile 接缝暗区时"消失"),表现为上半浅蓝、滚到中间变深蓝。已把星云 alpha 大幅压淡(~40→~15)、底色略提(6,8,20→8,12,28),纵向蓝通道最亮-最暗差从 ~11 降到 ~3,通体均匀深蓝。**经验:无缝平铺不仅要接缝连续,整张图的大尺度亮度/色相也要均匀,否则循环时会看到移动的亮/色带。**
  - *竖屏上半屏露空白/星星变少(2026-06-13 修复)*:`Free Aspect` 正常、切到 `Phone 1080x1920` 竖屏时上半屏周期性出现一段浅蓝色差、星星变少。**根因:** 原脚本每层硬编码只用 2 张贴图(tileA/tileB)首尾相接,而贴图按"屏幕宽度"等比缩放。1024² 正方形星图在又窄又高的竖屏(宽高比 0.5625)下,按宽度缩放后单张世界高度 ≈5.6 < 相机高度 10,**两张盖不满竖屏**,leapfrog 回收瞬间顶部露出相机纯色背景(浅蓝)并随卷动周期扫过。Free Aspect 窗口偏宽、屏幕没那么高,两张刚好够,所以不复现。**修复:** 把固定 2 张改为按相机高度自动算所需张数 `ceil(camHeight/tileHeight)+1`(至少 2),用 `List<Transform> tiles` 存多张,回收时接到"当前最高一张"的正上方。保持按宽度缩放(星星密度与 Free Aspect 一致,不会被放大变稀)。**经验:无限滚动背景的贴图数量必须按目标屏幕高度动态计算,不能硬编码,否则换分辨率/宽高比就漏底。**

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

## 霓虹血条 HUD(`Assets/Sprites/ui/`,2026-06-13 新增)

- **背景:** 原血量是纯数字 Text("HP: 3/3"),用户嫌太 low,改成科幻霓虹渐变血条。
- **贴图:** `Sprites/ui/hp_track.png`(底框/轨道,暗底胶囊+青色霓虹边+外发光)、`hp_fill.png`(填充,近白胶囊+顶部高光+纵向体积渐变,运行时被 tint 成血色)。均 512×96 横向条,RGBA。生成脚本 `outputs_tmp_neon/gen_healthbar.py`(Pillow 4x 超采样)。已配 `.meta`(各自独立 GUID,见下)。
- **脚本:** `Assets/Scripts/UI/HealthBar.cs`。订阅 `PlayerHealth.OnHealthChanged(current,max)`,按比例驱动血条。**关键设计:为减少手改场景 YAML 的风险,脚本在运行时自己创建 Track + Fill 两个子 Image** —— 场景里只挂一个 HealthBar 组件、拖两张 sprite 即可,不用手写 Image 序列化。Fill 用 `Image.Type.Filled` + Horizontal,`fillAmount=current/max` 左右裁切;颜色按血量在 青绿(满 fullColor)→黄(midColor)→红(濒死 lowColor)双段渐变;`lerpSpeed` 控制填充平滑过渡(用 `unscaledDeltaTime`,GameOver timeScale=0 时也能补完动画)。PlayerHealth 留空自动 FindObjectOfType。
- **场景接线(已在 SampleScene.unity 改好):** 把原 `HealthText` 物体复用为血条宿主 —— 删掉它的 Text 组件+CanvasRenderer,改名 `HealthBar`,挂 HealthBar 组件并序列化好两张 sprite 引用与颜色。同时把 GameHUD 的 `healthText` 槽位清空(`fileID: 0`,GameHUD 有判空,不再用数字血量)。GameHUD 仍管分数与 GameOver 面板。
- **资源 GUID:** HealthBar.cs=`780b1581bccf4672a3a786e69200a6cc`;hp_track.png=`a89bdc991752443e9616066e59671c84`;hp_fill.png=`5829e4d72d024f9cafc6ade5f4f546ec`;Sprites/ui 文件夹=`eab49669d223450d94f2841b463a5569`。
- **可调:** barSize、fillPadding(边框露出量)、三档颜色、lerpSpeed。要改血条形状/配色改 `gen_healthbar.py` 重生成贴图即可。
- **经验:** 给 UI 加自建子物体的组件时,让脚本在 `Awake/Start` 运行时构建子 Image,比手写场景 YAML 里的多个 Image 节点稳得多 —— 场景只需挂一个组件 + 拖 sprite。

## 霓虹文字样式(`UI/NeonText.cs`,2026-06-13 新增)

- **背景:** 用户要把分数和结算界面也统一成霓虹/街机风。分数仍用数字,但字体风格要"街机感"。**注:真正的像素字体需要生成位图字体资产(工作量大、接线有风险),与用户确认后选择"霓虹发光样式"路线 —— 保留内置 Arial,但加发光描边+投影+加粗+霓虹配色。**
- **脚本:** `Assets/Scripts/UI/NeonText.cs`。挂在任意 Legacy `UnityEngine.UI.Text` 同物体上,`Awake` 运行时自动给该 Text 加:多层 `Outline`(距离递增、alpha 递减 → 外发光层次)+ 一层 `Shadow`(投影)+ 加粗 + 主色。**与 HealthBar 同思路:脚本运行时构建 effect,场景只需挂组件+调颜色参数,不用手写多个 effect 的 YAML。** 不接管文本内容(分数仍由 GameHUD 赋值)。可调:textColor、bold、glowColor、glowLayers(建议2~3)、glowDistance、dropShadow、shadowColor/Offset。
- **场景接线(SampleScene.unity 已改好):** 给 3 个文本挂了 NeonText —— ScoreText(青色霓虹+蓝发光,comp 1234477432)、FinalScoreText(黄色+橙发光,comp 111557174)、RestartButton 的"重新开始"Text(白字+青发光,comp 337300721)。另把 GameOverPanel 背景改为深蓝半透明(0.031,0.047,0.110,0.85)、重开按钮底色改为深蓝青(0.059,0.125,0.216,0.95),让霓虹字更突出。
- **资源 GUID:** NeonText.cs=`1586d4f205934d33bb66b5b6c5c3bcd2`。
- **若日后要真·像素街机字体:** 用 Pillow 生成位图字符图集 + Unity custom Font 资产(.fontsettings + 材质),再把各 Text 的 `m_FontData.m_Font` 指过去即可,NeonText 仍可叠加发光。

## 霓虹按钮反馈 + HUD 对齐修复(`UI/NeonButton.cs`,2026-06-13 新增)

- **背景:** 用户反馈两个问题 ——(1)分数和血条没对齐;(2)结算页"重新开始"按钮不像按钮、按下没反馈。
- **对齐修复:** 分数 ScoreText 的文本框高度原是 160(文字垂直居中 → 偏低),血条被 HealthBar 脚本运行时改成高 60。已把 ScoreText 的 `m_SizeDelta.y` 从 160 改为 **60**,两者同高同顶(都锚左上/右上、距顶 30),水平对齐。
- **按钮问题根因:** 按钮原用 Unity Button 的 ColorTint 过渡(`m_Transition: 1`),而底色被改成深蓝。ColorTint 是"乘算",深底色乘以按下色只会更暗、几乎看不出变化;移动端又无 hover;且按钮没边框,不像可点。
- **脚本:** `Assets/Scripts/UI/NeonButton.cs`。挂在 Button 物体上,`Awake` 给背景 Image 加霓虹 Outline(让它像按钮);实现 `IPointerDown/Up/Exit`,**按下瞬时**缩放(pressedScale 0.92)+ 背景提亮(pressedBrighten 1.4),抬起/移出恢复。**关键:用瞬时切换而非 deltaTime 动画 —— 结算界面 GameOver 时 `Time.timeScale=0`,基于时间的动画会停,瞬时切换始终可见。** 同时把 Button 的 `m_Transition` 改为 0(None),避免 ColorTint 与 NeonButton 抢着改颜色。
- **场景接线:** RestartButton(1526361921)挂 NeonButton(comp 1526361926),targetImage 指其背景 Image(1526361924)。依赖场景里已有的 EventSystem(StandaloneInputModule)才能收到指针事件 —— 本场景已有。
- **资源 GUID:** NeonButton.cs=`552b4b46acab4089920c5a6e69833f20`。
- **经验:** 深色底的 UGUI Button 别用默认 ColorTint 过渡(乘算在深底上几乎无效),改用脚本驱动缩放/提亮 + 描边,反馈更明确;且结算等暂停界面的按钮反馈必须避开 deltaTime。

## 波次关卡系统(阶段8,2026-06-13 完成)

- **目标:** 把阶段3 的无限随机刷怪升级为按波次配置的关卡。设计决策(与用户确认):用 **ScriptableObject** 配关卡、**击毁全部才过波**、**全清触发通关胜利面板**。
- **`System/LevelData.cs`**(ScriptableObject,`[CreateAssetMenu] PlaneShooter/Level Data`):`levelName` + `WaveData[] waves`。每波 `WaveData`:`enemyCount`(数量)、`spawnInterval`(本波内每架间隔)、`delayAfterClear`(本波清空后停顿)、`enemyOverride`(本波敌机预制体,留空用 spawner 默认 → 便于以后混入不同敌机种类)。
- **`Enemy/EnemySpawner.cs`**(重写):`Start` 跑 `RunCampaign` 协程,**按顺序执行多个 LevelData(每个 = 一关)**;每关内 `RunLevel` 顺序执行 `waves`。每波:逐架 `SpawnOne` 生成,然后 `while (aliveCount>0)` 等到本波敌机全部离场才过波,波间停 `delayAfterClear`;每关前后插入横幅(见下"关卡横幅");全部关卡跑完调 `GameManager.OnLevelCleared()`。**对象池按预制体分桶**(`Dictionary<Enemy, IObjectPool<Enemy>>`,支持多敌机种类)。敌机离场用 `Enemy.SetOnReturned` 回调减 `aliveCount`。任何时候 GameManager 非 Playing 即中止刷怪。
- **存活计数机制:** `Enemy` 加 `SetOnReturned(cb)` + `counted` 标志,`ReturnToPool` 时回调一次(击毁/出屏/撞玩家都算离场),避免出屏逃逸导致永远清不掉本波而卡死。`OnEnable` 重置 `counted`,池复用安全。
- **`GameManager`:** `GameState` 加 `Victory`;`OnLevelCleared()` 进 Victory + timeScale=0。`OnPlayerDied()` 判空条件由 `==GameOver` 改为 `!=Playing`(避免 Victory 后又被死亡覆盖)。
- **`GameHUD`:** GameOver 与 Victory **复用同一结算面板**;新增 `titleText`,显示"通关!"(Victory)或"游戏结束"(GameOver)。
- **场景/资产接线(已完成):** `Assets/Levels/Level_01~03.asset`(关卡递增)接到 spawner 的 `levels` 数组(见下"关卡横幅")。结算面板新增 `TitleText` 子物体(品红霓虹,挂 NeonText)接到 GameHUD 的 `titleText`。spawner 旧的 `spawnInterval` 字段已从场景移除。
- **资源 GUID:** LevelData.cs=`f8cadbfd562646239fe666b9f1a348c3`;Level_01=`45091c63b79840899551514a52b3919d`、Level_02=`48daea0ed0bf478aab4470058fe1ea31`、Level_03=`c9b9ed0fb17c4f319eb4ba828388e925`(各主对象 fileID 11400000,引用 type:2);TitleText 物体=663994749/Text=663994750/NeonText=663994753。
- **经验:** Unity 沙箱里无 C# 编译器,改完只能手动核对交叉引用(`SetOnReturned`/`OnLevelCleared`/`GameState.Victory` 等),实际编译/运行需用户在编辑器验证。

## 关卡横幅 + 多关串联(2026-06-13 新增)

- **需求:** 关卡设计更明显 —— 每关开始屏幕中央明确提示「第 X 关」2 秒,打完提示「第 X 关通过」2 秒。设计决策(与用户确认):**多个 LevelData = 多关**(非"每波一关")、提示期间**暂停刷怪**。
- **EnemySpawner:** `levelData`(单关)→ `levels`(`LevelData[]` 多关)+ `bannerDuration`(默认 2)。`RunCampaign` 串关:每关前 `ShowBanner("第 X 关")` 等 2 秒、跑完 `RunLevel` 后 `ShowBanner("第 X 关通过")` 等 2 秒,全部跑完才 `OnLevelCleared`。**(2026-06-13 优化)** boss 出场前先 `ShowBanner("警告!Boss 来袭", bossWarningDuration)`;「第X关通过」与下一关「第X关」之间留 `betweenLevelsGap`(默认1秒)间隔,避免两段闪烁紧贴。`ShowBanner` 改为 `(text, duration)` 双参,各横幅可用不同时长。新字段在场景 spawner 已设:bossWarningDuration=2、betweenLevelsGap=1。
- **关键设计 —— 横幅不冻结 timeScale:** 横幅出现时屏幕上本就没有敌机(关首/清屏后),所以只让 spawner 协程这 2 秒不刷怪即可,**不设 `Time.timeScale=0`**(那会连玩家移动/背景滚动也停掉),玩家此刻仍可移动,体验更好。这就是"暂停刷怪"的真实含义。
- **首帧订阅时序坑:** `RunCampaign` 开头 `yield return null` 等一帧,确保 `GameHUD.Start` 已订阅 `OnBanner` 再发首条「第1关」横幅(Unity Start 执行顺序不确定,否则首条横幅会丢)。
- **横幅事件链:** `GameManager` 加 `event Action<string,float> OnBanner` + `ShowBanner(text,dur)`;`GameHUD` 订阅 `OnBanner` → `BannerRoutine` 把居中大字 `bannerText` 显示 dur 秒后隐藏(重入时先 StopCoroutine 旧的)。
- **场景接线:** Canvas 下新增 `BannerText` 物体(居中、字号100、青色霓虹挂 NeonText、RaycastTarget 关、overflow 设 Overflow 防裁切),接到 GameHUD 的 `bannerText`。GameHUD.Start 里默认隐藏。新 fileID 段 780000001~780000006,已确认无冲突。
- **闪烁(2026-06-13 追加):** `UI/UITextFlash.cs`(GUID `6172cabc6e804ca39ae74ef94fbd33a8`)挂到 BannerText(comp 780000006),**方波硬闪烁**:每隔 `onDuration` 秒可见、`offDuration` 秒隐藏,交替切换(用户要的是显示/不显示切换,不是渐变呼吸)。通过开关 Text 的 `enabled` 实现 —— NeonText 的 Outline/Shadow 随同一 CanvasRenderer 绘制,会和字一起整体显隐。用 `unscaledDeltaTime`。可调 `onDuration`(默认0.35)、`offDuration`(0.2)。OnEnable 从可见起步、OnDisable 还原 enabled=true(防复用残留隐藏)。
- **经验:** 用事件驱动 UI 横幅时注意发布方(spawner)与订阅方(HUD)的 Start 时序,首帧 yield 是稳妥做法;"暂停"类需求先想清楚到底要停什么(刷怪 vs 整个玩法),别无脑 timeScale=0。

## 退出按钮(2026-06-13 新增)

- **需求:** 通关页面加退出按钮,点击退出游戏。**因通关与失败共用同一结算面板,退出按钮两种情况都会显示**(已与场景结构一致,通常也合理)。
- **`GameManager.QuitGame()`:** 编辑器里 `UnityEditor.EditorApplication.isPlaying=false`(`#if UNITY_EDITOR`),打包后 `Application.Quit()`。
- **`GameHUD`:** 加 `quitButton` 引用 + `OnQuitClicked` → `QuitGame()`,Start 订阅、OnDestroy 退订(与 restartButton 同样写法)。
- **场景接线:** 复制 RestartButton 结构建 QuitButton(GO 770000001,红色霓虹描边/NeonButton/Button transition=None,子 Text"退出"挂 NeonText)。重排结算面板按钮:RestartButton 上移到 y=-40,QuitButton 在 y=-220。接到 GameHUD 的 `quitButton`(770000004)。所有新 fileID 在 770000001~770000014 段,已确认无冲突。
- **经验:** 复制 UGUI 按钮整套(GO+RectTransform+Image+Button+CanvasRenderer+NeonButton+子 Text+NeonText)时,新 fileID 要先 grep 确认不冲突;按钮的 Button.m_TargetGraphic、NeonButton.targetImage、子 Text 的 m_Father 都要指向新 ID,别漏改残留旧引用。

## Boss 系统 + 第1关坦克(阶段9,2026-06-13)

- **需求:** 每关一个 boss(第1关坦克、第2关飞机、第3关人形机器人,后两个之后再设计)。坦克有 1 主炮 + 2 副炮,**只有炮台能被击中**(本体不可受击),主炮血50、副炮血20,全部击破算 boss 死。设计决策(与用户确认):**波次清完后 boss 出场、boss 会向玩家开火、炮台头顶有血条、撞本体不扣血、主副炮独立(打哪个都行)**。
- **可受击接口 `Enemy/IDamageable.cs`**(GUID `a4e45a81b38a047a182c4a6e175b0710`):`void TakeDamage(int)`。**Bullet 命中改为找 `IDamageable`** 而非具体 `Enemy`(`Enemy` 现在 implements 它),这样普通敌机和 boss 炮台共用同一套命中逻辑。
- **`Enemy/BossPart.cs`**(GUID `474e3e55cb30b4e6ca5a471591b1bfa8`):炮台部位,implements IDamageable。被击中闪白、血量归零爆炸+隐藏+停用碰撞体并回调 boss。**头顶世界空间血条用运行时构建的 1x1 白 SpriteRenderer**(底+填充,缩放 FillAnchor.x 裁切,不依赖贴图资源)。主炮/副炮共用此脚本,血量/血条尺寸/颜色 Inspector 配。
- **`Enemy/EnemyBullet.cs`**(GUID `fe772fb7ed3e47c2bd471c5ca18ad667`):敌方子弹,对象池,`Launch(dir)` 设方向,命中 `PlayerHealth` 扣血,出屏回收。放在 **Enemy 层(8)**(与 Player 层6碰撞已启用)。预制体 `Assets/Prefabs/EnemyBullet.prefab`(GUID `1d5a5b0dc19049599575135a24e8e3fe`,复用 enemy_bullet 贴图染红、触发碰撞体、Kinematic 刚体)。
- **`Enemy/TankBoss.cs`**(GUID `b470770a1bd14e0e8122760b99400c8f`):本体。进场→上半屏左右徘徊→每个存活炮台按 `fireInterval` 朝玩家发 EnemyBullet(自带子弹对象池)。`parts[]` 全击破→`Defeat()`(计分500+多处爆炸+`onDefeated` 回调)。`Init(callback)` 由 spawner 注入。本体不挂可受击碰撞体→只有炮台能打。
- **美术:** `gen_tank.py`(在 outputs_tmp_neon/)程序化生成 3 件:`tank_body.png`(军绿履带车体,GUID `f5162c79e52a48d1b67b4dd8cb70bb55`)、`tank_main_gun.png`(红霓虹大炮塔,GUID `736e8a8b6a244c78833ac641ecba0e64`)、`tank_sub_gun.png`(青霓虹小炮塔,GUID `45e151642cda48e6aa39149a654abd9f`)。炮塔单独成图、锚点居中,Unity 里作为子物体叠在车体上。
- **预制体 `Assets/Prefabs/TankBoss.prefab`**(GUID `859e0016b4ac41729e560d9c5b5486b0`):root(车体 SpriteRenderer + TankBoss 脚本)+ 3 子物体炮台(MainGun y=0.5 血50;SubGunLeft x=-0.95、SubGunRight x=0.95,各血20、scale0.8)。炮台都在 Enemy 层、触发 BoxCollider2D + Kinematic 刚体 + BossPart。fileID 用 3002xxx 段,EnemyBullet 用 3001xxx 段。
- **`LevelData` 加 `bossPrefab` 字段**;**`EnemySpawner` 波次清完后**若 `level.bossPrefab!=null` 则 `RunBoss`:在屏幕上方生成 boss、`boss.Init(()=>defeated=true)`、等到 defeated 再放「第X关通过」横幅。Level_01.asset 已接坦克 boss(Level_02/03 暂无,留待飞机/机器人设计)。
- **经验:** 多部位 boss 的"只有某些部件可受击"靠"只给可受击部件挂碰撞体 + IDamageable"实现,本体不挂;命中逻辑解耦成接口后,加新可受击物零成本。Unity 沙箱无编译器,手写预制体后靠 grep 核对 fileID 唯一性、父子引用、脚本/贴图 GUID,实际编译运行需用户在编辑器验证。

## 开发路线与进度

**当前进度:阶段 9 进行中(boss 系统)—— 第1关坦克 boss 已实现,飞机/机器人 boss 待设计。**

- [x] 阶段1:玩家移动(触屏拖动)
- [x] 阶段2:玩家射击 + 子弹对象池
- [x] 阶段3:敌机生成 / 移动 / 回收
- [x] 阶段4:碰撞 + 基础血量
- [x] 阶段5:玩家无敌帧、死亡触发 Game Over、PlayerHealth 与 UI/GameManager 联动
- [~] 阶段6:击杀计分 —— `Enemy.Die()` 已调用 `GameManager.AddScore(scoreValue)`;计分系统已工作。后续可做连击/分数倍率等扩展
- [x] 阶段7:爆炸特效与音效 —— 5 个程序化音效 + SfxManager;8 帧霓虹爆炸序列 + Explosion/ExplosionManager 池化播放,已接 Enemy.Die
- [x] **阶段8:波次关卡系统 + 多关串联** —— LevelData ScriptableObject + EnemySpawner 波次执行(击毁全部过波)+ 多关 levels 数组串联 + 关卡横幅(第X关/第X关通过)+ GameManager Victory + 通关/退出面板。3 关(Level_01~03 递增)已接线。
- [~] **阶段9:Boss 系统** —— IDamageable 接口 + BossPart 可受击部位 + EnemyBullet 敌方子弹 + TankBoss(第1关坦克,1主2副炮)+ LevelData.bossPrefab + spawner RunBoss。第1关坦克已接线。**待办:第2关飞机 boss、第3关人形机器人 boss。**
- [ ] 下一步候选:飞机/机器人 boss、连击计分、关卡选择菜单

## 已验证

- 阶段5 实测:分数、血量 HUD 正常显示,GameOver 面板正常弹出(2026-06-12)。UI 文本用 Legacy `UnityEngine.UI.Text`,与 GameHUD 脚本匹配。
- **HUD 分数/血量不更新(2026-06-13 修复)**:运行时分数/血量一直显示 "New Text" 不刷新,而 GameOver 面板正常。**根因:** 场景 `SampleScene.unity` 里 GameHUD 组件的 `scoreText`/`healthText` 两个槽位是空的(`fileID: 0`)—— 设计时漏拖引用。GameHUD 里有 `if (scoreText != null)` 判空,所以事件照常触发但没 Text 被更新。GameOver 面板的三个引用(gameOverPanel/finalScoreText/restartButton)都拖好了所以正常。**修复:** 直接在场景文件里把 GameHUD 的 `scoreText` 绑到 ScoreText 物体的 Text 组件(fileID 1234477430)、`healthText` 绑到 HealthText 物体的 Text 组件(fileID 526461092)。`playerHealth` 仍留空(Start 会自动 FindObjectOfType,无碍)。**经验:CLAUDE.md 里此前标注的"待确认"项被坐实 —— 排查 HUD 不刷新优先查 Inspector 引用是否漏拖,而非脚本逻辑。**
- 整套画面已在 Unity 跑通(2026-06-12):霓虹玩家机停屏幕底部居中(开局定位生效)、敌机/子弹/爆炸/音效、滚动星空背景,色差与移动亮带均已修复。
- **已解决(2026-06-13):** 运行时 HUD 的 ScoreText/HealthText 显示默认 "New Text" 不更新的问题 —— 正是 GameHUD 的 `scoreText`/`healthText` 槽位没拖引用。已直接在场景文件补上绑定,详见上方"HUD 分数/血量不更新"。

## 下次继续:阶段8 设计草案

把 `EnemySpawner` 的随机刷怪升级为可配置波次关卡:
- 用 `ScriptableObject` 或可序列化数组定义波次(每波敌机数量、阵型、生成间隔、波次间停顿)。
- `EnemySpawner` 按波次表执行(替换当前无限随机协程),保留对象池。
- 接 GameManager:全部波次清完触发通关(或循环/进下一关)。
- 注意:敌机仍走对象池;新增敌机种类时给 Enemy 加可配置 `scoreValue`/`maxHealth`/`speed`。
