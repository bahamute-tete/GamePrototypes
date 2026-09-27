# 不透明物体 A2C 溶解 Demo

打开同目录的 `FeatureOverview.unity`，Game 视图使用 16:9。无需进入 Play，场景中的缓存总览即可显示。推荐 1920×1080 预览；输出为 3840×2160。

从 **Tools → LiangZhu → Feature Overview** 打开操作面板。

## 六个画面

1. **Noise / 噪声溶解**：默认 Amount = 0、0.35、0.7。Local / World 均由 Amount 控制，空间改变噪声采样坐标。面板可选择共享贴图或过程噪声。
2. **Direction / 定向溶解**：World 模式，共同底部 Origin、向上 Direction，Plane Offset = 0.65 / 1.45 米。青色框为真实切面，箭头表示 Direction；控制器支持反转消失侧。
3. **Radial / 径向溶解**：相同 Origin、Radius = 1.05 米；比较默认消去球内和 Reverse 保留球内。青色圆环为真实球范围。
4. **Local / World 空间**：相同立柱布局包含高度差与旋转。Local Amount = 0.5，按各 Renderer 的 Bounds 归一化；World Plane Offset = 1.1 米，共享一个世界切面。
5. **Timeline / 无需手动打帧**：真实 Amount Track，展示配置流程、Clip 起止值、实际时间曲线和 0.6 / 3 / 4.8 秒模型状态。
6. **Timeline / 多参数自由控制**：三个轨道绑定同一个 World Direction Controller，独立驱动 Plane Offset、Edge Width、Edge Intensity，展示 1.2 / 3 / 4.8 秒实际结果。

所有主体使用正式 `Custom/LiangZhu/Opaque_Dissolve_Lit`，独立 Demo 材质。辅助线只是展示工具。画面不包含透明、折射、区域雾或天空演示。

| 类型 | Local | World |
|---|---|---|
| Noise | Amount；局部噪声坐标 | Amount；世界噪声坐标 |
| Direction | Amount；各物体 Bounds 归一化 | Origin + Direction + Plane Offset |
| Radial | Amount；各物体 Bounds 中心 | Origin + Radius |

## Timeline 使用方法

**无需额外编写脚本，无需手动录制动画关键帧；绑定现成的 DissolveController，通过 Dissolve Clip 配置参数动画。**

- 基础资产：`FeatureOverviewTimeline.playable`。6 秒 Clip，Start Value = 0、End Value = 1。归一化时间曲线为 `(0,0) → (0.4,1) → (0.6,1) → (1,0)`，线性切线。默认采样 Amount 为 0.25 / 1 / 0.5。
- 多参数资产：`FeatureOverviewMultiTimeline.playable`。所有 Clip 从 0 秒开始，After Clip = Hold End。

| 参数 | 起止值 | 时长 | 曲线 |
|---|---|---|---|
| Plane Offset | −0.2 → 2.2 米 | 6 秒 | 线性 |
| Edge Width | 0.02 → 0.18 米 | 4 秒 | 平滑缓入缓出 |
| Edge Intensity | 0.5 → 4 | 6 秒 | 缓入 |

面板选择对应 Timeline 格，点击“打开对应 Timeline”。拖动面板时间滑条会更新该格的时间标记及面板中的当前状态图片；三个固定采样时刻保持不变。编辑 Clip 的起止值、曲线或时长后点击“刷新 Timeline 画面”。图、标签和图片均重新读取实际资产；模型通过 `PlayableDirector.Evaluate()` 求值。

支持 Amount、Plane Offset、Radius、Edge Width、Edge Intensity；可用参数随模式变化。类型、空间、颜色、方向不是当前 Dissolve Clip 支持的动画参数。不要让绑定同一个 Controller 的轨道重复控制同一参数。

**Hold End** 在 Clip 结束后保持该曲线终点的实际值；不一定等于 End Value（例如基础曲线终点为 0）。**Restore Original** 在 Clip 结束后恢复原值。停止 Timeline 时恢复原值。面板离线采样使用同一实际资产与绑定的独立临时求值实例，销毁图之后恢复类型明确的参数快照、Renderer 引用、显隐及属性块，不改变用户的原 Timeline 时间。

## 刷新、导出与恢复

- “刷新单格”只重渲染选中的格子并重新排版；“刷新全部”更新所有画面。
- “恢复默认展示参数”恢复演示参数，不覆盖用户编辑过的 Timeline。
- “导出带标签 / 无标签 4K PNG”会刷新真实模型状态后输出 PNG；无标签版本保留图像、曲线和三维辅助线。
- `FeatureOverviewAssets/Previews/FeatureOverview4K.png` 是默认带标签总览，`FeatureOverview4K_Clean.png` 是无标签版本。
- “Tools → LiangZhu → Rebuild A2C Feature Overview”重建默认模型布局、材质和两个 Timeline。每次重建先将现有 Demo 场景、未保存场景状态和 Timeline 备份到项目 `Library/FeatureOverviewBackups/时间戳`。不要将备份导入 Assets，避免重复 GUID。
- 改版本身另有完整改前备份，位于本次工作目录的 `OverviewDemoV2/Backup/SampleLevel`。

预览要求 URP Forward、4× MSAA、Opaque Texture。工具不修改项目管线设置，使用 URP SingleCameraRequest 在 4× MSAA 渲染目标完成 A2C，再解析到图片。源展示组默认隐藏；打开场景只显示缓存总览。

## 检查

**Tools → LiangZhu → Validate Feature Overview** 检查真实轨道采样、倒序求值、Stop 恢复、修改起止值/曲线/时长、Hold End / Restore Original、注入异常后的参数/引用/显隐恢复、重复刷新和带标签/无标签 4K 输出。检查期间会暂时修改基础 Clip 并在 finally 中恢复。

正式 Shader、DissolveController、DissolveTrack、DissolveClip 均不由此 Demo 修改。原测试场景、SampleTimeline 和正式功能资源保持原样。
