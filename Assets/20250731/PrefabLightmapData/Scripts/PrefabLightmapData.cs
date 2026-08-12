using UnityEngine;

namespace LiangZhu.Lighting
{

    [ExecuteAlways]
    public class PrefabLightmapData : MonoBehaviour
    {
        [System.Serializable]
        public struct RendererInfo
        {
            public Renderer renderer;            
            public int lightmapIndex;           
            public Vector4 lightmapScaleOffset;  
        }

        [SerializeField] RendererInfo[] m_RendererInfos;
        [SerializeField] Texture2D[] m_Lightmaps;    
        [SerializeField] Texture2D[] m_LightmapsDir; 
        [SerializeField] Texture2D[] m_ShadowMasks;   
        [SerializeField] LightmapsMode m_LightmapsMode;

        // 公开给编辑器 baker 写入
        public RendererInfo[] RendererInfos { get => m_RendererInfos; set => m_RendererInfos = value; }
        public Texture2D[] Lightmaps { get => m_Lightmaps; set => m_Lightmaps = value; }
        public Texture2D[] LightmapsDir { get => m_LightmapsDir; set => m_LightmapsDir = value; }
        public Texture2D[] ShadowMasks { get => m_ShadowMasks; set => m_ShadowMasks = value; }
        public LightmapsMode CapturedLightmapsMode { get => m_LightmapsMode; set => m_LightmapsMode = value; }

        void OnEnable()
        {
#if UNITY_EDITOR

            if (!Application.isPlaying || UnityEditor.Lightmapping.isRunning) return;
#endif
            Apply();
        }

        void OnDisable()
        {
            PrefabLightmapRegistry.Unregister(this);
        }

        /// <summary>
        /// 把本组件保存的 lightmap append 进全局数组(已存在则复用,避免重复增长),
        /// 然后逐个 Renderer 重映射 index 与 scaleOffset。可重入、幂等。
        /// </summary>
        public void Apply()
        {
            if (m_RendererInfos == null || m_RendererInfos.Length == 0) return;
            int n = m_Lightmaps != null ? m_Lightmaps.Length : 0;
            var storedMode = GetStoredLightmapsMode();
            var sceneLightmaps = LightmapSettings.lightmaps;
            int sceneLightmapCount = sceneLightmaps != null ? sceneLightmaps.Length : 0;
            //Debug.Log($"[PrefabLightmapData] '{name}' Apply Lightmaps: {n} 张, 模式 {storedMode}, 场景已有 {sceneLightmapCount} 张, 模式 {LightmapSettings.lightmapsMode}。");
            PrefabLightmapRegistry.RegisterOrRefresh(this);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 仅供编辑器预览管理器使用。移除临时注册的光图；Renderer 原状态由预览管理器恢复。
        /// </summary>
        public void RemoveEditorPreviewLightmaps()
        {
            PrefabLightmapRegistry.Unregister(this);
        }
#endif

        internal LightmapsMode StoredLightmapsMode => GetStoredLightmapsMode();

        internal void ApplyRendererMapping(int[] localToGlobal)
        {
            foreach (var info in m_RendererInfos)
            {
                if (info.renderer == null) continue;
                if (info.lightmapIndex < 0 || info.lightmapIndex >= localToGlobal.Length) continue;

                int g = localToGlobal[info.lightmapIndex];
                if (g < 0) continue;

                info.renderer.lightmapIndex = g;
                info.renderer.lightmapScaleOffset = info.lightmapScaleOffset;
            }
        }

        LightmapsMode GetStoredLightmapsMode()
        {
            // 以实际保存的贴图为准，不直接信任序列化枚举值。Unity 在编辑器初始化阶段
            // 可能返回 -1 等临时值，旧 Prefab 也可能已经把该值保存下来。
            if (m_LightmapsDir != null)
            {
                for (int i = 0; i < m_LightmapsDir.Length; i++)
                {
                    if (m_LightmapsDir[i] != null)
                        return LightmapsMode.CombinedDirectional;
                }
            }

            return LightmapsMode.NonDirectional;
        }

    }
}
