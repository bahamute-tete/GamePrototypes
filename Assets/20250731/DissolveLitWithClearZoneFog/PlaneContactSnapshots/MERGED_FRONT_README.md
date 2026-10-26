# 融合波前 Merged Front

在 ContactContourSnapshots 上启用 Generate Sdf，将 View 设为 Merged Front。编辑器内开启 Editor Preview，或手动捕获后用已有预览流程推进时间。独立示例为 Demo/ContactMergedFrontDemo.unity。

## 显示行为

每份快照先按自己的年龄向外扩散，再合并所有扩散区域，仅绘制并集边界。分离区域各自显示；接触后合并；内部交叉线和被完全包含的内圈消失。重复轮廓不会加亮。并集若仍有空洞，空洞边界也会显示。这是轮廓融合，并非物理水波干涉。

## 参数

- Spread Speed：扩散速度，米/秒。
- Wave Width / Wave Softness：亮带宽度和柔和过渡，单位米。
- Wave Color：颜色与 HDR 强度。
- Lifetime：每份快照的寿命，期间逐渐淡出。
- Capacity / Capture Interval：快照上限和捕获间隔。
- Wavelength / Packet Width / Amplitude / Source Phase 仅用于 Interference，对此模式无效。

## 实现

每份米制 SDF 为 d_i，扩散半径为 r_i = speed * age_i。使用 D = min(d_i - r_i) 合并区域，再根据 abs(D) 绘制窄亮带。必须先合并区域再画线；直接合并已经画好的环形亮带无法消除内部线。

ContactWaveUnion.shader 使用两张 ARGBHalf RT 交替读写。R 保存合并距离；G 保存获胜快照的淡出系数，最后由 ContactSnapshotDisplay.shader 统一显示。代码附中文核心注释。该 min 场保证并集的符号和零边界，内部距离并非处处为精确欧氏距离。

每帧从空区域重算，仅合并本组件内有效且未到期的 SDF。旧快照退出后，被遮住的较新内圈可能重新露出；本实现不保存不可逆合并历史。边界超过平面范围会被裁切，需留足平面尺寸。不同快照主导的边界段可能有不同亮度。

## 范围与验证

改动只在 PlaneContactSnapshots 内，原 PlaneContact 基础系统保持不变。旧模式仍保留，枚举末尾追加 MergedFront，已有场景模式不会因序号变化而改变。

Unity 编译通过；GPU 两个偏心圆距离场与 CPU min 对照最大误差约 0.000488 米（半浮点精度），被覆盖旧边界变为负值，重复波前的距离与亮度变化为 0。独立角色示例已检查融合预览及寿命结束后 ActiveCount=0。

此模式额外使用两张半浮点 RT，512×512 时约 4 MiB；每个有效快照每帧一次全屏合并，加一次统一显示。CPU 捕获 SDF 的原有成本仍存在，未进行目标 VR 设备性能验证。
