# Dissolve 与 Clear Zone Fog 接入

本目录的模块分为计算核心和 Unity 参数适配层。原来的 `Shader/DissolveCore.hlsl`、`Shader/SphereFogCommon.hlsl`、`Shader/ClearZoneFogInclude.hlsl` 是兼容入口，保留原函数名与字段名。现有材质保留原行为；控制器按 Local 归一化进度 / World 几何定位控制，不提供旧组件迁移逻辑。自定义宿主接入新版空间裁切时，需要补齐下列字段及入口条件。

## 文件职责

| 文件 | 职责 / 依赖 |
| --- | --- |
| Dissolve/DissolveCore.hlsl | 纯数学、DissolveSettings、DissolveResult；无材质字段、纹理、导数和管线依赖 |
| Dissolve/DissolveNoise.hlsl | 3D Value Noise、等权重三平面纹理采样；纹理函数需要 Unity 纹理宏 |
| Dissolve/DissolveMaterial.hlsl | 从既有 `_Dissolve…` 字段读取配置；覆盖率与抗锯齿入口用于片元阶段 |
| ClearZoneFog/ClearZoneFogCore.hlsl | 球、定向盒距离、smin、距离转雾量；无全局字段和管线依赖 |
| ClearZoneFog/ClearZoneFogGlobals.hlsl | `_SF_*` 全局数据、最多 8 个区域的遍历、仅区域雾的颜色混合 |
| ClearZoneFog/ClearZoneFogURP.hlsl | URP 逐像素距离雾、无区域回退、两种雾的平滑交接 |

模块不声明 ShaderLab Properties、关键字、Blend、ZWrite、AlphaToMask 或 Pass。拆文件不会自动增加 Shader 变体。所有内部引用使用相对路径；整体搬移模块时，只需调整调用者的入口路径。

## 直接复用现有 Controller 的溶解

1. 在宿主 Shader 中提供材质属性；字段名保持以下约定，才能被现有 DissolveController 和 GUI 识别。
2. 在宿主的 **同一个 UnityPerMaterial CBUFFER** 内声明下列字段；同一 Shader 的所有 Pass 使用相同声明和顺序。模块不会另建 CBUFFER，不要把这些声明重复添加到已有字段后。
3. 在 CBUFFER 外声明噪声纹理与采样器，再 include `Dissolve/DissolveMaterial.hlsl`（或者旧兼容入口）。先包含宿主管线的 Core.hlsl，以提供纹理宏。

```hlsl
// 放入宿主既有的 UnityPerMaterial，而不是创建额外的 CBUFFER。
float _DissolveMode;                 // 0=Noise, 1=Axis, 2=Radial
float _DissolveSpace;                // 0=Local, 1=World
float _DissolveSpatialMode;          // 0=normalized, 1=world geometry, 2=missing Origin (visible)
float _DissolveEdgeUnits;            // 0=legacy threshold width, 1=controller length units
float _DissolveCoverageWidth;
float _DissolveAAPixels;
float _DissolveEdgeWidth;
float _DissolveNoiseScale;
float _DissolveEdgeNoiseStrength;
float _DissolveUseNoiseTex;
float4 _DissolveAxis;                // xyz=单位方向，w=半范围
float _DissolveAxisCenter;
float4 _DissolveRadial;              // xyz=中心，w=最大半径
float _DissolveRadialReverse;
float _DissolveBrightnessFade;
float _DissolveBrightnessPower;

// 由调用者使用的控制与合成参数，同样放入该 CBUFFER。
float _DissolveEnabled;
float _DissolveAmount;
float4 _DissolveEdgeColor;
float _DissolveEdgeIntensity;

// CBUFFER 外：
TEXTURE2D(_DissolveNoiseTex);
SAMPLER(sampler_DissolveNoiseTex);
```

局部坐标由顶点阶段传入原始模型坐标；世界坐标由宿主完成变换。模式参数和坐标空间必须与 Controller 一致。Noise 的 scale 由适配层应用；Axis/Radial 无扰动时不采样噪声。纹理噪声保持原有三个投影平均，不改变为法线加权。

片元阶段的最小合成片段（`rgb`、`alpha`、`positionWS`、`positionOS` 来自宿主）：

```hlsl
if (_DissolveEnabled > 0.5 && DissolveShouldEvaluate(_DissolveAmount))
{
    DissolveResult d = DissolveEvaluateMaterial(positionWS, positionOS, _DissolveAmount);
    rgb *= ComputeDissolveBrightness(_DissolveAmount);
    rgb += _DissolveEdgeColor.rgb * d.edge * _DissolveEdgeIntensity;
    alpha *= d.coverage;
}
```

- **不透明 A2C**：Forward Pass 配置 `AlphaToMask On`，输出连续覆盖率到 Alpha；保留原有颜色合成与深度写入策略。效果依赖 MSAA。
- **透明**：`Blend SrcAlpha OneMinusSrcAlpha`、`AlphaToMask Off`，以上 alpha 是原始透明度乘溶解覆盖率。现有实现使用 `ZWrite Off`。
- **ShadowCaster**：启用且 Amount 大于现有阈值时，使用 `clip(ComputeDissolveShadowCoverage(positionWS, positionOS, _DissolveAmount) - 0.5)`，并保留原本贴图裁剪。这里不计算覆盖率导数；若选择纹理噪声，仍使用片元纹理采样。
- **DepthOnly**：使用 `ComputeDissolveCoverage`；按目标 Pass 的 MSAA/A2C 或抖动裁剪策略处理，不能一律套用透明 Alpha。
- **Meta**：现有 Shader 不将运行时溶解烘焙到光照贴图，保持原有行为。

旧的 `ComputeDissolveCoverageAndEdge` 仍返回 float2(coverage, edge)。`ComputeDissolveAlphaOnly/AndEdge` 是可选的二值抖动接口，当前透明 Shader 不使用它们。`AlphaToCutoutCoverage` 也保留供现有 Pass 调用。

## 不使用既有材质字段的溶解

仅 include `Dissolve/DissolveCore.hlsl`，填充 DissolveSettings 的全部字段。使用自己的坐标、纹理、顶点数据或控制参数：

```hlsl
// s 为调用者完整初始化的 DissolveSettings。
// noise 为调用者提供的噪声；轴向/径向且扰动为 0 时可传 0。
float field = DissolveEvaluateField(position, s, noise);
float pixelWidth = fwidth(field) * max(aaPixels, 0.0); // 仅片元阶段
DissolveResult result = DissolveEvaluate(field, amount, pixelWidth, s);
```

核心不使用屏幕导数。非片元阶段可显式传入合适的宽度（例如 0），不能直接调用含 fwidth 的材质适配入口。`DissolveDistance` 是归一化场的阈值距离，不是世界单位 SDF。

## 普通表面的 Clear Zone Fog

include `ClearZoneFog/ClearZoneFogURP.hlsl`；需要 Unity 雾时，在宿主 Pass 保留 `#pragma multi_compile_fog`。在片元阶段调用：

```hlsl
float fogAmount;
float3 fogColor;
ClearZoneFog_Resolve(positionWS, allowClearZoneFog, fogAmount, fogColor);
rgb = lerp(rgb, fogColor, fogAmount);
```

透明材质可以像当前 Shader 一样，自行把 Fog Color Strength 乘入颜色混合系数，把 Fog Fade Strength 用于 Alpha 衰减。模块不强制改变 Alpha。

场景中的 ClearZoneFogController 和 SphereFogVolume 负责 `_SF_*`；这些是全局字段，不放入 UnityPerMaterial。开启时接管雾，无有效区域时按当前 Controller 协议回退 Unity 雾。`_SF_Weight` 控制交接；颜色按各自雾量贡献加权，不是把两层雾依次叠加。接管模式下不要再对结果额外调用一次 Unity MixFog。

## 天空接入

天空仅 include `ClearZoneFog/ClearZoneFogGlobals.hlsl`，无需 URP 距离雾接口。`_SF_AffectSky`、`_SF_SkyDistance` 仍由天空 Shader 自行声明，保留原有顶点、立体渲染和 cubemap 解码方式。

```hlsl
if (_SF_AffectSky > 0.5 && _SF_Active > 0.5 && _SF_Weight > 0.0)
{
    float3 samplePosition = cameraPositionWS + normalize(worldDirection) * _SF_SkyDistance;
    rgb = ClearZoneFog_Apply(rgb, samplePosition);
}
```

天空只应用区域雾及其权重；不自动回退 Unity 距离雾。不要把 Cubemap 自身采样方向直接当作世界方向。

## 独立区域计算与兼容约定

`ClearZoneFogCore.hlsl` 可单独用于自定义数据源：先计算球或定向盒距离，按原遍历顺序用 `ClearZoneFog_SmoothMin` 合并，再调用 `ClearZoneFog_DistanceToFactor`。盒轴须为世界空间正交单位向量，轴 w 是对应半尺寸。smin 的 K 和雾过渡宽度是不同参数；保持遍历顺序可以避免平滑合并顺序造成的外观差异。

全局数组、`SF_MAX_VOLUMES=8`、球/盒编码、Controller 上传方式及所有 `_SF_*` 名称均保持不变；新增统一 ClearZoneFog 函数名前缀只用于模块 API。旧 SphereFog 函数通过兼容入口继续可用。项目其他目录中的旧版 DissolveCore 副本有不同历史行为，不应直接批量替换。

