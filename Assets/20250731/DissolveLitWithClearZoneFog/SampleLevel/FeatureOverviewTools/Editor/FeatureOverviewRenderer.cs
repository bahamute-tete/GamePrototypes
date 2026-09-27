using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LiangZhu.Demo.Editor
{
    public static class FeatureOverviewRenderer
    {
        public static readonly float[] BasicTimes = { .6f, 3, 4.8f };
        public static readonly float[] MultiTimes = { 1.2f, 3, 4.8f };
        static Font font;
        static readonly Color White = new Color(.87f,.92f,.96f), Muted = new Color(.55f,.65f,.72f), Orange = new Color(1,.48f,.2f), Cyan = new Color(.22f,.8f,.84f);

        // Preserve typed values and live references; no JSON roundtrip of scene objects.
        public sealed class Snapshot
        {
            readonly DissolveController c;
            readonly float amount, offset, radius, width, intensity, noiseScale, edgeNoise, threshold;
            readonly DissolveController.DissolveMode mode;
            readonly DissolveController.DissolveSpace space;
            readonly Transform origin;
            readonly Texture2D noise;
            readonly Vector3 direction;
            readonly Color color;
            readonly bool reverse, radialReverse, autoToggle, animatedBounds;
            readonly Renderer[] renderers;
            readonly bool[] enabled;
            readonly MaterialPropertyBlock[] blocks;
            public Snapshot(DissolveController target)
            {
                c=target; amount=c.amount; offset=c.planeOffset; radius=c.radius;
                width=c.DissolveEdgeWidth; intensity=c.DissolveEdgeIntensity; color=c.DissolveEdgeColor;
                mode=c.mode; space=c.space; origin=c.worldOrigin; noise=c.noiseTexture;
                direction=c.axisDirection; reverse=c.directionReverse; radialReverse=c.radialReverse;
                noiseScale=c.noiseScale; edgeNoise=c.edgeNoiseStrength; threshold=c.hideThreshold;
                autoToggle=c.autoToggleRenderer; animatedBounds=c.refreshAnimatedBounds;
                renderers=c.controlledRenderers.ToArray(); enabled=renderers.Select(r=>r.enabled).ToArray();
                blocks=renderers.Select(r=>{var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b;}).ToArray();
            }
            public void Restore()
            {
                c.amount=amount;c.planeOffset=offset;c.radius=radius;c.DissolveEdgeWidth=width;c.DissolveEdgeIntensity=intensity;c.DissolveEdgeColor=color;
                c.mode=mode;c.space=space;c.worldOrigin=origin;c.noiseTexture=noise;c.axisDirection=direction;c.directionReverse=reverse;c.radialReverse=radialReverse;
                c.noiseScale=noiseScale;c.edgeNoiseStrength=edgeNoise;c.hideThreshold=threshold;c.autoToggleRenderer=autoToggle;c.refreshAnimatedBounds=animatedBounds;
                c.controlledRenderers.Clear();c.controlledRenderers.AddRange(renderers);c.ForceRefresh();
                for(int i=0;i<renderers.Length;i++){renderers[i].enabled=enabled[i];renderers[i].SetPropertyBlock(blocks[i]);}
            }
        }

        // Each refresh uses the actual Timeline asset and bindings, with a separate playable graph.
        // Stop destroys that graph (and invokes track restoration) BEFORE restoring our snapshot.
        public static void WithTimeline(PlayableDirector source, Action<PlayableDirector> sample)
        {
            var targets=((TimelineAsset)source.playableAsset).GetOutputTracks().OfType<DissolveTrack>()
                .Select(t=>source.GetGenericBinding(t) as DissolveController).Where(c=>c).Distinct().ToArray();
            var snapshots=targets.Select(c=>new Snapshot(c)).ToArray();
            var go=new GameObject("Temporary Demo Timeline Evaluation"){hideFlags=HideFlags.HideAndDontSave};
            var director=go.AddComponent<PlayableDirector>();director.playOnAwake=false;
            director.playableAsset=source.playableAsset;director.extrapolationMode=DirectorWrapMode.Hold;
            try {
                foreach(var track in ((TimelineAsset)source.playableAsset).GetOutputTracks())director.SetGenericBinding(track,source.GetGenericBinding(track));
                director.RebuildGraph();sample(director);
            } finally {
                try { director.Stop();Object.DestroyImmediate(go); }
                finally { foreach(var snapshot in snapshots)snapshot.Restore(); }
            }
        }
        public static void Refresh(FeatureOverviewData d,int panel)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请退出 Play 再刷新。");
            if(UnityEngine.SceneManagement.SceneManager.sceneCount!=1)throw new InvalidOperationException("请单独打开 FeatureOverview 场景。");
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(!pipeline||pipeline.msaaSampleCount!=4||!pipeline.supportsCameraOpaqueTexture)throw new InvalidOperationException("需要 Forward URP / 4× MSAA / Opaque Texture；工具不会修改项目设置。");
            var active=d.stages.Select(s=>s.activeSelf).ToArray();
            var snapshots=d.controllers.Select(c=>new Snapshot(c)).ToArray();bool fog=RenderSettings.fog;
            try {
                RenderSettings.fog=false;foreach(var s in d.stages)s.SetActive(false);
                if(d.previews==null||d.previews.Length!=17)d.previews=new Texture2D[17];
                for(int p=0;p<6;p++) {
                    if(panel>=0&&p!=panel)continue;
                    EditorUtility.DisplayProgressBar("不透明物体 A2C 溶解","刷新画面 "+(p+1)+" / 6",p/6f);
                    if(p==0)for(int i=0;i<3;i++)Capture(d,0,i,c=>{c.space=d.noiseSpace;c.amount=d.noiseAmounts[i];c.noiseTexture=d.proceduralNoise?null:d.sharedNoise;},false,372,540);
                    if(p==1)for(int i=0;i<2;i++)Capture(d,1,3+i,c=>c.planeOffset=d.planeOffsets[i],true,564,530);
                    if(p==2)for(int i=0;i<2;i++)Capture(d,2,5+i,c=>{c.radius=d.radialRadius;c.radialReverse=i==1;},true,564,530);
                    if(p==3){Capture(d,3,7,c=>c.amount=d.localAmount,true,564,390);Capture(d,4,8,c=>c.planeOffset=d.worldOffset,true,564,390);}
                    if(p==4||p==5) {
                        bool multi=p==5;int stage=multi?6:5,slot=multi?13:9;
                        d.stages[stage].SetActive(true);var c=d.controllers[stage];c.ForceRefresh();
                        WithTimeline(multi?d.multiDirector:d.director, director=>{
                            for(int i=0;i<4;i++){
                                director.time=i<3?(multi?MultiTimes[i]:BasicTimes[i]):(multi?d.multiPreviewTime:d.previewTime);director.Evaluate();
                                if(multi)d.multiValues[i]=new Vector3(c.planeOffset,c.DissolveEdgeWidth,c.DissolveEdgeIntensity);else d.timelineAmounts[i]=c.amount;
                                d.previews[slot+i]=Save(Render(d.cameras[stage],372,300),"A2C_"+(slot+i).ToString("00"));
                            }
                        });d.stages[stage].SetActive(false);
                    }
                }
                Compose(d,true);EditorUtility.SetDirty(d);EditorSceneManager.MarkSceneDirty(d.gameObject.scene);AssetDatabase.SaveAssets();
            } finally {
                for(int i=0;i<active.Length;i++)d.stages[i].SetActive(active[i]);
                foreach(var snapshot in snapshots)snapshot.Restore();
                RenderSettings.fog=fog;EditorUtility.ClearProgressBar();
            }
        }
        static void Capture(FeatureOverviewData d,int stage,int slot,Action<DissolveController> configure,bool guides,int w,int h)
        {
            var c=d.controllers[stage];d.stages[stage].SetActive(true);configure(c);c.ForceRefresh();
            GameObject helper=null;Material material=null;
            try {
                if(guides){helper=new GameObject("Temporary Guides");material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",Cyan);Guides(helper,c,material);}
                d.previews[slot]=Save(Render(d.cameras[stage],w,h),"A2C_"+slot.ToString("00"));
            } finally {if(helper)Object.DestroyImmediate(helper);if(material)Object.DestroyImmediate(material);d.stages[stage].SetActive(false);}
        }
        static void GuideLine(GameObject root,Material material,params Vector3[] points)
        {
            var go=new GameObject("Guide");go.transform.SetParent(root.transform);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=.014f;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        }
        static void Guides(GameObject root,DissolveController c,Material m)
        {
            if(c.space==DissolveController.DissolveSpace.Local){
                foreach(var r in c.controlledRenderers){var origin=r.bounds.center;var dir=r.transform.TransformDirection(c.axisDirection).normalized;GuideLine(root,m,origin-dir*.7f,origin+dir*.7f);}
                return;
            }
            var o=c.worldOrigin.position;
            for(int axis=0;axis<3;axis++){Vector3 v=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;GuideLine(root,m,o-v*.1f,o+v*.1f);}
            if(c.mode==DissolveController.DissolveMode.Radial){
                for(int axis=0;axis<3;axis++){var points=new Vector3[97];for(int n=0;n<97;n++){float a=n*Mathf.PI*2/96;float x=Mathf.Cos(a)*c.radius,y=Mathf.Sin(a)*c.radius;points[n]=o+(axis==0?new Vector3(x,y,0):axis==1?new Vector3(x,0,y):new Vector3(0,x,y));}GuideLine(root,m,points);}
            }else{
                Vector3 normal=c.axisDirection.normalized;
                Vector3 u=Vector3.Cross(normal,Mathf.Abs(normal.z)<.9f?Vector3.forward:Vector3.right).normalized*1.65f,v=Vector3.Cross(normal,u.normalized)*1.05f;
                Vector3 p=o+normal*c.planeOffset;GuideLine(root,m,p-u-v,p+u-v,p+u+v,p-u+v,p-u-v);
                Vector3 a=o+u*1.08f,b=a+normal*2.45f;GuideLine(root,m,a,b,b-normal*.22f+v*.13f,b,b-normal*.22f-v*.13f);
            }
        }

        public static Texture2D Render(Camera camera,int width,int height)
        {
            var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;var oldActive=RenderTexture.active;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};
            var resolve=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            try {
                rt.Create();resolve.Create();camera.targetTexture=rt;camera.aspect=(float)width/height;Canvas.ForceUpdateCanvases();
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("URP 尚未就绪或不支持 SingleCameraRequest。");
                RenderPipeline.SubmitRenderRequest(camera,request);Graphics.Blit(rt,resolve);
                RenderTexture.active=resolve;var tex=new Texture2D(width,height,TextureFormat.RGB24,false,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();return tex;
            } finally {camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=oldActive;rt.Release();resolve.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(resolve);}
        }
        static Texture2D Save(Texture2D texture,string name)
        {
            string p=FeatureOverviewBuilder.AssetsRoot+"/Previews/"+name+".png";
            File.WriteAllBytes(p,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;importer.sRGBTexture=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static void Block(Transform parent,float x,float y,float w,float h,Color color){Rect(parent,"Block",x,y,w,h).gameObject.AddComponent<Image>().color=color;}
        static void Text(Transform parent,string value,float x,float y,float w,float h,int size,Color color,bool bold=false)
        {
            var t=Rect(parent,value,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=color;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;
        }
        static void Picture(Transform parent,Texture tex,float x,float y,float w,float h){var r=Rect(parent,"Preview",x,y,w,h).gameObject.AddComponent<RawImage>();r.texture=tex;r.raycastTarget=false;}
        static void Line(Transform parent,Vector2 a,Vector2 b,Color color,float thickness=3)
        {
            var r=Rect(parent,"Curve",a.x,a.y,(b-a).magnitude,thickness);r.pivot=new Vector2(0,0.5f);r.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);r.gameObject.AddComponent<Image>().color=color;
        }
        public static void Compose(FeatureOverviewData d,bool persist)
        {
            if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},48);
            var layout=Rect(d.boardCanvas.transform,"Temporary Capture Layout",0,0,3840,2160);
            bool active=d.boardImage.gameObject.activeSelf;d.boardImage.gameObject.SetActive(false);
            try {
                Block(layout,0,0,3840,2160,new Color(.025f,.037f,.052f));
                if(d.labels){
                    Text(layout,"不透明物体 A2C 溶解",76,48,3000,104,78,White,true);
                    Text(layout,"三种溶解类型 · Local / World 空间 · Timeline 多参数控制",80,159,3200,52,34,Muted);
                    Text(layout,"OPAQUE DISSOLVE",3060,77,720,45,31,Cyan);
                    Text(layout,"FEATURE STUDY   /   01—06",3060,133,720,42,26,Muted);
                }
                string[] titles={"Noise / 噪声溶解","Direction / 定向溶解","Radial / 径向溶解","Local / World 空间","Timeline / 无需手动打帧","Timeline / 多参数自由控制"};
                string[] descriptions={"Local / World 均由 Amount 控制","World · 按世界距离推进，支持反转消失侧","同一 Origin + Radius · World 半径单位为米","Local 随物体旋转；World 跨物体形成统一切面","绑定 Controller → 选择参数 → 设置 Clip","三条独立轨道 → 同一个 World Direction Controller"};
                for(int p=0;p<6;p++){
                    var card=Rect(layout,titles[p],72+p%3*1240,265+p/3*875,1216,845);
                    Block(card,0,0,1216,845,new Color(.044f,.059f,.077f));Block(card,0,0,1216,3,p>=4?Orange:Cyan);
                    if(d.labels){Text(card,(p+1).ToString("00"),28,28,70,54,27,Cyan);Text(card,titles[p],98,24,1090,64,42,White,true);Text(card,descriptions[p],30,98,1150,50,29,Muted);}
                    if(p==0){
                        for(int i=0;i<3;i++){Picture(card,d.previews[i],28+i*392,166,372,540);if(d.labels)Text(card,"Amount  "+d.noiseAmounts[i].ToString("0.00"),42+i*392,720,360,44,30,i==0?Muted:Orange);}
                        if(d.labels)Text(card,"空间决定噪声采样坐标 · 当前 "+d.noiseSpace+" / "+(d.proceduralNoise?"过程噪声":"共享 Noise 贴图"),30,790,1150,40,27,Muted);
                    }
                    if(p==1||p==2){
                        int slot=p==1?3:5;
                        for(int i=0;i<2;i++)Picture(card,d.previews[slot+i],28+i*596,166,564,530);
                        if(d.labels){
                            Text(card,p==1?"Plane Offset  "+d.planeOffsets[0].ToString("0.00")+" m":"默认 · 消去球内",38,711,560,48,30,White);
                            Text(card,p==1?"Plane Offset  "+d.planeOffsets[1].ToString("0.00")+" m":"Reverse · 保留球内",634,711,560,48,30,Orange);
                            Text(card,p==1?"Origin = 底部十字 · Direction = ↑ · 青线 = 溶解平面":"Origin = 球心十字 · Radius = "+d.radialRadius.ToString("0.00")+" m · 青线 = 球范围",30,785,1170,42,27,Muted);
                        }
                    }
                    if(p==3){
                        for(int i=0;i<2;i++)Picture(card,d.previews[7+i],28+i*596,150,564,390);
                        if(d.labels){
                            Text(card,"Local · Amount "+d.localAmount.ToString("0.00"),38,541,565,44,29,White);
                            Text(card,"World · Plane Offset "+d.worldOffset.ToString("0.00")+" m",634,541,565,44,29,Orange);
                            Block(card,26,605,1164,218,new Color(.029f,.042f,.057f));
                            string[,] table={{"类型","Local","World"},{"Noise","Amount；局部噪声坐标","Amount；世界噪声坐标"},{"Direction","Amount；各物体 Bounds 归一化","Origin + Direction + Plane Offset"},{"Radial","Amount；各物体 Bounds 中心","Origin + Radius"}};
                            for(int row=0;row<4;row++)for(int col=0;col<3;col++)Text(card,table[row,col],new[]{40,213,700}[col],612+row*51,new[]{172,482,505}[col],44,row==0?28:26,row==0?Cyan:Muted);
                        }
                    }
                    if(p==4||p==5)TimelineCard(d,card,p==5);
                }
                if(d.labels){Text(layout,"无需额外编写脚本，无需手动录制动画关键帧；绑定现成的 DissolveController，通过 Dissolve Clip 配置参数动画。",80,2028,3710,48,30,White);Text(layout,"UNITY URP / FORWARD / 4× MSAA · 编辑器静态预览 · 3840 × 2160",80,2090,1620,40,25,Muted);Text(layout,"支持 Amount、Plane Offset、Radius、Edge Width、Edge Intensity；可用参数随模式变化。",1760,2090,2020,40,25,Cyan);}
                Canvas.ForceUpdateCanvases();var texture=Render(d.boardCamera,3840,2160);
                if(persist)d.boardImage.texture=Save(texture,d.labels?"FeatureOverview4K":"FeatureOverview4K_Clean");else Object.DestroyImmediate(texture);
            }finally{Object.DestroyImmediate(layout.gameObject);d.boardImage.gameObject.SetActive(active);Canvas.ForceUpdateCanvases();}
        }
        static void TimelineCard(FeatureOverviewData d,Transform card,bool multi)
        {
            var source=multi?d.multiDirector:d.director;
            var asset=(TimelineAsset)source.playableAsset;
            var tracks=asset.GetOutputTracks().OfType<DissolveTrack>().ToArray();
            double duration=Math.Max(.01,asset.duration);float time=multi?d.multiPreviewTime:d.previewTime;
            int index=0;
            foreach(var track in tracks){
                float y=multi?158+index*62:163;
                foreach(var clip in track.GetClips()){
                    var b=((DissolveClip)clip.asset).template;
                    string unit=multi&&track.parameter!=DissolveParameter.EdgeIntensity?" m":"";
                    if(d.labels)Text(card,track.parameter+"  "+b.startValue.ToString("0.##")+" → "+b.endValue.ToString("0.##")+unit+"  / "+clip.duration.ToString("0.##")+" s",30,y,800,40,multi?27:30,White);
                    float x=multi?745:58,w=multi?420:1084,top=multi?y+3:220,h=multi?37:104;
                    Block(card,x,top,w,h+5,new Color(.026f,.038f,.052f));
                    float left=x+(float)(clip.start/duration)*w,right=x+(float)(clip.end/duration)*w;
                    float minimum=0,maximum=1;
                    if(b.curve!=null)for(int n=0;n<=80;n++){float value=b.curve.Evaluate(n/80f);minimum=Mathf.Min(minimum,value);maximum=Mathf.Max(maximum,value);}
                    for(int n=0;n<80;n++){
                        float a=n/80f,z=(n+1)/80f;
                        float va=b.curve==null?a:b.curve.Evaluate(a),vz=b.curve==null?z:b.curve.Evaluate(z);
                        Line(card,new Vector2(Mathf.Lerp(left,right,a),top+h*(1-Mathf.InverseLerp(minimum,maximum,va))),new Vector2(Mathf.Lerp(left,right,z),top+h*(1-Mathf.InverseLerp(minimum,maximum,vz))),index==1?Cyan:Orange,3);
                    }
                    float marker=x+Mathf.Clamp01(time/(float)duration)*w;Line(card,new Vector2(marker,top-3),new Vector2(marker,top+h+7),White,2);
                }
                index++;
            }
            if(d.labels){
                string options=string.Join(" / ",tracks.SelectMany(t=>t.GetClips()).Select(c=>((DissolveClip)c.asset).template.endBehaviour.ToString()).Distinct());
                Text(card,"LIVE "+time.ToString("0.00")+" s  ·  After Clip: "+options,30,343,1160,38,26,Cyan);
                string phases="";
                if(!multi){var clip=tracks.First().GetClips().First();var b=((DissolveClip)clip.asset).template;var keys=b.curve?.keys;
                    if(keys!=null)phases=string.Join(" → ",keys.Skip(1).Select((key,i)=>{float delta=(key.value-keys[i].value)*(b.endValue-b.startValue);return Mathf.Abs(delta)<.0001f?"保持":delta>0?"溶解":"恢复";}));}
                Text(card,multi?"各参数独立设置起止值 / 时长 / 曲线":"实际 Clip 时间曲线："+phases,30,381,1150,34,26,Muted);
            }
            for(int i=0;i<3;i++){
                int slot=(multi?13:9)+i;
                Picture(card,d.previews[slot],28+i*392,420,372,288);
                if(d.labels){
                    float sample=multi?MultiTimes[i]:BasicTimes[i];Text(card,sample.ToString("0.0")+" s",40+i*392,711,366,37,29,White);
                    if(multi){Vector3 v=d.multiValues[i];Text(card,"P "+v.x.ToString("0.00")+" m · W "+v.y.ToString("0.000")+" m",40+i*392,751,370,34,25,Orange);Text(card,"Intensity "+v.z.ToString("0.00"),40+i*392,786,370,33,25,Muted);}
                    else Text(card,"Amount "+d.timelineAmounts[i].ToString("0.00")+(d.timelineAmounts[i]>=.995f?" · 已消失":""),40+i*392,751,373,39,27,Orange);
                }
            }
            if(d.labels&&!multi)Text(card,"Hold End / Restore Original 可选；停止 Timeline 后恢复原值",30,803,1160,36,26,Muted);
        }
        public static void Validate(FeatureOverviewData d)
        {
            if(d.previews.Any(t=>!t))throw new Exception("Missing preview");
            if(d.controllers.Any(c=>!c||c.controlledRenderers.Count==0||c.controlledRenderers.Any(r=>!r)))throw new Exception("Lost renderer binding");
            foreach(var c in d.controllers){if(c.UsesWorldGeometry&&!c.worldOrigin)throw new Exception("Missing World Origin");foreach(var r in c.controlledRenderers)if(r.sharedMaterial.shader.name!="Custom/LiangZhu/Opaque_Dissolve_Lit")throw new Exception("Non-opaque demo material");}
            foreach(var pair in new[]{new[]{0,1},new[]{3,4},new[]{5,6},new[]{7,8},new[]{9,10},new[]{13,14}})
                if(File.ReadAllBytes(AssetDatabase.GetAssetPath(d.previews[pair[0]])).SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(d.previews[pair[1]]))))throw new Exception("Identical comparison images "+pair[0]+"/"+pair[1]);
            foreach(var message in ShaderUtil.GetShaderMessages(Shader.Find("Custom/LiangZhu/Opaque_Dissolve_Lit")))if(message.severity.ToString()=="Error")throw new Exception(message.message);
            var board=(Texture2D)d.boardImage.texture;if(board.width!=3840||board.height!=2160)throw new Exception("Export dimensions");
            Debug.Log("A2C_OVERVIEW_VALIDATED: 17 previews, opaque materials, World origins, distinct comparisons, 4K");
        }
    }
}
