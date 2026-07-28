# VAT 软体变形 Shader 逐行解析

> 文件：`VAT_SoftBodyDeformation_Reconstruction.shader`（SideFX Labs VAT 的 Unity URP 移植版）
> 核心思想：**把 Houdini 烘焙好的每帧顶点位置/旋转存进纹理，在 vertex shader 里查表回放**，完全绕过骨骼。

---

## 目录

1. [第 0 步：VAT 数据布局（前提）](#第-0-步vat-数据布局)
2. [第 1 步：时间 → 帧号](#第-1-步时间--帧号getvatframedata127175-行)
3. [第 2 步：帧号 → 纹理 UV](#第-2-步帧号--纹理-uv)
4. [第 3 步：采样位置纹理并解码](#第-3-步采样位置纹理并解码samplevatposition177195-行)
5. [第 4 步：还原法线（两条路径）](#第-4-步还原法线两条路径)
6. [第 5 步：组装最终顶点](#第-5-步组装最终顶点evaluatevatgeometry227291-行)
7. [第 6 步：像素阶段 = 普通 URP Lit](#第-6-步像素阶段就是普通-urp-lit)
8. [第 7 步：其余 4 个 Pass](#第-7-步其余-4-个-pass)
9. [总数据流](#总数据流)
10. [附录：关键公式速查](#附录关键公式速查)

---

## 第 0 步：VAT 数据布局

Houdini 导出的 `_posTexture` 组织方式：

```
纹理宽度  = 顶点数（每列一个顶点）
纹理高度  = 动画帧数（每一行是一帧的所有顶点位置）
RGB      = 该顶点该帧的位置（或偏移）
A        = 打包后的法线
```

所以"播放动画"本质上就是：**根据当前时间，算出该采样纹理的哪一行（V 坐标）**。

涉及的四张纹理：

| 纹理 | 内容 |
|---|---|
| `_posTexture` | 位置（RGB）+ 打包法线（A） |
| `_posTexture2` | 位置小数部分（双纹理高精度模式用） |
| `_rotTexture` | 每顶点每帧的旋转四元数 |
| `_colTexture` | 顶点颜色 |

---

## 第 1 步：时间 → 帧号（`GetVATFrameData`，127–175 行）

### 1.1 已播放时长

```hlsl
float elapsedTime = _TimeParameters.x - _gameTimeAtFirstFrame;
```

- `_TimeParameters.x`：Unity 提供的游戏运行秒数
- `_gameTimeAtFirstFrame`：这段动画"第一帧"对应的游戏时刻
- 开启 GPU Instancing 时改用 `StructuredBuffer<float> _gameTimeAtFirstFrameBuffer[unity_InstanceID]`，让每个实例起始时间不同（错开播放）

### 1.2 秒 → 循环帧号

```hlsl
float animationFrame = frac(
    elapsedTime * (_houdiniFPS / max(playbackFrameCount - 0.01, 0.01)) * _playbackSpeed
) * playbackFrameCount;
```

逐步理解：

- `elapsedTime * _houdiniFPS` = 按 Houdini 导出帧率（如 60fps）经过了多少帧
- 除以帧数再 `frac` = 取小数部分，实现 0→1→0 的**循环**
- 乘回 `playbackFrameCount` = 得到循环中的浮点帧号，例如 `12.7` 帧
- `_playbackSpeed` 控制播放速度；`max(..., 0.01)` 防止除零

### 1.3 拆分当前帧 / 下一帧 / 插值系数

```hlsl
float currentFrame = firstFrameIndex + PositiveModulo(selectedFrame - 1.0, playbackFrameCount);
float nextFrame    = firstFrameIndex + PositiveModulo(selectedFrame,     playbackFrameCount);
output.interpolation = frac((_B_autoPlayback > 0.5) ? animationFrame : _displayFrame);
```

- `currentFrame`：当前整数帧
- `nextFrame`：下一帧（供帧间插值）
- `interpolation`：帧号的小数部分（如 0.7），后面给 `lerp` 用
- `_B_autoPlayback` 关闭时，直接用材质面板上的 `_displayFrame` 手动指定帧

`PositiveModulo`（122–125 行）是数学意义上的正取模：

```hlsl
return value - divisor * floor(value / divisor);
```

HLSL 的 `%`/`fmod` 对负数会返回负数，自己写以避免负帧号。

`firstFrameIndex`（138–143 行）：允许从 `_PlaybackStartFrame` 开始只播放动画的一段，可播帧数 = `_frameCount - firstFrameIndex`。

---

## 第 2 步：帧号 → 纹理 UV

```hlsl
float baseV = (1.0 - vatUV.y) * vScale;
output.currentUV = float2(u, 1.0 - (baseV + currentFrame / frameCount * vScale));
output.nextUV    = float2(u, 1.0 - (baseV + nextFrame    / frameCount * vScale));
```

- `vatUV` 来自顶点属性 `TEXCOORD1`：**U = 该顶点在纹理里的列号，V = 顶点归一化 ID**
- `currentFrame / frameCount` = 帧号在纹理高度上的比例 → 行号
- 两次 `1.0 -` 是因为 Houdini 与 Unity 的 V 轴方向相反，需要翻转对齐

结果：`currentUV` 指向纹理中"**我这个顶点 × 当前这一帧**"的那个像素。

### 藏在 bound 小数里的元数据

SideFX 把纹理布局标志编码进 bound 数值的小数部分（因为 bound 在材质面板本来就要显示，顺便利用）：

```hlsl
float uScale = 1.0 - (ceil(boundMin.z) - boundMin.z);   // 有效宽度补偿
float vScale = 1.0 - frac(-boundMax.x);                 // 有效高度补偿
output.rawPositionData = step(0.5, frac(boundMax.z));   // 位置是否原始坐标
```

- `frac(boundMax.z) >= 0.5` → `rawPositionData = 1`：纹理存的是**原始坐标**
- 否则纹理存的是 **0~1 归一化坐标**，需要解码（见第 3 步）
- `uScale` / `vScale`：纹理做了 2 的幂 padding，这两个系数把 UV 缩回有效区域

### 补充：VAT Data 参数组是干什么用的

材质面板上的这组参数是 **Houdini 导出时自动写入的元数据**，不是给人调的：

```hlsl
_frameCount  // 动画总帧数 = 位置纹理的有效行数
_boundMaxX/Y/Z, _boundMinX/Y/Z  // 整场动画所有帧所有顶点的总包围盒（AABB）
```

**用途 1：还原归一化位置（主要用途）**

Houdini 导出 8bit 位置纹理时，把整段动画所有顶点位置线性压缩到 [0,1]：

```
编码（Houdini 侧）：t = (x − min) / (max − min)
还原（shader 侧）：x = t · (max − min) + min
```

包围盒必须涵盖**全部帧**的最大范围，否则某帧顶点超出会被截断。shader 用同一个包围盒做逆映射，位置才能严丝合缝还原。

> 代码里的 `* 10.0`（134–135、186–187 行）是 SideFX 约定：序列化到材质面板时数值被除了 10，shader 乘回来。显示值和实际值差 10 倍，不用管。

**用途 2：小数部分偷藏纹理布局标志**

SideFX 把元数据编码进 bound 数值的小数部分（面板小数位本来没人看）：

| 代码 | 取自 | 含义 |
|---|---|---|
| `step(0.5, frac(boundMax.z))` | `_boundMaxZ` 小数 | **rawPositionData**：位置纹理是原始坐标（1）还是归一化坐标（0） |
| `uScale = 1 − (ceil(min.z) − min.z)` | `_boundMinZ` 小数 | 纹理**有效宽度**比例（宽度 padding 到 2 的幂后的补偿） |
| `vScale = 1 − frac(−max.x)` | `_boundMaxX` 小数 | 纹理**有效高度**比例（帧数 padding 的补偿） |

即一个 `_boundMaxZ` 同时携带两份信息：**整数部分** → Z 轴包围盒上限（×10 后）；**小数部分** → "位置纹理是不是原始坐标"的开关。

所以材质面板上这些值是一堆奇怪的小数，就是这个原因。

---

## 第 3 步：采样位置纹理并解码（`SampleVATPosition`，177–195 行）

```hlsl
float4 positionSample = SAMPLE_TEXTURE2D_LOD(_posTexture, sampler_posTexture, uv, 0.0);
float3 encodedPosition = positionSample.rgb;

#if defined(_B_LOAD_POS_TWO_TEX)
    encodedPosition += SAMPLE_TEXTURE2D_LOD(_posTexture2, ...).rgb * 0.01;
#endif

output.offset = (rawPositionData > 0.5)
    ? encodedPosition                                       // 情况A：直接就是坐标
    : encodedPosition * (boundMax - boundMin) + boundMin;   // 情况B：归一化还原
```

### 情况 B：归一化 → 还原

Houdini 为了把位置存进 8bit 贴图，导出时把所有顶点位置线性映射到 [0,1]：

```
编码：t = (x - min) / (max - min)
还原：x = t * (max - min) + min     ← shader 里这一句
```

`boundMin` / `boundMax` 就是导出时记录的整体包围盒。

### 双纹理高精度模式

8bit 精度不够（位置抖动）时开启 `_B_LOAD_POS_TWO_TEX`：Houdini 导出第二张纹理存小数部分，`+= tex2.rgb * 0.01` 相当于两张图拼出更高的有效位数。

### alpha 通道

`output.packedNormal = positionSample.a;` —— 打包法线，留给第 4 步路径 1 使用。

---

## 第 4 步：还原法线（两条路径）

### 路径 1：压缩法线（`DecodeCompressedNormal`，197–212 行）

勾选 **Use Compressed Normals**（`_B_UNLOAD_ROT_TEX`）时，不使用旋转纹理，法线被压进位置纹理 alpha 的一个 float：

```hlsl
float packed = packedValue * 1024.0;                       // 10bit 整数
float highBits = floor(packed / 32.0);                     // 高 5bit
float2 encoded = float2(highBits, packed - highBits * 32.0) / 31.5;  // 两个 0~1
encoded = encoded * 4.0 - 2.0;                             // 映射到 [-2, 2]

float squaredLength = dot(encoded, encoded);
float reconstruction = sqrt(saturate(1.0 - squaredLength * 0.25));
float3 normal = float3(
    -encoded.x * reconstruction,
    1.0 - squaredLength * 0.5,
    encoded.y * reconstruction
);
return normalize(clamp(normal, -1.0, 1.0));
```

原理：一个 10bit 数拆成两个 5bit 数（0~31），归一化后映射到 [-2,2]，再用球面映射（spheremap）的逆运算 `sqrt(1 - len²/4)` 重建第三分量 —— 经典的法线压缩方案。

### 路径 2：旋转四元数（默认路径）

不压缩时，Houdini 给每个顶点每帧导出一个**四元数**（存 `_rotTexture`），表示该顶点局部朝向相对静止姿态的旋转。

解码（`DecodeRotation`，214–217 行）：归一化存储时把 [0,1] 映射回 [-1,1]：

```hlsl
return (rawPositionData > 0.5) ? encodedRotation : encodedRotation * 2.0 - 1.0;
```

旋转公式（`RotateByQuaternion`，219–225 行）：

```hlsl
return inputVector + 2.0 * cross(
    quaternion.xyz,
    quaternion.w * inputVector + cross(quaternion.xyz, inputVector)
);
```

这是四元数旋转向量 `q·v·q⁻¹` 的标准展开形式：**`v + 2(q.xyz × (q_w·v + q.xyz × v))`**，比构造旋转矩阵便宜。

用法：

```hlsl
float3 normal = RotateByQuaternion(float3(0, 1, 0),  rotation);   // 上向量 → 法线
float3 tangent = RotateByQuaternion(float3(-1, 0, 0), rotation);  // 侧向量 → 切线（供法线贴图）
```

---

## 第 5 步：组装最终顶点（`EvaluateVATGeometry`，227–291 行）

```hlsl
float3 positionOffset = (_B_interpolate > 0.5)
    ? lerp(current.offset, next.offset, frameData.interpolation)
    : current.offset;
output.positionOS = sourcePositionOS + positionOffset;   // 静止位置 + 动画偏移
```

法线/切线同理：`lerp(当前帧, 下一帧, interpolation)` 后归一化。

### 丢弃 padding 顶点（244–247 行）

```hlsl
if (vatUV.y <= 0.1) output.positionOS = 0.0;
```

Houdini 导出的 mesh 顶点数会 padding 到 2 的幂（对齐纹理宽度），多出来的假顶点 V≈0。这里把它们**折叠到原点丢弃**，避免渲染出垃圾三角形。

---

## 第 6 步：像素阶段就是普通 URP Lit

`ForwardFragment`（505–526 行）拿到顶点阶段算好的：

- `input.color`：从 `_colTexture` 采的顶点颜色（`SampleVATColor`，同样支持帧间插值，由 `_B_interpolateCol` 控制）
- 重建的法线 + 可选表面法线贴图（`GetSurfaceNormalWS`，399–424 行：标准 TBN 变换 `T*x + B*y + N*z`，再可选双面法线翻转）

表面参数（`GetVATSurfaceData`，452–466 行）：金属度 0、光滑度 0、无高光、无自发光。

最后直接调 URP 的 `UniversalFragmentPBR` + `MixFog` —— **和普通 Lit 材质完全一样**。VAT 只负责"动"，不负责"好看"。

---

## 第 7 步：其余 4 个 Pass

| Pass | 作用 | 复用的变形函数 |
|---|---|---|
| Universal Forward | 前向渲染主 Pass | `EvaluateVATGeometry` |
| GBuffer | 延迟渲染路径 | `EvaluateVATGeometry` |
| ShadowCaster | 让变形后的模型**投正确的影子** | `EvaluateVATGeometry` + `ApplyShadowBias` |
| DepthOnly | 深度预 Pass（`_DEPTH_TEXTURE`） | `EvaluateVATPosition`（只算位置，省法线采样） |
| DepthNormals | 深度+法线（SSAO 等需要） | `EvaluateVATGeometry` |

关键点：**阴影和深度也必须走同样的 VAT 变形**，否则影子会留在静止姿态的位置。

---

## 总数据流

```
时间 _TimeParameters.x
  → 减去起始时刻 → elapsedTime
  → × houdiniFPS × speed → frac 循环 → 浮点帧号
  → 拆成: 当前帧 currentFrame / 下一帧 nextFrame / 插值 t

顶点属性 vatUV (TEXCOORD1: 顶点列号)
  + 帧号 / frameCount → 帧行号
  → 采样 UV (currentUV / nextUV)

采样 _posTexture
  → RGB [归一化? ×(max-min)+min : 原样] → 位置偏移 offset
  → A   压缩法线  ── 或 ── _rotTexture 四元数旋转出 法线/切线

位置: sourcePositionOS + lerp(offset_cur, offset_next, t)
法线: normalize(lerp(normal_cur, normal_next, t))
颜色: lerp(colTex_cur, colTex_next, t)

→ 标准 URP PBR 着色
→ ShadowCaster / DepthOnly / DepthNormals 重复同样的变形
```

一句话：**GPU 上的"纹理查表式骨骼动画"，只不过每一根"骨骼"就是顶点自己。**

---

## 附录：关键公式速查

| 公式 | 位置 | 作用 |
|---|---|---|
| `frac(t · fps / n) · n` | 154 行 | 时间 → 循环浮点帧号 |
| `value - divisor·floor(value/divisor)` | 124 行 | 正取模 |
| `frame / frameCount` | 171 行 | 帧号 → 纹理 V 坐标 |
| `x = t·(max−min) + min` | 192 行 | 归一化位置还原 |
| `packed/32` 拆 5bit | 200–201 行 | alpha 解压法线（spheremap 逆运算） |
| `v + 2(q×(q_w·v + q×v))` | 221 行 | 四元数旋转向量 |
| `lerp(cur, next, frac(frame))` | 239 行 | 帧间插值 |
| `vatUV.y <= 0.1 → 0` | 244 行 | 丢弃 padding 顶点 |
