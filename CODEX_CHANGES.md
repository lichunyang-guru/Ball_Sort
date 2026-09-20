## 2026-09-20 源管在跟随段出发后立即释放

**原因**
上一轮只放开接收目标，源管仍被每个在途段和整批等待流程占用，必须等待动画结束才能重新操作。

**修改**
- 源管只在准备自动跟随段时占用，所有段出发后立即释放，不再等待落地。
- 在途段仅占用目标管；删除逐段源管占用和整批动画完成等待状态。
- 保留目标容量预占和接收中水柱的选取保护。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
无需配置；退出运行模式，等待脚本编译后重新运行验证。

**注意**
运行时 C# 编译通过。CLI 与 MCP 均无法连接当前编辑器，未完成实机点击复现；用户报告的独立试管操作受阻尚不能确认完全解决。未提交 Git。

## 2026-09-20 允许其他水柱并行倒入接收中的试管

**原因**
正在接收水柱的试管被当作不可操作目标，导致其他水柱抬起后无法向它移动。

**修改**
- 接收中的试管允许继续接收其他源管的水柱，沿用模型的颜色检查和即时容量预占。
- 自动跟随在等待后和抬起后重新检查目标容量；空间不足时放回抬起段并停止跟随。
- 正在出水的试管仍由当前跟随流程占用，接收中的在途水柱仍不能被再次选中取走。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
无。

**注意**
使用项目 Unity 引用程序集编译全部 33 个运行时源文件通过；未进行 Unity 实际游玩或 Luna 构建验证。未提交 Git。

## 2026-09-18 跟随改为间隔出发、允许同时在途

**原因**
用户要求调整后段开始跟随的间隔，而不是加快后段速度或等待前段落位。

**修改**
- 移除 Follow Speed Multiplier，新增 Follow Interval，默认 0.12 秒：前段出发后到下一段开始抬起的等待时间。
- 各段移动协程独立运行，后段无需等待前段落位；抬起、飞行、下落及展开保持原有速度。
- 逐段预占目标容量，整个跟随过程保持双管锁定；全部在途段结束后才合并显示，避免中途重排或超容量。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
刷新后在 GameController → Move Animation → Follow Interval 调整。

**注意**
Unity 引用程序集编译通过；尚未实际游玩或重新打 Luna 包验证并发动画。未提交 Git。

## 2026-09-18 配置同色水柱跟随速度

**原因**
用户希望后续水柱跟随更快，并能够配置节奏。

**修改**
- GameController 的 Move Animation 新增 Follow Speed Multiplier，默认 1.5。
- 仅后续同色水柱的抬起、移动、下落和展开使用该倍率，首段保持原速；继续逐段落位后跟随。
- 使用 DOTween Sequence.timeScale 统一加速，临近落点换色回调随动画同步。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
刷新后在 GameController → Move Animation → Follow Speed Multiplier 调整，1 为原速，2 为两倍速。

**注意**
未提交 Git。

## 2026-09-18 单段抬起及同色水柱依次跟随

**原因**
同色合并水柱需要只抬起顶部高度 2，并在目标空间允许时让下方同色段跟随；水滴特效需要缩小。

**修改**
- 每次选中和转移最多高度 2，下方同色部分保持显示；每段落位后再抬起下一段，同色且目标能容纳整段才继续。
- 整个跟随过程锁定源管和目标管，逐段更新模型和显示；删除整组溢出返回分支，保留取消选中放回及临近落点换色。
- 通过 Unity 原生预制体 API 将 water-up、water-down 的 localScale 设置为 (0.6, 0.6, 0.6)。

**主要文件**
- `Assets/Scripts/Game/TubeModel.cs`
- `Assets/Scripts/Game/TubeView.cs`
- `Assets/Scripts/Game/GameController.cs`
- `Assets/prefabs/Water/body.prefab`

**Unity 编辑器操作**
预制体缩放已由 Unity 保存并复查，一次性脚本已移除。重新运行游戏。

**注意**
C# 编译通过；模型验证 6→2+2+2、只剩 2 空间时源管保留 4、不同颜色停止跟随、空间不足整段不转移。尚未完整游玩或 Luna 构建验证新动画。未提交 Git。

## 2026-09-18 修复 Luna 开场水流与 ShootRoot 退出

**原因**
在实际 Luna 浏览器包中复现：动态创建的 LineRenderer 颜色渐变为空，设置 startColor 抛出 setKey 空引用，Start 中断导致 ShootRoot 上移未执行。宽度曲线也为空；初始化后另发现世界坐标点叠加了绘制对象的位置偏移。

**修改**
- ShootBall 显式初始化颜色渐变和恒定宽度曲线。
- 保留原 DOPath 采样，采样完成后将 LineRenderer 原点归零，避免 Luna 中水流路径整体偏移。
- 保留发射时长、轨迹、退出动画和关卡入场配置。

**主要文件**
- `Assets/Scripts/Game/ShootBall.cs`

**Unity 编辑器操作**
刷新脚本后重新打 Luna 包。

**注意**
Unity 引用程序集编译通过；在本地 Luna 已构建包内临时加入等价 JS 修复，实际观察到连续水流贴合路径、ShootRoot 退出和关卡入场，修复运行无新增异常。诊断时曾临时延长发射时间，源配置未变；诊断 JS 已恢复，仍需重新构建正式包。未提交 Git。

## 2026-09-18 修复 Luna 不支持 SpriteRenderer.localBounds

**原因**
Luna 编译环境未提供 SpriteRenderer.localBounds，导致 CS1061，普通 Unity 编译无法发现此兼容性差异。

**修改**
- 移除 BallView 和 WaterBodyTopAnchor 内全部四处 localBounds 使用。
- 统一以 Sprite.bounds 计算局部边界；Sliced/Tiled 模式结合 SpriteRenderer.size 缩放边界并保留支点，兼容水平/垂直翻转。
- Body 底部定位、Up 对齐和裁剪面继续使用相同边界计算。

**主要文件**
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`

**Unity 编辑器操作**
刷新后重新执行 Luna 构建。

**注意**
Unity 引用程序集编译通过；检查底部支点在高度 1/2/4/13/16 下保持不变，业务脚本无 localBounds 剩余引用。尚未实际完成 Luna 构建。未提交 Git。

## 2026-09-18 修正杯身、水柱和杯口渲染顺序

**原因**
杯身和杯口位于 tube 排序层，水柱预制体位于 Default 层，跨层优先级导致杯身遮住水柱。

**修改**
- 通过 Unity 原生 Prefab API 将水柱预制体内 Body、Up、发光及下落/抬起特效的 SpriteRenderer 统一到 tube 层。
- 保留原有 Order：杯身 -2，水柱按高度递增，杯口 101；不增加运行时排序逻辑。

**主要文件**
- `Assets/prefabs/Water/body.prefab`

**Unity 编辑器操作**
已由 Unity 保存预制体，重新运行游戏生效。

**注意**
原生编辑器保存后复查 5 个水柱渲染器均为 tube 层；六根杯子均满足杯身 < 水柱（含 Up）< 杯口。一次性脚本已移除。未提交 Git。

## 2026-09-18 试管高度上限改为 16

**原因**
用户将每管总高度限制调整为 16。

**修改**
- MaximumHeight 从 13 改为 16，配置校验、剩余容量、满管判定和内腔高度换算统一使用新上限。
- 同步 Inspector 提示，保留现有各段 Length。

**主要文件**
- `Assets/Scripts/Game/TubeModel.cs`
- `Assets/Scripts/Game/TubeView.cs`
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
重新运行游戏。

**注意**
内腔尺寸不变，现有高度换算会将总高度 16 映射到整管，因此相同 Length 的显示高度会比原来小。已检查容量引用和差异格式；未提交 Git。

## 2026-09-18 下落 70% 路程时换色

**原因**
用户要求提前到下落路程的 70% 改变承接水柱 Up 颜色。

**修改**
- 按 InQuad 缓动换算触发时间为下落时长乘以 √0.7，适用于所有下落入口。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
刷新脚本并重新运行。

**注意**
已检查缓动换算：时间比例约 83.67% 对应路程 70%。未提交 Git。

## 2026-09-18 遮住水柱弧边并延后落点换色

**原因**
Up 与 Body 的圆弧素材不完全一致，仅对齐边界仍会露边；从开始下落就改变承接截面颜色过早。

**修改**
- Up 默认偏移改为 -0.10，新增 Seam Overlap 默认 0.04 世界单位，让 Body 越过截面底边，补足圆弧边缘覆盖；有效高度和容量不变。
- 所有下落入口的换色统一推迟到下落时长的 95%，对应 InQuad 下约 90% 的路程，保留展开完毕及中断恢复颜色的处理。

**主要文件**
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
刷新并重新运行。Body 的 WaterBodyTopAnchor 可调整 Up Vertical Offset 和 Seam Overlap；如保存过偏移覆盖值，手动将其设为 -0.10。

**注意**
C# 编译通过；实际贴图边缘效果尚未在 Unity 中验证。未提交 Git。

## 2026-09-18 加深边缘覆盖及下落期间截面换色

**原因**
水柱交界仍有细边；下落阶段露出的承接截面需要与来水颜色一致。

**修改**
- Up 默认偏移从 -0.06 调整为 -0.08 世界单位，Body 覆盖深度同步增加。
- 开局下落、跨管转移和选中放回期间，将下方实际可见水柱的 Up 临时设为来水的提亮颜色。
- 颜色覆盖由 WaterBodyTopAnchor 统一应用，避免 LateUpdate 覆盖；水柱展开结束或动画被取消时恢复，下落颜色不修改 Body 和关卡数据。

**主要文件**
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
刷新脚本并重新运行；若自行保存过 Up Vertical Offset 的覆盖值，可改为 -0.08。

**注意**
使用 Unity 与项目程序集完成 C# 编译检查；尚未实际运行验证最终颜色过渡与边缘外观。未提交 Git。

## 2026-09-18 水柱覆盖深度同步 Up 偏移

**原因**
Up 下移后，上一段 Body 仍只覆盖半个截面，下移的部分露出一圈颜色。

**修改**
- 覆盖深度统一为“半截面高度减去 Up 垂直偏移”；默认下移 0.06 时，Body 向下额外覆盖 0.06。
- Body 绘制高度、底部定位及展开动画统一读取此深度，保留 Length 和累计堆叠高度。

**主要文件**
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`

**Unity 编辑器操作**
刷新脚本并重新运行游戏。后续调整 Up Vertical Offset 时，覆盖深度同步变化。

**注意**
C# 编译及覆盖边界计算检查通过；尚未在 Unity 镜头下验证最终贴图接缝。未提交 Git。

## 2026-09-18 下移 Up 遮住素材接缝

**原因**
Body 顶部素材留白使 Up 按截面中线对齐后仍出现细缝，直接调整 Transform 会被顶部跟随脚本覆盖。

**修改**
- WaterBodyTopAnchor 增加 Up Vertical Offset，默认向下 0.06 世界单位，可在 Inspector 微调。
- 偏移只影响 Up 贴图位置，保留有效高度、半截面覆盖量、容量与裁剪边界。

**主要文件**
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`

**Unity 编辑器操作**
刷新脚本即可应用默认偏移；可在 Body 的 WaterBodyTopAnchor 组件中调整 Up Vertical Offset，负值越大越向下。

**注意**
C# 编译通过；最终接缝效果需要在当前镜头下查看。未提交 Git。

## 2026-09-18 水柱按截面中线衔接

**原因**
以完整 Sprite 外框堆叠会把圆弧底部也算入有效高度，相邻水柱的截面之间出现空隙。

**修改**
- Length 改为上下截面中线之间的有效高度，Body 向下额外延伸半个 Up 截面用于覆盖，不扣减累计堆叠高度。
- Up 中心跟随 Body 顶部，移除固定的世界偏移；展开动画的表面位置与裁剪平面使用同一截面边界。
- 重叠量由 Up 当前尺寸计算；2+2 与 4 保持相同有效高度，每管容量仍为 13。

**主要文件**
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
刷新脚本后重新运行游戏。

**注意**
Body 的 SpriteRenderer Size Y 现在包含半截面的绘制延伸，因此略大于配置 Length；关卡容量只计算 Length。C# 编译通过；Unity 实际验证六根管的 2+2 与 4 顶部中线齐平、半截面覆盖量、上下显示层级及展开中点全部通过；一次性验证脚本已删除。未提交 Git。

## 2026-09-18 水柱 Length 改为实际高度

**原因**
旧 Length 使用原始 Body 高度加槽位间距换算，数值 3 与 2 的视觉比例不直观。

**修改**
- Length 保留正整数，直接对应 Body SpriteRenderer Size Y，默认 2；每管总高度上限 13。
- 按累计高度堆叠，以内腔遮罩高度确定统一显示比例，取消旧槽位间距换算。
- 开局、移动和余量返回统一使用实际高度；main 场景 21 个条目设为 2，六根管绑定内腔遮罩，Body 预制体 Size Y 设为 2。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`
- `Assets/Scripts/Game/TubeModel.cs`
- `Assets/Scripts/Game/TubeView.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scenes/main.unity`
- `Assets/prefabs/Water/body.prefab`

**Unity 编辑器操作**
已通过 Ball_Sort 编辑器原生 API 保存配置，一次性脚本已删除。重新运行游戏生效。

**注意**
实际 Unity 验证六管中高度 3/2 比例为 1.5、累计堆叠和高度 13 贴合内腔；C# 编译及模型测试通过（13 允许、14 拒绝、部分转移守恒）。尚未完整游玩验证动画。保留原满管同色通关规则，当前默认水量不足以满足原通关要求；未提交 Git。

## 2026-09-18 收紧 Body 底部透明范围

**原因**
Body 图片底部有 53 像素透明留白，完整 Sprite 的选中范围因此延伸到可见水柱下方。

**修改**
- 通过 Unity 原生编辑器脚本裁掉纯透明底边，图片从 85×732 改为 85×679；保留的像素逐像素不变。
- 使用 TextureImporter 保存 Bottom Center 支点及九宫格底边 113→60，保留 GUID 和 Sprite fileID。
- Body 的 Size Y 从 2.5 改为 1.97，本地 Y 上移 0.2901059，保持可见底部、顶部及各整数长度的形状位置。
- 临时编辑器脚本已移除，备份保存在 `/tmp/BallSort-body-trim-backup`。

**主要文件**
- `Assets/prefabs/Water/body.png`
- `Assets/prefabs/Water/body.png.meta`（由 Unity 导入器保存）
- `Assets/prefabs/Water/body.prefab`

**Unity 编辑器操作**
已在 Ball_Sort 编辑器执行并保存；退出后重新运行游戏即可让新实例使用修正后的预制体。

**注意**
Unity 实际执行成功；像素一致性、资源标识及长度 1/2/4/7 的顶部位置检查通过。未提交 Git。

## 2026-09-18 修复 SpriteMask 导致编辑器启动崩溃

**原因**
上次直接写入场景的 SpriteMask 序列化数据不完整，缺少 Unity 默认遮罩材质；加载场景时在 Renderer::GetMaterial → SpriteMask::SetupProperties 中发生原生崩溃。独立 C# 编译无法发现此问题。

**修改**
- 备份故障场景和崩溃日志，仅移除错误的六个遮罩。
- 使用 Unity 2022.3.62f3 的 AddComponent<SpriteMask> 正式创建遮罩，并由 EditorSceneManager 保存完整序列化数据。
- 保留内腔遮罩、按高度显露效果和用户其他修改；一次性恢复脚本已删除。

**主要文件**
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
已自动重新启动项目，无需清空 Library 或重建项目。

**注意**
真实 Unity 批处理创建、保存、再次加载 main 场景成功，退出码为 0；六个遮罩的 Sprite/材质引用正常，两份水相关 Shader 未报告编译错误。恢复日志：`/tmp/BallSort-mask-recovery.log`。未提交 Git。

## 2026-09-18 修复空管入水时底部挤压和越界

**原因**
Body 从零缩放会压扁圆弧底部，Up 保持宽度；玻璃图片本身不裁剪管外像素，底部因而露出。

**修改**
- 六根试管增加独立、固定的 WaterInteriorMask，复用已贴合管内的蜡烛轮廓 Sprite 的 Alpha，不跟随蜡烛完成动画缩放。
- Body 与 Up 使用 Visible Inside Mask；water-up/water-down 飞行帧动画不受遮罩影响。
- Body 保持完整尺寸，用 WaterBodyReveal 材质按液面高度裁剪显露，替代 Y 轴零到一缩放。
- WaterBodyTopAnchor 统一管理显露进度、裁剪平面及 Up 位置；进度为零时隐藏 Up，取消/重置时恢复完整显示。

**主要文件**
- `Assets/Scenes/main.unity`
- `Assets/prefabs/Water/body.prefab`
- `Assets/prefabs/Water/WaterBodyReveal.shader`
- `Assets/prefabs/Water/WaterBodyReveal.mat`
- `Assets/Scripts/Game/WaterTransferVisual.cs`
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`

**Unity 编辑器操作**
已配置场景遮罩、预制体材质及组件引用。等待导入并重新加载 main 场景后运行；无需手动挂载。新增其他试管时需一并复制 WaterInteriorMask。

**注意**
代码使用 Unity/项目程序集独立编译通过；六个遮罩的层级和轮廓变换、场景 fileID 唯一性、Body/Up 遮罩范围、零进度和重置、不同长度及变换下的裁剪平面检查通过。编辑器连接不可用，尚未实际播放验证 SpriteMask 与新 Shader 的渲染效果。未提交 Git。

## 2026-09-18 恢复误删的 Body 预制体

**原因**
`body.prefab` 及其 `.meta` 被删除，main 场景仍引用该水柱预制体。

**修改**
- 从 Git 索引恢复预制体和原始 `.meta`，保留原 GUID 与组件 fileID。

**主要文件**
- `Assets/prefabs/Water/body.prefab`
- `Assets/prefabs/Water/body.prefab.meta`

**Unity 编辑器操作**
等待 Unity 重新导入；场景中的资源引用仍存在，无需重新拖拽。

**注意**
已验证恢复文件与索引一致，且匹配 main 场景引用。未改动其他现有修改，未提交 Git。

## 2026-09-18 将连续水流改接到 ShootRoot 发射路径

**原因**
连续水流需求指的是 ShootRoot 上方小球的 DOPath，而不是下方试管之间的转移；此前接错位置。

**修改**
- 撤回试管转移中的水流接入，恢复整段 DOPath 移动、water-down 下落和 Body 展开。
- ShootBall 直接采样原 DOPath，沿原 Path 节点生成连续水带，替换逐颗小球的对象池发射。
- 水头沿路径延伸，持续出水后水尾收走，保留发射结束后装置退出、GameRoot 进入及引导流程。
- 水流材质引用改配到 main 场景的 ShootRoot。保留原路径类型、0.7 秒路径时长及排序 50；持续出水 2.37 秒对应原 80 颗、0.03 秒间隔的发射跨度。

**主要文件**
- `Assets/Scripts/Game/ShootBall.cs`
- `Assets/Scenes/main.unity`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/GameController.cs`
- `Assets/Scripts/Game/WaterTransferVisual.cs`
- `Assets/prefabs/Water/body.prefab`
- `Assets/prefabs/Water/WaterStream.mat`
- `Assets/prefabs/Water/WaterStream.shader`

**Unity 编辑器操作**
- 已配置 ShootRoot → Shoot Ball → Water Stream，运行 main 即可查看上方发射效果。
- Stream Color 调颜色，Stream Width 调粗细，Emission Duration 调连续出水时间，Move Duration 调走完整条路径的时间。
- WaterStream 材质仍可替换 Water Texture；正式贴图使用 Repeat，并将 Temporary Stripe Strength 设为 0。

**注意**
Unity/DOTween 实际程序集编译通过；实际 ShootBall 路径裁剪方法通过 3003 次时间采样，覆盖短/长出水、连续几何、源头连接、到达终点、纹理相位和尾部清除。场景材质引用与下方试管恢复检查通过。未实际播放或验证 Shader 的平台渲染。未提交 Git。

## 2026-09-18 沿发射路径显示连续水流（试管接入已撤回）

**原因**
水柱转移需要显示沿路径连续流动的图片效果，并为后续替换水贴图保留入口。

**修改**
- 保留现有出管弧线，将弧线和入管段连接成完整路径；由一个 LineRenderer 显示水头到水尾之间的连续水带。
- 水头沿路径延伸，停止出水后水尾收走；出水时长按转移整数长度计算，不再逐格发射。
- 水流抵达后目标整段 Body 逐渐长高，水流末端随液面上升；结束和中断时隐藏水带。
- 新增 URP 无光照水流材质，使用当前 Body 颜色和滚动浅色条纹，支持后续指定 Water Texture；固定材质通过预制体引用，运行时只生成路径几何。
- 保留开局 water-down 和取消选择的返回效果。

**主要文件**
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/GameController.cs`
- `Assets/Scripts/Game/WaterTransferVisual.cs`
- `Assets/prefabs/Water/body.prefab`
- `Assets/prefabs/Water/WaterStream.mat`
- `Assets/prefabs/Water/WaterStream.shader`

**Unity 编辑器操作**
- 已配置 body 预制体的 Stream Material；Stream Width 调粗细，Stream Seconds Per Unit 调每格出水时长。
- WaterStream 材质的 Texture Flow Speed 调条纹流速。以后把水贴图指定给 Water Texture，并将 Temporary Stripe Strength 设为 0；贴图 Wrap Mode 使用 Repeat，纹理沿图片横向流动。

**注意**
代码已使用 Unity/项目程序集独立编译。实际水流时间线及路径裁剪方法通过长度 1、2、4、7 各 1001 个时间采样检查，覆盖抵达前隐藏、连续几何、液面跟随、完整增长及尾部清除；预制体和材质引用已校验。编辑器连接不可用，尚未实际播放或验证 Shader 在 Editor/目标平台上的编译效果。未提交 Git。

## 2026-09-18 开局各试管同时下落

**原因**
开局不再需要从左到右逐管开始下落。

**修改**
- main 场景 GameController 的 Intro Drop Start Interval 从 0.14 改为 0，使各管同时开始。
- 保留同管内 0.24 秒的水柱间隔，以及 water-down 落位后展开整段水柱的效果。

**主要文件**
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
已修改保存的场景配置；若当前打开的场景未刷新，重新加载 main 场景。

**注意**
已检查配置值及现有时间计算。编辑器连接不可用，未实际播放验证。未修改代码或提交 Git。

## 2026-09-18 开局使用 Water-down 下落

**原因**
开局仍直接移动完整 Body，需要先播放 water-down，落位后再出现整段水柱。

**修改**
- 开局改用整段下落序列：隐藏普通水柱、播放 water-down、落位后从底部展开到配置长度。
- 与返回流程共用 CreateDropSequence，首次下落无需已有 water-up 特效即可播放。
- 保留开局的分管与同管间隔、音效节奏，等待整段展开结束后开放交互。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/WaterTransferVisual.cs`

**Unity 编辑器操作**
无需额外配置。Intro Drop Duration 控制开局下落时长，Body 预制体的 Reveal Duration 沿用整段展开时长。

**注意**
Unity 项目程序集独立编译通过；使用实际下落方法及动画替身检查延迟开始、water-down → 落位 → 展开的顺序和下落时长。尚未在 Unity 中实际播放验证。未提交 Git。

## 2026-09-18 水柱整段下落与展开

**原因**
整数长度此前仅在静止显示时合并，转移和返回仍逐格播放动画，导致水柱下落时拆成多段。

**修改**
- 每段只使用一个可见 Body 播放转移、下落和展开，其余容量成员保持隐藏。
- 将完整段高设置到 Body，从目标底部槽位一次展开到对应长度；取消选择也整段返回。
- 容量不足时，转移部分与剩余部分各作为一段播放，保留容量计算规则。
- 删除逐格转移间隔配置；保留用户当前关卡颜色和长度。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
无需重新配置，等待编译后运行验证下落效果。

**注意**
Unity 项目程序集独立编译通过。使用实际控制器方法和动画替身验证长度 1、2、4 的单次转移、容量不足时 2＋3 的拆分返回、长度 3 的取消返回及底部落点。尚未在 Unity 中实际播放验证。未提交 Git。

## 2026-09-18 关卡水柱配置支持整数长度

**原因**
每种颜色需要直接配置水柱长度，不再逐格重复填写颜色。

**修改**
- Level Entries 使用 Columns 列表，每项配置 Color 和正整数 Length；长度按槽位计数，总长不超过容量 7，空管使用空列表。
- 连续同色显示为一段加长的 Body，开局按整段下落，取消选择和转移结束后重新合并显示。
- 保留按格计算容量及拆分转移的逻辑；原场景迁移为每个非空管长度 5＋2，保持原有颜色顺序和数量。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`
- `Assets/Scripts/Game/TubeView.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
等待编译后，在 GameController → Level Entries → Columns 配置颜色和长度；列表顺序为从底部到顶部。

**注意**
独立编译通过；3＋4 配置、非法长度、超容量、空管、连续同色合并、容量不足拆分及场景迁移检查通过。尚未在 Unity 中实际播放验证。未提交 Git。

## 2026-09-18 连续同色水柱共用抬起动画

**原因**
连续同色水柱选中时只需要一个 water-up 抬到管口，不应按格重复显示抬起效果。

**修改**
- 隐藏整段连续同色水柱，仅由最上格显示 water-up，从连续段底部升至管口。
- 按实际槽位顺序决定转移与溢出成员，避免动画位置影响代表水柱的转移顺序。
- 取消选择时，隐藏成员直接恢复展开，不重复播放抬起/下落效果；保留实际格数和容量规则。

**主要文件**
- `Assets/Scripts/Game/GameController.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/WaterTransferVisual.cs`

**Unity 编辑器操作**
无额外配置，等待编译后运行查看。

**注意**
独立编译通过；实际 TubeModel 的连续同色、两格转移、容量不足返回及数量守恒检查通过。尚未在 Unity 中实际播放验证。未提交 Git。

## 2026-09-18 Water-up 尾段改为往返循环

**原因**
首轮结束后需要在时间轴第 30 帧与第 10 帧之间往返播放。

**修改**
- 保留首轮 0～30 帧完整播放。
- 循环片段依次播放 29～10、11～30，承接首轮末帧 30；两个转折点不重复停留。
- 保持 30 FPS，一轮往返 40 帧，约 1.33 秒。

**主要文件**
- `Assets/Controller/water-up-loop.anim`

**Unity 编辑器操作**
等待动画资源重新导入后运行查看。

**注意**
已验证 Sprite 帧序列、关键帧时间和循环接缝；尚未实际播放预览。未提交 Git。

## 2026-09-17 水柱抬起与下落改为帧动画

**原因**
水柱转移需要通过 water-up / water-down 呈现，并在落位后从底部展开。

**修改**
- 选中时隐藏 Body/Up，改用 water-up 从水柱底部随位置升起。
- Animator 先完整播放 water-up，再无混合切换到原时间轴 20～30 帧的循环片段。
- 到目标管口后切换 water-down，下落动画速度匹配实际下落时长；落位后隐藏特效，Body 用底部支点展开，Up 保持自身尺寸并跟随。
- WaterTransferVisual 管理视觉切换，特效与普通视觉为同级，颜色及排序沿用水柱。
- 取消选择、溢出返回也使用下落/展开流程；中断、重建关卡和对象销毁时清理对应 Tween。

**主要文件**
- `Assets/Scripts/Game/WaterTransferVisual.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/GameController.cs`
- `Assets/prefabs/Water/body.prefab`
- `Assets/Controller/water_00.controller`
- `Assets/Controller/water-up.anim`
- `Assets/Controller/water-up-loop.anim`
- `Assets/Controller/water-down.anim`

**Unity 编辑器操作**
等待资源导入及编译后运行。body 预制体 WaterTransferVisual 的 Reveal Duration 控制展开时长，默认 0.25 秒；帧动画子对象默认隐藏。

**注意**
独立编译、帧序列与循环配置、资源引用检查通过；现有关卡模型测试通过。当前无法连接编辑器，尚未进行实际播放验收。未提交 Git。

## 2026-09-17 上层水柱遮挡下层 Up

**原因**
所有水柱共用 Body 层级，Up 统一高一层，导致下层 Up 显示在上层 Body 前面。

**修改**
- TubeView 按槽位自下向上分配显示层级，每格间隔 3 层，保留 glow、body、up 的顺序。
- 选中水柱仍保留组内层次，并为最上层 Up 预留位置，避免盖过试管前景。
- 取消选择、转移落入目标管及溢出放回时，按实际槽位恢复排序。

**主要文件**
- `Assets/Scripts/Game/TubeView.cs`
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
无额外配置，等待编译后运行查看。

**注意**
相关脚本使用项目 Unity、DOTween 与 Playworks 程序集编译通过；尚未在 Unity 中实际播放验证。未提交 Git。

## 2026-09-17 小球替换为七格水柱

**原因**
按水柱示例显示每个颜色单元，每根试管容量为 7，相邻单元位置间距为 0.8。

**修改**
- 将 Water/body.prefab 接入 BallView，保留示例 body/up 的外观比例，增加独立 visualRoot 以沿用选中、移动、淡出与发光效果。
- 6 根试管各保留 7 个居中槽位，从世界 Y=-5.2 开始，每格增加 0.8；删除场景中的 7 个静态示例以免与生成对象重叠。
- 初始配置改为五种颜色各 7 个，共 35 个单元，保留一根空管；以实际 TubeModel 验证可解。
- 颜色表统一使用 body Sprite，Tint 控制主体颜色；Up 自动向白色提亮，透明度和排序跟随主体。

**主要文件**
- `Assets/Scenes/main.unity`
- `Assets/prefabs/Water/body.prefab`
- `Assets/prefabs/BallVisualPalette.asset`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/BallVisualPalette.cs`
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`

**Unity 编辑器操作**
- 重新加载 main 场景并运行查看生成的水柱。
- 在 BallVisualPalette 的各颜色条目中调整 Tint。
- 在 body 预制体内的 WaterBodyTopAnchor 调整 Up Lighten Amount（默认 0.4）和 Top Offset。

**注意**
独立编译、资源引用、槽位间距和实际 TubeModel 关卡测试通过；当前编辑器连接不可用，尚未进行 Unity 播放和画面验收。未提交 Git。

## 2026-09-17 Body 顶部对齐 Up

**原因**
Body 从底部向上伸长时，需要 Up 跟随顶部且保持自身尺寸。

**修改**
- 添加 WaterBodyTopAnchor，挂在 Body 上，自动获取自身 SpriteRenderer。
- 使用实际渲染边界将 Up 底边对齐 Body 顶边，只修改位置；编辑模式及运行时生效。
- 已配置 main 场景 Body 的 Up 引用，Top Offset 可微调接缝。

**主要文件**
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
重新加载 main 场景；Body 和 Up 保持同级，组件与引用已配置。

**注意**
已用 Unity 2022.3 程序集进行独立编译检查，尚未在 Unity 中实际预览。

## 2026-09-17 water Body 启用九宫格拉伸

**原因**
Body 使用 Simple 绘制模式，Sprite Border 未参与尺寸调整。

**修改**
- Body 的 SpriteRenderer 改为 Sliced，图片 Mesh Type 改为 Full Rect。
- 保留已有 Border、Transform 和 Size 设置。

**主要文件**
- `Assets/Scenes/main.unity`
- `Assets/prefabs/Water/body.png.meta`

**Unity 编辑器操作**
重新加载场景；后续通过 SpriteRenderer Size Y 调整高度，保持 Transform Scale 固定。

**注意**
未在编辑器预览验证。Size Y 小于上下边界总高度 4.61 时，首尾仍会被压缩。

## 2026-09-17 试管小球改为居中单列

**原因**
试管内小球改为单列，槽位只保留一侧并居中。

**修改**
- 6 根试管各保留 16 个槽位，删除右侧槽位，将保留槽位对齐试管 Sprite 中心。
- 初始小球按原左右配对缩减为单列，5 种颜色各 16 个，共 80 个，保持颜色顺序和纵向间距。

**主要文件**
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
- 已直接调整场景配置；在 Unity 重新加载 main 场景后查看。

**注意**
未进行 Unity 运行验证；未提交 Git。
