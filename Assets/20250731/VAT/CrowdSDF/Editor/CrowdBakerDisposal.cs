using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX.SDF;
using Object = UnityEngine.Object;

namespace UnityEditor.VFX
{
    // Compatibility adapter for VFX 14. Its Dispose unconditionally calls
    // Object.Destroy on three materials. Destroy(null) also logs in Edit Mode,
    // so clearing those array entries before Dispose cannot fix the problem.
    static class CrowdBakerDisposal
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        internal static void Dispose(MeshToSDFBaker baker)
        {
            if(Application.isPlaying) {baker.Dispose();return;}
            Type type=typeof(MeshToSDFBaker);
            var disposed=type.GetField("m_IsDisposed",Flags);
            var ownsCommand=type.GetField("m_OwnsCommandBuffer",Flags);
            if(disposed==null || ownsCommand==null)
                throw new NotSupportedException("MeshToSDFBaker internals changed; review the VFX 14 editor disposal adapter before re-baking.");
            if((bool)disposed.GetValue(baker))return;
            bool owns=(bool)ownsCommand.GetValue(baker);
            var released=new HashSet<object>();
            foreach(var field in type.GetFields(Flags))
            {
                Type element=field.FieldType.IsArray?field.FieldType.GetElementType():field.FieldType;
                // Release only baker-owned temporary resources. In particular,
                // never destroy the input Mesh, shared ComputeShader or resources asset.
                if(element!=typeof(RenderTexture) && element!=typeof(Material) &&
                   element!=typeof(GraphicsBuffer) && element!=typeof(CommandBuffer))continue;
                if(element==typeof(CommandBuffer) && !owns)continue;
                object value=field.GetValue(baker);
                if(value is Array array)
                {
                    for(int i=0;i<array.Length;i++){Release(array.GetValue(i),released);array.SetValue(null,i);}
                }
                else Release(value,released);
                field.SetValue(baker,null);
            }
            disposed.SetValue(baker,true);
            GC.SuppressFinalize(baker);
        }

        static void Release(object value,HashSet<object> released)
        {
            if(value==null || !released.Add(value))return;
            if(value is GraphicsBuffer buffer)buffer.Dispose();
            else if(value is CommandBuffer commands)commands.Dispose();
            else if(value is RenderTexture texture)
            {
                if(texture!=null){texture.Release();Object.DestroyImmediate(texture);}
            }
            else if(value is Material material && material!=null)Object.DestroyImmediate(material);
        }
    }
}
