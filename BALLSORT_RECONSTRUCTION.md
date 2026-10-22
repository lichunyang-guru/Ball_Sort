# Ball Sort 网页复刻

入口：`Assets/Scenes/Home.unity`，Unity 2022.3.62f3。打开后进入 Play Mode 即可运行。

## 已还原

- 原网页资源、熊猫背景、24 根短管和 1 根长管、192 个球及初始排列。
- 选中、同色转移、容量限制、动画锁定、完成蜡烛及火焰、音效、两步点击引导。
- 横竖屏布局、图片本地化和 Play Now 按钮颜色。
- 原网页点击次数和完成管数跳转配置。GameRoot 上的 GameConfig 默认点击 50 次、周期 1、完成 4 管；AssetConfig 绑定背景和球贴图。

素材位于 `Assets/BallSort/Art`、`Audio`、`Generated`。玩法脚本位于 `Assets/Scripts/Game`，UI 脚本位于 `Assets/Scripts/UI`。C# 根据项目历史版本和网页导出逻辑重建，不是从 HTML 直接恢复的原始 C# 源文件。

## 验证记录（2026-10-21）

- TubeModel 与原网页 JS 差分测试：3005/3005 通过。
- 配置及跳转计数差分测试：2006/2006 通过。
- 25 根管子的横屏、竖屏初始状态及一次转移后状态全部一致。
- Unity 运行测试确认：第一次有效点击后引导切换目标，第二次有效点击后隐藏；满管触发一次完成通知。
- 排除未同步的手指动画区域后，截图 RGB 平均绝对误差：竖屏 0.928/255，横屏 0.339/255。按钮主色均为 RGB(207,28,28)。这些指标不代表所有动画帧逐像素一致。
- 最新脚本编译错误查询为 0。控制台仍有 MCP 插件 `get_tool_states` 不兼容提示和 Metal depth surface 提示，不将其描述为“控制台完全无错误”。

实际输入限制：Unity 自动化测试通过调用点击处理方法和引导射线检查验证交互。原网页用实际浏览器鼠标点击验证。Codex 原生鼠标点击在本次 Unity 会话未触发引导，工具操作期间编辑器焦点状态异常；原因尚未确定，真实 Unity 鼠标输入不能标记为通过。尚未重新导出并验证 PlayWorks 包。

原版中红色球对应 Green 枚举，满管后蜡烛呈绿色；已在原网页实际运行确认，保留该行为。5 个禁用的 MLT_TextSwitch 未导入 translationJsonText，当前可见本地化由 MLT_ImageSwitch 图片承担；完整源字段保留在 SourceScene.json。

## 重建与复测

`Tools/Ball Sort/Rebuild Extracted Playable` 重新生成素材和 Home 场景；`Rebuild Scene From Imported Assets` 使用已导入素材重建。两者都会覆盖 Home 的手工修改，日常运行不需要执行。

`Tools/Ball Sort/Verify Portrait And Landscape` 只在手动调用时运行，会切换 Game View 分辨率并在 Play Mode 内创建临时完成测试布局；测试后退出 Play Mode 恢复场景。`Enable Native Input Verification` 仅在当前 Play Mode 临时启用后台刷新；`Capture Native Input State` 保存当前状态，不模拟点击。

证据目录：

- `/Users/cdit228/Documents/Codex/2026-10-15/new-chat/BallSort-Reconstruction`：提取脚本、原版截图、解码数据、tests 下的差分报告。
- `/Users/cdit228/Desktop/UnityProject/GitHub/BallSort-Verification`：Unity 截图、管子状态和原生输入尝试记录。

源文件：`/Users/cdit228/Downloads/T_S_BallSort_var_Red1_Auto1_260828_Zane-Dane-Lila_AppLovin.html`

SHA256：`a4ae3edf7e4bae24c5d78af24fb417a28e592f8e6af9864321a1bc3dbb39f44a`
