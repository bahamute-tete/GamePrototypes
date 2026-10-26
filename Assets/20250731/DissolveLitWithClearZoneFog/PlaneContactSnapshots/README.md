# 当前进度：第二阶段 SDF 已加入

新增功能与限制请先阅读 SDF_README.md。下文为第一阶段快照池的使用说明；开启 Generate Sdf 后，每份快照还会生成独立距离场，动态外扩尚未接入。

# 独立轮廓快照：第一阶段

本目录是独立实现，只读取原 DissolvePlaneContact 的 ContactTexture、SegmentCount、平面位置与尺寸。不修改原脚本、Shader、材质或场景，不更换原显示材质，也不写入原对象的 MaterialPropertyBlock。

## 使用

打开 Demo/ContactSnapshotDemo.unity，进入 Play。选中 `Contour Snapshots - independent preview` 调整参数。

在其他场景使用时，新建空物体并添加 ContactContourSnapshots，将原接触平面上的 DissolvePlaneContact 拖入 Source。Display Shader 指定 Custom/LiangZhu/ContactSnapshotDisplay，以确保构建时包含 Shader。

- Capacity：最多同时存储的快照数，默认 6，满池替换最老一份。
- Capture Interval：持续接触的捕获间隔，默认 0.2 秒。首次接触立即捕获。
- Lifetime：每份快照从出生到完全消失的时长，默认 1 秒，线性淡出。
- Line Color / Glow Color：独立快照颜色。多份快照重合使用加法混合，会更亮；可降低颜色亮度。
- 组件右键 Clear Snapshots / 清空快照：立即隐藏所有快照，下一次接触可重新捕获。

## 当前能力及边界

基础模式是“定时轮廓快照 + 独立年龄 + 淡出”，第二阶段已增加可选 SDF，尚无动态向外扩散。快照保存原 RT 的 R/G，不冻结当前能量着色结果，本版快照显示不应用能量噪声。

每个快照槽拥有独立的 RT；捕获时复制像素而非保存引用。出生后纹理内容保持不变，只更新显示强度。原接触消失不会清空旧快照；它们继续播放到各自到期。

平面位置、朝向、尺寸、来源 RT 或 Source 变化时清空。分辨率或容量改变时重建资源。关闭组件会释放所有自建纹理、Mesh、材质和临时显示对象。编辑模式默认不自动累积。开启 Editor Preview 后使用编辑器时间、约 30 Hz 自动捕获和淡出；关闭后冻结已有快照。Inspector 的“手动捕获当前轮廓”按钮可在预览关闭时捕获一份供检查，“清空快照”立即隐藏所有快照；若自动预览仍开着且角色仍接触，下一次更新会重新捕获。Play 模式使用游戏 deltaTime，暂停时停止自动捕获和衰减。进出 Play 会清空预览资源。

后续可在这个目录增加“截面填充 → SDF 快照 → 等距波前显示”，不需要改动原工程实现。当前复制普通 ContactMask 不能直接得到有符号距离场。

## 成本与检查

默认六张 512×512 ARGB32 快照约占 6 MiB（不含原 RT 和驱动开销），最多六次独立透明绘制。分辨率 2048 时六张约占 96 MiB，应避免无必要提高分辨率。每次捕获一次复制，不新增 BakeMesh 或三角形求交。

已检查：捕获独立性、像素固定、最老槽复用、离开后保留、过期、重新接触、分辨率重建、禁用释放和重新启用。已渲染保留、淡出、清空三张预览图。尚未做头显性能验证。


