using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>捕获时独立求截线、奇偶填充，再生成米制栅格 SDF。不访问原组件私有数据。</summary>
public sealed class ContactSnapshotSdf : IDisposable
{
    struct Segment { public Vector2 a,b; public Segment(Vector2 p,Vector2 q){a=p;b=q;} }
    readonly List<Segment> segments = new List<Segment>();
    readonly List<float> crossings = new List<float>();
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<int> triangles = new List<int>();
    readonly HashSet<Renderer> visited = new HashSet<Renderer>();
    Mesh baked;
    bool[] inside;
    float[] scratch, distanceInside, distanceOutside, result, line, output;
    int[] sites;
    double[] cuts;
    int count;
    const float Far = 1e18f;

    public bool Build(DissolvePlaneContact source,int resolution,ref Texture2D texture,out string diagnostic)
    {
        diagnostic = "";
        if (!SystemInfo.SupportsTextureFormat(TextureFormat.RFloat)) { diagnostic="设备不支持 RFloat SDF。"; return false; }
        Allocate(resolution);
        Array.Clear(inside,0,inside.Length); visited.Clear();
        Vector3 origin,normal;
        if (!source.TryGetPlane(out origin,out normal)) { diagnostic="平面无效。"; return false; }
        Vector3 right=source.transform.right,up=source.transform.up;
        var worldToPlane=Matrix4x4.identity;
        worldToPlane.SetRow(0,new Vector4(right.x,right.y,right.z,-Vector3.Dot(origin,right)));
        worldToPlane.SetRow(1,new Vector4(up.x,up.y,up.z,-Vector3.Dot(origin,up)));
        worldToPlane.SetRow(2,new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(origin,normal)));
        var sources=source.contactRenderers.Count>0?source.contactRenderers:source.controller!=null?source.controller.controlledRenderers:null;
        if(sources==null){diagnostic="没有接触网格。";return false;}
        int totalSegments=0;
        foreach(var renderer in sources)
        {
            if(renderer==null || !renderer.gameObject.activeInHierarchy || !visited.Add(renderer) || renderer.gameObject==source.gameObject)continue;
            Bounds bounds=renderer.bounds;
            float radius=Vector3.Dot(new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)),bounds.extents);
            if(Mathf.Abs(Vector3.Dot(bounds.center-origin,normal))>radius+.001f)continue;
            var skin=renderer as SkinnedMeshRenderer;
            var filter=skin==null?renderer.GetComponent<MeshFilter>():null;
            var mesh=skin!=null?skin.sharedMesh:filter!=null?filter.sharedMesh:null;
            if(mesh==null)continue;
            if(skin!=null)
            {
                // 仅捕获时再次 BakeMesh，避免修改基础组件；这笔额外 CPU 成本会记录在捕获耗时中。
                if(baked==null)baked=new Mesh { name="Snapshot SDF pose",hideFlags=HideFlags.HideAndDontSave };
                skin.BakeMesh(baked,false);mesh=baked;
            }
            if(!mesh.isReadable){diagnostic=renderer.name+" 网格不可读，请启用 Read/Write 或使用闭合接触代理。";return false;}
            mesh.GetVertices(vertices);
            var matrix=worldToPlane*renderer.localToWorldMatrix;
            for(int i=0;i<vertices.Count;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
            segments.Clear();
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                if(mesh.GetTopology(sub)!=MeshTopology.Triangles)continue;
                mesh.GetTriangles(triangles,sub);
                for(int i=0;i+2<triangles.Count;i+=3)
                {
                    Vector2 a,b;
                    if(DissolvePlaneContact.IntersectTriangle(vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]],out a,out b))
                        segments.Add(new Segment(a,b));
                }
            }
            if(segments.Count==0)continue;
            totalSegments+=segments.Count;
            float dx=source.size.x/resolution,dy=source.size.y/resolution;
            foreach(var s in segments)
            {
                // 留一像素外部区域，避免截断的轮廓被误当成平面边界。
                if(Mathf.Max(Mathf.Abs(s.a.x),Mathf.Abs(s.b.x))>=source.size.x*.5f-dx ||
                   Mathf.Max(Mathf.Abs(s.a.y),Mathf.Abs(s.b.y))>=source.size.y*.5f-dy)
                {diagnostic="截线触及 SDF 范围边缘，请增大接触平面 Size。";return false;}
            }
            // 同一 Renderer 用奇偶规则，支持不相连轮廓与嵌套孔洞；不同 Renderer 取并集。
            for(int y=0;y<resolution;y++)
            {
                float py=(y+.5f)*dy-source.size.y*.5f;
                crossings.Clear();
                foreach(var s in segments)
                {
                    // 半开区间：共享顶点只计一次；水平边不参与扫描线交点。
                    if((s.a.y>py)==(s.b.y>py))continue;
                    crossings.Add(s.a.x+(py-s.a.y)*(s.b.x-s.a.x)/(s.b.y-s.a.y));
                }
                crossings.Sort();
                if((crossings.Count&1)!=0){diagnostic=renderer.name+" 截线不闭合（扫描线交点为奇数），不能可靠标记内外；保留普通快照。";return false;}
                for(int i=0;i<crossings.Count;i+=2)
                {
                    int begin=Mathf.Clamp(Mathf.CeilToInt((crossings[i]+source.size.x*.5f)/dx-.5f),0,resolution);
                    int end=Mathf.Clamp(Mathf.CeilToInt((crossings[i+1]+source.size.x*.5f)/dx-.5f),0,resolution);
                    for(int x=begin;x<end;x++)inside[y*resolution+x]=true;
                }
            }
        }
        int filled=0;foreach(bool value in inside)if(value)filled++;
        if(filled==0 || filled==inside.Length){diagnostic="SDF 没有同时包含内部和外部像素；请检查闭合网格、平面范围或提高 SDF 分辨率。";return false;}
        float pixelX=source.size.x/resolution,pixelY=source.size.y/resolution;
        Transform(true,distanceInside,pixelX,pixelY);
        Transform(false,distanceOutside,pixelX,pixelY);
        // 距离到另一类像素中心，再减去半个最小像素；边界误差约一个像素，非解析几何 SDF。
        float halfPixel=Mathf.Min(pixelX,pixelY)*.5f;
        for(int i=0;i<result.Length;i++)result[i]=inside[i]
            ? -Mathf.Max(0,Mathf.Sqrt(distanceOutside[i])-halfPixel)
            : Mathf.Max(0,Mathf.Sqrt(distanceInside[i])-halfPixel);
        if(texture==null || texture.width!=resolution)
        {
            Destroy(texture);
            texture=new Texture2D(resolution,resolution,TextureFormat.RFloat,false,true)
            {name="Contour SDF (metres)",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        }
        texture.SetPixelData(result,0);texture.Apply(false,false);
        diagnostic="SDF 就绪："+totalSegments+" 条截线，"+filled+" 个内部像素；单位米。";
        return true;
    }

    void Allocate(int n)
    {
        if(count==n)return;count=n;int pixels=n*n;
        inside=new bool[pixels];scratch=new float[pixels];distanceInside=new float[pixels];distanceOutside=new float[pixels];result=new float[pixels];
        line=new float[n];output=new float[n];sites=new int[n];cuts=new double[n+1];
    }
    void Transform(bool target,float[] destination,float dx,float dy)
    {
        // 可分离平方欧氏距离变换：先横向，再纵向。权重是实际米制像素尺寸的平方。
        for(int y=0;y<count;y++)
        {
            for(int x=0;x<count;x++)line[x]=inside[y*count+x]==target?0:Far;
            TransformLine(dx*dx);
            for(int x=0;x<count;x++)scratch[y*count+x]=output[x];
        }
        for(int x=0;x<count;x++)
        {
            for(int y=0;y<count;y++)line[y]=scratch[y*count+x];
            TransformLine(dy*dy);
            for(int y=0;y<count;y++)destination[y*count+x]=output[y];
        }
    }
    void TransformLine(double weight)
    {
        // 每个种子定义一条抛物线，维护它们的下包络。跳过无穷远种子，避免 INF-INF。
        int last=-1;
        for(int q=0;q<count;q++)
        {
            if(line[q]>=Far*.5f)continue;
            double crossing=double.NegativeInfinity;
            while(last>=0)
            {
                int v=sites[last];crossing=((double)line[q]-line[v]+weight*((double)q*q-(double)v*v))/(2*weight*(q-v));
                if(crossing>cuts[last])break;last--;
            }
            last++;sites[last]=q;cuts[last]=last==0?double.NegativeInfinity:crossing;cuts[last+1]=double.PositiveInfinity;
        }
        if(last<0){for(int q=0;q<count;q++)output[q]=Far;return;}
        int k=0;for(int q=0;q<count;q++){while(cuts[k+1]<q)k++;double delta=q-sites[k];output[q]=(float)(line[sites[k]]+weight*delta*delta);}
    }
    public void Dispose(){Destroy(baked);baked=null;}
    static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
}
