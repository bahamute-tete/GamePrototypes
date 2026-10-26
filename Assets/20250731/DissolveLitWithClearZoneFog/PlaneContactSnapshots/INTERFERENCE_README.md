# SDF 波包干涉

打开 Demo/ContactInterferenceDemo.unity，进入 Play 或开启 Editor Preview。现有快照组件也可以直接将 View 切换为 Interference，Generate Sdf 保持开启。

## 参数

- Wavelength：波长，米。建议至少为 SDF 最大像素边长的四倍，太小会锯齿/闪烁。
- Packet Width：高斯波包标准差，米。调大后波包更宽，更容易相互重叠。
- Amplitude：振幅系数。
- Source Phase：新捕获波包的相位（度），通常保持 0。180 可用于同位置反相抵消测试；旧快照的相位不会跟着改。
- Spread Speed / Lifetime：沿用已有传播速度和寿命。
- Capture Interval：控制各波包的出生间隔，影响重叠相位。
- Wave Color：合成后的 HDR 发光颜色。

Wave Width / Wave Softness 只用于 Expanding Wave 亮带模式；干涉模式用 Wavelength / Packet Width。

## 实现

每份快照计算 q = d - speed * age，振幅为 amplitude * cos(2π*q/wavelength + phase) * exp(-q²/(2*packetWidth²)) * fade。

新增 ContactWaveAmplitude Shader 将正负振幅加法写入线性 ARGBHalf RT 的 R 通道，每次更新先清零。不能用普通 UNorm RT，不能提前 abs/saturate 振幅。最后一个统一平面读取合成值并用 abs(totalHeight) 转成能量亮度。

同相增强，反相抵消；每份包络、年龄与位置仍独立。只在同一快照组件的池中进行干涉，不跨多个组件。它是简化的波形叠加，没有水面反射、障碍物和完整波动方程。保留原始 SDF 的栅格误差和捕获成本。

每次显示更新新增最多 N 次振幅绘制和一次统一平面显示。512×512 ARGBHalf 约 2 MiB。目标设备需支持半浮点 RT 及其加法混合；本机已验证负值可保留，头显仍需实机测量。波纹超过平面尺寸时会被裁切。

进入其他 View 可返回之前的亮带或检查视图。清空/关闭组件会隐藏合成层并清理对应资源。Amplitude Shader 保留资产引用以供打包。

## 验证

GPU 浮点读回：单波振幅同时包含正负；两个同相快照得到精确两倍（本机半浮点结果），反相残余约 5.96e-8。偏心截面与 CPU 公式最大差约 0.00024，验证 RT 方向。真实角色轮廓已检查干涉和过期画面。仅修改 PlaneContactSnapshots 独立目录。
