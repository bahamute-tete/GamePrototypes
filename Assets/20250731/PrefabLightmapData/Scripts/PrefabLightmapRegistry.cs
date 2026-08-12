using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiangZhu.Lighting
{
    /// <summary>
    /// 集中维护运行时追加到 LightmapSettings 的 Prefab 光图。
    /// 同一组三张贴图只注册一次；最后一个使用者离开时，移除由本注册表追加的条目，
    /// 并统一重映射仍然存活的 Prefab Renderer。
    /// </summary>
    internal static class PrefabLightmapRegistry
    {
        readonly struct LightmapKey : IEquatable<LightmapKey>
        {
            public readonly Texture2D Color;
            public readonly Texture2D Direction;
            public readonly Texture2D ShadowMask;

            public LightmapKey(Texture2D color, Texture2D direction, Texture2D shadowMask)
            {
                Color = color;
                Direction = direction;
                ShadowMask = shadowMask;
            }

            public bool IsValid => Color != null;

            public bool Equals(LightmapKey other)
            {
                return Color == other.Color &&
                       Direction == other.Direction &&
                       ShadowMask == other.ShadowMask;
            }

            public override bool Equals(object obj)
            {
                return obj is LightmapKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + GetObjectHash(Color);
                    hash = hash * 31 + GetObjectHash(Direction);
                    hash = hash * 31 + GetObjectHash(ShadowMask);
                    return hash;
                }
            }

            static int GetObjectHash(UnityEngine.Object value)
            {
                return value != null ? value.GetInstanceID() : 0;
            }
        }

        sealed class Entry
        {
            public int Index;
            public int ReferenceCount;
            public bool OwnedByRegistry;
        }

        sealed class Registration
        {
            public readonly HashSet<LightmapKey> Keys;
            public readonly LightmapsMode Mode;

            public Registration(HashSet<LightmapKey> keys, LightmapsMode mode)
            {
                Keys = keys;
                Mode = mode;
            }
        }

        static readonly Dictionary<LightmapKey, Entry> s_Entries =
            new Dictionary<LightmapKey, Entry>();

        static readonly Dictionary<PrefabLightmapData, Registration> s_Registrations =
            new Dictionary<PrefabLightmapData, Registration>();

        static LightmapKey[] s_LastLayout = Array.Empty<LightmapKey>();
        static LightmapsMode s_LastMode;
        static bool s_Initialized;
        static bool s_Refreshing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            UnsubscribeSceneEvents();
            s_Entries.Clear();
            s_Registrations.Clear();
            s_LastLayout = Array.Empty<LightmapKey>();
            s_LastMode = default;
            s_Initialized = false;
            s_Refreshing = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RegisterExistingOwners()
        {
            var owners = UnityEngine.Object.FindObjectsByType<PrefabLightmapData>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var owner in owners)
            {
                if (owner != null && owner.isActiveAndEnabled)
                    RegisterOrRefresh(owner);
            }
        }

        public static void RegisterOrRefresh(PrefabLightmapData owner)
        {
            if (owner == null || !HasValidLightmap(owner)) return;

            EnsureInitialized();

            var current = GetCurrentLightmaps();
            if (!LayoutMatches(current))
            {
                RefreshAfterExternalChange(current);
                current = GetCurrentLightmaps();
            }

            if (s_Registrations.TryGetValue(owner, out var existing))
            {
                var currentKeys = CollectUniqueKeys(owner);
                if (existing.Mode == owner.StoredLightmapsMode &&
                    existing.Keys.SetEquals(currentKeys))
                {
                    ApplyOwner(owner);
                    return;
                }

                Unregister(owner);
                current = GetCurrentLightmaps();
            }

            RegisterNew(owner, current);
        }

        public static void Unregister(PrefabLightmapData owner)
        {
            if (!s_Initialized || ReferenceEquals(owner, null)) return;

            var current = GetCurrentLightmaps();
            if (!LayoutMatches(current))
            {
                s_Registrations.Remove(owner);
                RefreshAfterExternalChange(current);
                return;
            }

            if (!s_Registrations.TryGetValue(owner, out var registration)) return;

            s_Registrations.Remove(owner);
            foreach (var key in registration.Keys)
            {
                if (s_Entries.TryGetValue(key, out var entry))
                    entry.ReferenceCount = Math.Max(0, entry.ReferenceCount - 1);
            }

            RemoveUnusedOwnedEntries(current);
        }

        static void RegisterNew(PrefabLightmapData owner, LightmapData[] current)
        {
            var mode = owner.StoredLightmapsMode;
            if (!ValidateMode(owner, mode, current)) return;

            var combined = new List<LightmapData>(current);
            var keys = new HashSet<LightmapKey>();
            var localToGlobal = CreateEmptyMapping(owner.Lightmaps);
            bool changed = false;

            for (int i = 0; i < localToGlobal.Length; i++)
            {
                var key = GetOwnerKey(owner, i);
                if (!key.IsValid) continue;

                if (!s_Entries.TryGetValue(key, out var entry))
                {
                    entry = new Entry
                    {
                        Index = combined.Count,
                        OwnedByRegistry = true,
                    };
                    s_Entries.Add(key, entry);
                    combined.Add(CreateLightmapData(key));
                    changed = true;
                }

                localToGlobal[i] = entry.Index;
                keys.Add(key);
            }

            if (keys.Count == 0) return;

            foreach (var key in keys)
                s_Entries[key].ReferenceCount++;

            s_Registrations.Add(owner, new Registration(keys, mode));

            if (changed)
            {
                var updated = combined.ToArray();
                LightmapSettings.lightmaps = updated;
                CaptureLayout(updated);
            }
            else
            {
                CaptureLayout(current);
            }

            owner.ApplyRendererMapping(localToGlobal);
        }

        static void RemoveUnusedOwnedEntries(LightmapData[] current)
        {
            var removeIndices = new HashSet<int>();
            var retainedOwnedKeys = new HashSet<LightmapKey>();

            foreach (var pair in s_Entries)
            {
                var entry = pair.Value;
                if (!entry.OwnedByRegistry) continue;

                if (entry.ReferenceCount == 0)
                    removeIndices.Add(entry.Index);
                else
                    retainedOwnedKeys.Add(pair.Key);
            }

            if (removeIndices.Count == 0) return;

            var combined = new List<LightmapData>(current.Length - removeIndices.Count);
            for (int i = 0; i < current.Length; i++)
            {
                if (!removeIndices.Contains(i))
                    combined.Add(current[i]);
            }

            var updated = combined.ToArray();
            LightmapSettings.lightmaps = updated;
            Reindex(updated, retainedOwnedKeys);
            RecalculateReferenceCounts();
            CaptureLayout(updated);
            ApplyAllOwners();
        }

        static void RefreshAfterExternalChange(LightmapData[] current)
        {
            if (s_Refreshing) return;

            s_Refreshing = true;
            try
            {
                var owners = new List<PrefabLightmapData>();
                foreach (var owner in s_Registrations.Keys)
                {
                    if (IsActiveOwner(owner))
                        owners.Add(owner);
                }

                s_Registrations.Clear();
                Reindex(current, null);

                var combined = new List<LightmapData>(current);
                var mappings = new Dictionary<PrefabLightmapData, int[]>();
                bool changed = false;
                bool hasMode = current.Length > 0;
                var requiredMode = GetModeFromLightmaps(current);
                if (hasMode && LightmapSettings.lightmapsMode != requiredMode)
                    LightmapSettings.lightmapsMode = requiredMode;

                foreach (var owner in owners)
                {
                    var mode = owner.StoredLightmapsMode;
                    if (!hasMode)
                    {
                        requiredMode = mode;
                        LightmapSettings.lightmapsMode = mode;
                        hasMode = true;
                    }
                    else if (mode != requiredMode)
                    {
                        LogModeConflict(owner, mode, requiredMode);
                        continue;
                    }

                    var keys = new HashSet<LightmapKey>();
                    var mapping = CreateEmptyMapping(owner.Lightmaps);
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        var key = GetOwnerKey(owner, i);
                        if (!key.IsValid) continue;

                        if (!s_Entries.TryGetValue(key, out var entry))
                        {
                            entry = new Entry
                            {
                                Index = combined.Count,
                                OwnedByRegistry = true,
                            };
                            s_Entries.Add(key, entry);
                            combined.Add(CreateLightmapData(key));
                            changed = true;
                        }

                        mapping[i] = entry.Index;
                        keys.Add(key);
                    }

                    if (keys.Count == 0) continue;

                    foreach (var key in keys)
                        s_Entries[key].ReferenceCount++;

                    s_Registrations.Add(owner, new Registration(keys, mode));
                    mappings.Add(owner, mapping);
                }

                LightmapData[] layout;
                if (changed)
                {
                    layout = combined.ToArray();
                    LightmapSettings.lightmaps = layout;
                }
                else
                {
                    layout = current;
                }

                CaptureLayout(layout);
                foreach (var pair in mappings)
                    pair.Key.ApplyRendererMapping(pair.Value);
            }
            finally
            {
                s_Refreshing = false;
            }
        }

        static void EnsureInitialized()
        {
            if (s_Initialized) return;

            s_Initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            var current = GetCurrentLightmaps();
            Reindex(current, null);
            CaptureLayout(current);
        }

        static void UnsubscribeSceneEvents()
        {
            if (!s_Initialized) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshForSceneChange();
        }

        static void OnSceneUnloaded(Scene scene)
        {
            RefreshForSceneChange();
        }

        static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            RefreshForSceneChange();
        }

        static void RefreshForSceneChange()
        {
            if (!s_Initialized || s_Refreshing) return;

            var current = GetCurrentLightmaps();
            if (LayoutMatches(current))
                ApplyAllOwners();
            else
                RefreshAfterExternalChange(current);
        }

        static void Reindex(LightmapData[] lightmaps, HashSet<LightmapKey> ownedKeys)
        {
            s_Entries.Clear();
            for (int i = 0; i < lightmaps.Length; i++)
            {
                var key = GetKey(lightmaps[i]);
                if (!key.IsValid || s_Entries.ContainsKey(key)) continue;

                s_Entries.Add(key, new Entry
                {
                    Index = i,
                    OwnedByRegistry = ownedKeys != null && ownedKeys.Contains(key),
                });
            }
        }

        static void RecalculateReferenceCounts()
        {
            foreach (var entry in s_Entries.Values)
                entry.ReferenceCount = 0;

            foreach (var registration in s_Registrations.Values)
            {
                foreach (var key in registration.Keys)
                {
                    if (s_Entries.TryGetValue(key, out var entry))
                        entry.ReferenceCount++;
                }
            }
        }

        static void ApplyAllOwners()
        {
            var deadOwners = new List<PrefabLightmapData>();
            foreach (var owner in s_Registrations.Keys)
            {
                if (!IsActiveOwner(owner))
                {
                    deadOwners.Add(owner);
                    continue;
                }

                ApplyOwner(owner);
            }

            foreach (var owner in deadOwners)
                s_Registrations.Remove(owner);

            if (deadOwners.Count > 0)
            {
                RecalculateReferenceCounts();
                RemoveUnusedOwnedEntries(GetCurrentLightmaps());
            }
        }

        static void ApplyOwner(PrefabLightmapData owner)
        {
            if (owner == null) return;

            var mapping = CreateEmptyMapping(owner.Lightmaps);
            for (int i = 0; i < mapping.Length; i++)
            {
                var key = GetOwnerKey(owner, i);
                if (key.IsValid && s_Entries.TryGetValue(key, out var entry))
                    mapping[i] = entry.Index;
            }

            owner.ApplyRendererMapping(mapping);
        }

        static bool ValidateMode(
            PrefabLightmapData owner,
            LightmapsMode mode,
            LightmapData[] current)
        {
            if (current.Length == 0)
            {
                LightmapSettings.lightmapsMode = mode;
                s_LastMode = mode;
                return true;
            }

            var currentMode = GetModeFromLightmaps(current);
            if (currentMode == mode)
            {
                // OnEnable 可能早于 Unity 设置全局模式。实际贴图已经足以确定模式，
                // 因此顺便把 -1 或旧枚举状态纠正为受支持的正式值。
                if (LightmapSettings.lightmapsMode != currentMode)
                    LightmapSettings.lightmapsMode = currentMode;

                s_LastMode = currentMode;
                return true;
            }

            LogModeConflict(owner, mode, currentMode);
            return false;
        }

        static void LogModeConflict(
            PrefabLightmapData owner,
            LightmapsMode prefabMode,
            LightmapsMode sceneMode)
        {
            Debug.LogError(
                $"[PrefabLightmapData] '{owner.name}' 的光照贴图模式为 {prefabMode}," +
                $"但当前场景模式为 {sceneMode}。两种模式不能共用全局 LightmapSettings。",
                owner);
        }

        static HashSet<LightmapKey> CollectUniqueKeys(PrefabLightmapData owner)
        {
            var keys = new HashSet<LightmapKey>();
            var colors = owner.Lightmaps;
            int count = colors != null ? colors.Length : 0;
            for (int i = 0; i < count; i++)
            {
                var key = GetOwnerKey(owner, i);
                if (key.IsValid)
                    keys.Add(key);
            }

            return keys;
        }

        static bool HasValidLightmap(PrefabLightmapData owner)
        {
            var colors = owner.Lightmaps;
            if (colors == null) return false;

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] != null) return true;
            }

            return false;
        }

        static bool IsActiveOwner(PrefabLightmapData owner)
        {
            return owner != null &&
                   owner.isActiveAndEnabled &&
                   owner.gameObject.scene.IsValid() &&
                   owner.gameObject.scene.isLoaded;
        }

        static int[] CreateEmptyMapping(Texture2D[] colors)
        {
            int count = colors != null ? colors.Length : 0;
            var mapping = new int[count];
            for (int i = 0; i < mapping.Length; i++)
                mapping[i] = -1;
            return mapping;
        }

        static LightmapKey GetOwnerKey(PrefabLightmapData owner, int index)
        {
            return new LightmapKey(
                GetTexture(owner.Lightmaps, index),
                GetTexture(owner.LightmapsDir, index),
                GetTexture(owner.ShadowMasks, index));
        }

        static Texture2D GetTexture(Texture2D[] textures, int index)
        {
            return textures != null && index >= 0 && index < textures.Length
                ? textures[index]
                : null;
        }

        static LightmapKey GetKey(LightmapData lightmap)
        {
            return lightmap == null
                ? default
                : new LightmapKey(lightmap.lightmapColor, lightmap.lightmapDir, lightmap.shadowMask);
        }

        static LightmapData CreateLightmapData(LightmapKey key)
        {
            return new LightmapData
            {
                lightmapColor = key.Color,
                lightmapDir = key.Direction,
                shadowMask = key.ShadowMask,
            };
        }

        static LightmapData[] GetCurrentLightmaps()
        {
            return LightmapSettings.lightmaps ?? Array.Empty<LightmapData>();
        }

        static LightmapsMode GetModeFromLightmaps(LightmapData[] lightmaps)
        {
            for (int i = 0; i < lightmaps.Length; i++)
            {
                if (lightmaps[i] != null && lightmaps[i].lightmapDir != null)
                    return LightmapsMode.CombinedDirectional;
            }

            return LightmapsMode.NonDirectional;
        }

        static bool LayoutMatches(LightmapData[] current)
        {
            if (GetModeFromLightmaps(current) != s_LastMode ||
                current.Length != s_LastLayout.Length)
            {
                return false;
            }

            for (int i = 0; i < current.Length; i++)
            {
                if (!s_LastLayout[i].Equals(GetKey(current[i])))
                    return false;
            }

            return true;
        }

        static void CaptureLayout(LightmapData[] lightmaps)
        {
            s_LastLayout = new LightmapKey[lightmaps.Length];
            for (int i = 0; i < lightmaps.Length; i++)
                s_LastLayout[i] = GetKey(lightmaps[i]);
            s_LastMode = GetModeFromLightmaps(lightmaps);
        }
    }
}
