# 从零理解 Bone Matrix 动画纹理

这是 Unity 2022.3 / URP 的入门实验：42 个顶点、2 根逻辑骨骼、120 帧、一个 Unlit Shader。
骨骼运动由数学公式在编辑器中生成，不需要 FBX、AnimationClip 或场景中的骨骼对象。
运行时只有普通 MeshRenderer。左边显示绑定姿势，右边通过纹理蒙皮。

## 1. 先运行

**已附带烘焙好的场景：直接打开 `Generated/TwoBoneDemo.unity`，点击 Play 即可。**
要自己体验烘焙过程，再按下面的步骤重新生成：

1. 等 Unity 完成脚本导入。
2. 菜单选择 **Tools > VAT Learning > 1 - Create Two Bone Demo**。
3. 如当前场景有未保存修改，Unity 会先询问是否保存；取消就不生成。
4. 工具生成并打开 `Generated/TwoBoneDemo.unity`，点击 **Play**，查看 **Game** 窗口。建议 16:9。
5. 左边保持直立；右边下半段不动、上半段左右摆动，中部平滑弯曲。

每次生成使用新目录（Generated、Generated 1 等），可保留不同实验。源场景不会被覆盖。
生成资产包含同一份网格、一张矩阵纹理、两份材质和演示场景。

操作：

| 控件 | 实验 |
|---|---|
| Pause / Play | 暂停或继续 |
| Next frame | 暂停并前进一帧 |
| Frame 0 | 回到直立姿势 |
| 下方滑条 | 暂停并查看指定帧，允许小数帧 |
| Interpolate adjacent baked frames | 是否混合相邻帧的结果 |
| Apply bone matrices | 关闭后右侧恢复原始网格 |

颜色表示权重：蓝色跟随 Bone 0，橙色跟随 Bone 1，中间渐变区域同时受两者影响。
默认 60 FPS，插值差异比较细微；暂停后在相邻帧之间缓慢拖动滑条，更容易看清连续与跳变的区别。

## 2. 第一个概念：顶点有“跟随谁”的信息

网格是一条从 y=0 到 y=2 的带子，关节在 y=1。

```text
y=2     顶端          权重 (0, 1)
         |
y=1.3   橙色          完全跟随 Bone 1
         |
y=1     关节/混合区域 权重 (0.5, 0.5)
         |
y=0.7   蓝色          完全跟随 Bone 0
         |
y=0     根部          权重 (1, 0)
```

每个顶点除位置外，还存了：

```csharp
boneIndices = (0, 1);
boneWeights = (1 - w1, w1);
```

例如 `(0.25, 0.75)` 表示把两根骨骼分别算出的位置按 25%、75% 混合。
权重总和应为 1。骨骼索引不是位置，是查询动画纹理时使用的编号。

打开 `Editor/BoneMatrixDemoBuilder.cs`，阅读 `CreateRibbon()`。
`SetUVs(1, indices)` 对应 Shader 的 `TEXCOORD1`；`SetUVs(2, weights)` 对应 `TEXCOORD2`。
这里借 UV 通道存数字，和普通贴图坐标没有关系。

## 3. 第二个概念：矩阵描述“如何移动”，不是最终顶点位置

Bone 0 永远保持单位矩阵，所以它负责的底部不动。
Bone 1 绕 `(0,1,0)` 沿 Z 轴摆动，最大角度为 ±60°，两秒一循环：

```csharp
angle = 60 * sin(2 * PI * frame / 120);
```

关键帧：0 帧 0°，30 帧 +60°，60 帧 0°，90 帧 -60°。
120 帧与 0 帧相同，所以纹理仅保存 0 到 119，播放时从 119 插值回 0。

要绕关节旋转一个网格顶点，先把关节移到原点，再旋转，再移回去：

```text
p' = T(关节) × R(角度) × T(-关节) × p
                           ↑
                       本例的 Bindpose
```

`BoneMatrix()` 用下面两步表达它：

```csharp
currentBone = Matrix4x4.TRS(Joint, rotation, Vector3.one);
bindpose = Matrix4x4.Translate(-Joint);
skinMatrix = currentBone * bindpose;
```

这里数学上的骨骼层级是：Bone 0 在原点，Bone 1 是它的子骨骼且局部位置为 `(0,1,0)`。
因为父骨骼不动，直接用矩阵就能描述完整变换，不需要生成 Transform 对象。

**思考：为什么动画角度为零时，结果是单位矩阵？**

因为 `T(关节) × T(-关节) = I`。绑定姿势下，蒙皮不应该改变原始网格。
若去掉 Bindpose，即使角度为零，所有受 Bone 1 影响的顶点也会被额外向上移动。

普通模型的一般形式是：

```csharp
skinMatrix = renderer.transform.worldToLocalMatrix
           * bone.localToWorldMatrix
           * mesh.bindposes[i];
```

本例烘焙坐标系为单位变换。左、右两条带子的场景位移，在 Shader 蒙皮之后通过物体矩阵统一应用。

## 4. 第三个概念：纹理是一个浮点数表

打开 `BakeBoneTexture()`。每根骨骼每帧存一个 3×4 矩阵：

```text
[ m00 m01 m02 m03 ]  -> 一个 RGBA 像素
[ m10 m11 m12 m13 ]  -> 一个 RGBA 像素
[ m20 m21 m22 m23 ]  -> 一个 RGBA 像素
[  0   0   0   1  ]  -> 固定，不存
```

纹理宽 6、高 120：

| 帧 / 像素 x | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| y=0 | B0 行0 | B0 行1 | B0 行2 | B1 行0 | B1 行1 | B1 行2 |
| y=1 | B0 行0 | B0 行1 | B0 行2 | B1 行0 | B1 行1 | B1 行2 |
| … | … | … | … | … | … | … |
| y=119 | B0 行0 | B0 行1 | B0 行2 | B1 行0 | B1 行1 | B1 行2 |

寻址公式：`x = boneIndex * 3 + matrixRow`，`y = frame`。
例如 Bone 1 在第 30 帧的第一行位于像素 `(3,30)`。

这一帧角度 +60°，Bone 1 矩阵约为：

```text
 0.5000  -0.8660   0   0.8660
 0.8660   0.5000   0   0.5000
 0        0        1   0
```

注意最后一列的平移：它保证绕 `(0,1,0)` 旋转，而不是绕原点旋转。

纹理用 RGBAFloat，每像素 16 字节，因此数据为 `6 × 120 × 16 = 11,520 字节`，约 11.25 KiB，不计资产元数据。
关闭 sRGB、压缩和 mipmap；矩阵可能有负数，不适合保存成普通颜色 PNG。
Inspector 里看起来奇怪的颜色不代表数据损坏。本例先使用 Float 消除半精度量化干扰，生产版本再评估 Half。

## 5. 第四个概念：Shader 在每个顶点上做蒙皮

打开 `BoneMatrixUnlit.shader`，先只看 `TransformByBone()`：

```hlsl
float4 p = float4(position, 1);
return float3(dot(row0, p), dot(row1, p), dot(row2, p));
```

这就是矩阵乘法。明确写成三次点积，避免初学时混淆矩阵行列。
`w=1` 表示位置，需要应用平移。法线是另一类量，本课用 Unlit 材质专注于位置。

再看 `SkinAtFrame()`：

```hlsl
return p0 * weight0 + p1 * weight1;
```

例如原始顶点为 `(0,2,0)`，完全跟随 Bone 1，在第 30 帧：

```text
移到关节空间：(0,2,0) - (0,1,0) = (0,1,0)
旋转 +60°： (-0.866, 0.5, 0)
移回网格空间：(-0.866, 1.5, 0)
```

最后 `TransformObjectToHClip()` 将蒙皮结果变到屏幕上。
**Shader 的输出变了，CPU 上原始 Mesh 的顶点数组没有变。**

## 6. 第五个概念：烘焙帧与屏幕帧不同

运行到第 `30.25` 帧时，Shader 读取 30、31 两帧，混合比例为 0.25：

```hlsl
p = lerp(SkinAtFrame(30), SkinAtFrame(31), 0.25);
```

固定权重下，这与线性插值蒙皮矩阵后再乘顶点等价；不是旋转四元数插值。
它只是在两次烘焙结果之间平滑过渡，不会重新计算真实骨骼姿势。

运行时代码 `BoneMatrixDemoPlayer.cs` 只推进时间，通过 MaterialPropertyBlock 设置帧和两个开关。
矩阵纹理已在编辑器里写好，不需要每帧上传全部矩阵。

## 7. 按这个顺序做三个实验

1. **理解权重**：暂停到 30 帧，观察蓝色不动、橙色旋转、渐变区域弯曲。关闭 Apply bone matrices，比较原始形状。
2. **理解 Bindpose**：阅读 `BoneMatrix()`，尝试在纸上把 `bindpose` 换成单位矩阵。预测角度为零时为什么会向上错位。如果实际改代码，生成器的关节校验会主动报错；恢复后再生成。
3. **理解混合**：把 `CreateRibbon()` 中的 `w1` 改为 `y < 1f ? 0f : 1f`，重新生成。混合区域消失，折弯会更生硬。观察后恢复平滑权重。

本例骨骼数量是刻意固定的两根；增减骨骼需要同时修改顶点数据、纹理布局和 Shader，不能只改一个常量。

## 校验与范围

菜单 **Tools > VAT Learning > 2 - Validate Bake Math** 会生成临时数据，逐帧解码纹理，与独立的绕点旋转公式对照，共检查 5,040 个顶点样本，并检查关节不动和包围盒覆盖范围。
这项检查验证烘焙数值，不替代 GPU 渲染检查。运行成功后 Console 会显示 `Bone Matrix math PASS`。

本课只实现位置蒙皮、单动画、单对象播放控制和帧间插值。无光照、法线蒙皮、阴影、Motion Vectors、实例化、Root Motion、动画切换、FBX 导入或运行时 IK。
这些是下一阶段功能；本课的结束标准是能解释 `原始顶点 → 查骨骼矩阵 → 权重混合 → 屏幕位置`。

若菜单未出现，先查看 Console 中的 C# 编译错误，包括项目中其他脚本的错误。
若网格呈粉色，检查项目是否启用 URP 和 Shader 的导入错误。

交付验证：在独立临时项目中使用 Unity 2022.3.55f1c1、D3D11 和本机缓存的 URP 14.0.11 Shader 库，完成 C# 编译、5,040 个顶点数值校验（最大误差 2.458e-7）、Shader 编译与 GPU 绘制检查。临时项目的包管理器启动异常，因此测试使用本地 Shader 库直接绘制；未对原工程的完整 URP 渲染配置或 Play Mode UI 做自动验证。工程声明的 URP 版本为 14.0.12。

参考：[Unity bindposes](https://docs.unity3d.com/ScriptReference/Mesh-bindposes.html)、[Unity 2022.3 SetPixels](https://docs.unity.cn/2022.3/Documentation/ScriptReference/Texture2D.SetPixels.html)、[NVIDIA Skinned Instancing](https://developer.download.nvidia.com/SDK/10/direct3d/Source/SkinnedInstancing/doc/SkinnedInstancingWhitePaper.pdf)。
