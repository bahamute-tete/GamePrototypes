# 第三阶段：动态外扩

动态扩散已接入，前两阶段文档中“尚未实现动态外扩”的描述不再适用。

打开 Demo/ContactWaveDemo.unity，进入 Play，或者勾选快照组件的 Editor Preview。

在现有快照场景使用时：Generate Sdf 开启，View 选择 Expanding Wave。已有检查模式保持不变。

- Spread Speed：米/秒，默认 0.3。
- Wave Width：亮带总宽度，默认 0.025 米。
- Wave Softness：亮带两侧的柔和过渡宽度，默认 0.02 米。
- Wave Color：独立 HDR 波纹颜色。
- Lifetime：每圈从出生到消失的时间。演示为 1.5 秒。
- Capture Interval：持续接触时产生新波纹的间隔。演示为 0.25 秒。

每圈的半径是 spreadSpeed × age，亮度是 1-age/lifetime。角色离开只停止生成新圈，旧圈继续扩散。手动捕获且关闭编辑预览时年龄冻结，不会自动移动。

波纹使用固定快照的 SDF 正距离等值线，不缩放平面；不额外保留原边界灰线。没有可靠 SDF 时该圈不显示，请检查组件 SdfDiagnostic。波前超出平面范围会被裁切，应为 Spread Speed × Lifetime 留够空间。需要更平滑的曲线可提高 Sdf Resolution，但会增加捕获成本。

CPU 捕获成本沿用第二阶段，每帧显示只改变半径和淡出参数。动态扩散不重新生成 SDF。多圈加法重合会增亮，可降低 Wave Color。

已验证独立年龄、半径、淡出、停止接触后传播、暂停和过期，以及角色轮廓 0.2/0.7/1.2 秒与过期截图。仅修改独立快照目录。
