# VAT 人群：路径引导与静态 SDF 避障

打开 `Assets/20250731/VAT/VATScene.unity`，运行场景。默认显示 50 人的 SDF 版本。左上角可以切换原版、调节速度、重新生成。

选择 **VAT Crowd SDF Demo** 调整人数（1–1024）、速度、碰撞半径、前视距离、避让强度和转向速度。人数变化会重启人群。人数上限是图容量，不是性能承诺。

原图 `VFXGraph/VFX_VAT_Houdini.vfx` 继续保留。新版为 `VFXGraph/VFX_VAT_Houdini_SDF.vfx`。原场景中的 `DrawMeshInstance` 和 `VFX_VAT` 是另外两套演示，默认关闭，仍可手动打开。`Generated/VATSceneBeforeSDF.unity` 是改造前的场景备份，包含构建时已有的编辑。

## 节点与数据

- **Initialize VAT Crowd (SDF)**：沿路线分布角色，避开固体出生点，初始化随机动作相位和固定绕行偏好。
- **Follow Path + Avoid SDF + Slide + Animate**：根据实际位置更新路径进度；前方、左前、右前采样避障；短步积分并修正穿透；沿接触面滑动；平滑朝向；根据位移累积 VAT 帧。
- Output 沿用原 VAT Shader Graph、网格和纹理；关闭自动播放，用 `crowdFrame` 驱动 `_displayFrame`。停止时保持当前动作帧，这套素材不包含独立待机动画。
- Update 的自动位置和旋转积分均关闭。不要重新开启 **Update Position**，否则会重复积分。
- 场景、路径和 SDF 均采用**世界坐标**。VFX 对象保留原非零位置，不重复叠加它的平移。
- `FieldCenter/FieldSize` 表示世界空间、轴对齐烘焙盒。纹理内部负、外部正，距离按烘焙盒最长边归一化。`BodyHeight` 是从脚底枢轴向上的碰撞采样偏移；碰撞法线投影到 XZ，地面高度由 `GroundY` 单独控制。
- 半径不依赖网格大小；内部另有 0.08 米安全余量。当前体素边长为 0.5 米，特别薄的墙或窄缝不适合这组分辨率。

## 修改环境

1. 打开 VATScene，退出 Play Mode。
2. 在 **VAT Crowd SDF Demo → Static collision proxies (rebake after editing)** 下移动、旋转或缩放围墙、柱子、方块。新增障碍也应放在这个分组下，并带有 MeshFilter 和封闭实体网格；单独添加 Collider 不会参与烘焙。
3. 选中演示根对象，确认障碍和路线位于青色烘焙范围内。超出范围时先调整 **Field Center / Field Size**。
4. 执行 **Tools → VAT Crowd → Re-bake SDF**。不需要选中特定障碍物，它会合并分组内激活物体上的 MeshFilter。地面不参与烘焙。
5. Console 出现 `SDF re-baked` 后保存场景，再运行查看。重烘焙会更新纹理并重启人群，以免角色留在刚移动进去的障碍内部。

VFX 14 烘焙器的默认资源清理会在 Edit Mode 调用 `Destroy`，连 `Destroy(null)` 也会报错。项目的 `CrowdBakerDisposal` 使用编辑模式清理临时材质、纹理和 GPU 缓冲区，不删除场景障碍或输入网格，也不修改 Unity 包源码。

SDF 使用 Unity `MeshToSDFBaker`，最长边 128；当前烘焙盒为 32×8×64 米，对应 64×16×128 的 RFloat 三维纹理。合并碰撞网格和纹理保存在 `Generated`。

选中演示根对象可查看路径及烘焙范围，启用 **Show Proxy Wireframes** 可以查看碰撞模型。移动整个演示组不会自动重算世界空间路径和 SDF，修改环境后必须重新烘焙并检查路径。

## 验证

**Tools → VAT Crowd → Validate GPU Movement (120 simulated seconds)** 使用与 VFX 完全相同的 `CrowdStep` GPU 函数，分别按 30/60 FPS 模拟 120 秒，检查全部角色能完成路线、最小距离、地面约束；另测正面撞墙、斜向碰墙、墙角、正面遇柱和零速动画。

结果见 `Generated/Validation.txt`。其中批量计算耗时只衡量验证任务的提交与回读，不能当成实际游戏每帧耗时。Play Mode 数据包含编辑器、场景和渲染开销，不能当作纯 GPU 节点耗时。

## 兼容性与边界

Unity 2022.3 / VFX Graph 14。这个版本的自定义节点 API 是内部接口，因此 `Editor/Unity.VisualEffectGraph.Editor.asmref` 将节点编译到现有 VFX 编辑器程序集；包源码和依赖版本没有修改。升级 VFX 包后需重新核对这些编辑器接口。运行时只使用编译好的 GPU 图和普通 `VisualEffect` 参数控制，不依赖编辑器程序集。

静态环境、平地、局部绕障，允许人物互相穿过。没有人与人避让、排队、动态障碍或迷宫寻路；封死路线可能无法通过。速度变化有平滑过渡。到达终点后回到出生点，适合作为持续演示。
