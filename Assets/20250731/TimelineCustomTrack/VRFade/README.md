# VR Fade — URP 14 / Unity 2022.3 LBE 场景过渡 RenderFeature

为 VR LBE 项目设计的场景过渡 RenderFeature，支持 5 种过渡类型，由 Timeline 自定义轨道驱动，单 Material + 单 Pass + Keyword 切换。

## 文件结构

```
Assets/VRFade/
├── Runtime/
│   ├── FadeRuntime.cs          全局状态层（含 FadeType 枚举与 FadeState 结构）
│   ├── FadeRenderFeature.cs    URP RenderFeature 主入口
│   └── FadeRenderPass.cs       Blit Pass（XR 兼容，按 type 切 keyword）
├── Shaders/
│   └── VRFade.shader           4 路径合一（multi_compile keyword 切换）
├── Timeline/
│   ├── FadeTrack.cs            自定义轨道
│   ├── FadeClip.cs             Clip（OnValidate 自动应用类型默认值）
│   ├── FadeBehaviour.cs        Clip 运行时数据
│   └── FadeMixerBehaviour.cs   Mixer（主导 Clip 决定 type）
└── Editor/
    └── FadeClipEditor.cs       Inspector（按 type 隐藏无关字段）
```

## 五种过渡类型

| Type | Shader 路径 | 特点 | 推荐场景 |
|---|---|---|---|
| **SolidColor** | 默认（无 keyword） | 整屏 lerp 到 Color | 通用黑场 / 白场 / 任意纯色 |
| **Iris** | `_FADE_IRIS` | head-locked 圆形虹膜遮罩 | VR 最舒适的转场，瞬移、视点切换 |
| **Desaturate** | `_FADE_DESAT` | 降饱和 + 压暗 | 情绪转折、时间慢镜 |
| **DepthFade** | `_FADE_DEPTH` | 远/近不同步淡入 | 大场景过渡，比纯黑多一层空间感 |
| **Flash** | 默认（同 SolidColor） | 白色 + 尖峰曲线 预设 | 剧情高潮、爆炸、能量激发 |

## 安装步骤

1. 把 `VRFade/` 拖到 `Assets/` 下。
2. 打开 URP Renderer 资产（通常在 `Assets/Settings/`）。
3. 点 `Add Renderer Feature` → `Fade Render Feature`。
4. **如果用 DepthFade**，确认 URP Asset 的 `Depth Texture` 开关已勾上（或保持默认行为，RenderPass 会通过 `ConfigureInput` 自动请求深度）。

## RenderFeature 面板

| 字段 | 说明 |
|---|---|
| Render Pass Event | VR 推荐 `AfterRenderingPostProcessing`（覆盖最终画面）。 |
| Override Material | 留空使用默认 shader（含全部 5 种类型）。 |
| Editor Preview | 不进 Play 即可在 Scene/Game 视图看效果。 |
| Preview Type / Color / Alpha | 预览参数（仅 Editor）。 |

## Timeline 用法

1. 创建 Timeline。
2. 右键 → Add Track → **VR Fade Track**。
3. 轨道上右键 → Add VR Fade Clip。
4. 选中 Clip，在 Inspector 选 **Type**：

   - 切换 type 时，type 专属参数自动填默认值
   - 切换到 Flash 时会同时改写 color = 白、curve = 尖峰
   - Inspector 会自动隐藏与当前 type 无关的字段

### 各类型参数说明

#### Iris（圆形虹膜遮罩）
- `Iris Center` — UV 空间的中心。VR 中 (0.5, 0.5) = 摄像机正前方
- `Iris Softness` — 边缘软度，0.05 是温和过渡
- `Iris Aspect Correct` — 开启后是正圆而不是椭圆

#### Desaturate（降饱和 + 压暗）
- `Desaturation Amount` — alpha=1 时的灰度强度，1 = 完全黑白
- `Brightness Multiplier` — alpha=1 时的亮度乘数，0.4 = 压暗到 40%
- ⚠ 不使用 Color 字段

#### DepthFade（深度感应黑场）
- `Depth Near` — 该距离开始淡入
- `Depth Far` — 该距离完全覆盖
- `Depth Invert` — 反转：勾选后近处先黑

#### Flash（闪白）
- 等同 SolidColor，但默认色 = 白、曲线 = 尖峰
- 推荐总时长 ≤ 0.3 秒

## 多 Clip 重叠规则

- **同类型重叠**：alpha 加权累加（自然交叉过渡）
- **异类型重叠**：权重最大的 Clip 决定 type 与 type-specific 参数；alpha 仍然累加
- LBE 实战中通常顺序排列、不重叠，规则不会触发

## 脚本 API

```csharp
// 简单纯色（用于绕过 Timeline 的紧急黑场）
VRFade.FadeRuntime.SetSolid(Color.black, 1f);

// 完整状态（如需 Iris/Desat 等）
var s = VRFade.FadeState.Default;
s.type = VRFade.FadeType.Iris;
s.alpha = 0.7f;
VRFade.FadeRuntime.SetState(in s);

// 清除（淡入回正常）
VRFade.FadeRuntime.Clear();
```

## VR / XR 兼容性

- ✅ XR Single Pass Instanced（`SAMPLE_TEXTURE2D_X` + `Blitter.BlitCameraTexture`）
- ✅ XR Multi Pass
- ✅ 非 VR 桌面项目
- ⚠️ 跳过 Preview / Reflection 相机，反射探针不会被涂黑

## 性能特性

- alpha=0 时整个 Pass 被 skip，VR 90Hz/120Hz 零开销
- 单全屏 Blit，移动 VR（Quest）也无压力
- shader 用 `multi_compile_local_fragment`，每帧只编译激活变体的代码
- 所有 Property ID 已缓存，无 GC alloc

## 已知细节

- `FadeRuntime` 是 static，跨场景保持状态
- `FadeMixerBehaviour.OnGraphStop` 不清状态，由下一段 Timeline 接管
- 进入 Play 模式时 `[RuntimeInitializeOnLoadMethod]` 自动重置一次

## 扩展自定义 Shader

如需做径向遮罩、十字溶解等更复杂效果：

1. 复制 `VRFade.shader` 改名
2. 添加新的 keyword（或新 Pass）
3. 保留 `_FadeColor / _FadeAlpha` 接口
4. 在 RenderFeature 的 Override Material 字段赋上新材质 —— C# 一行不用动
