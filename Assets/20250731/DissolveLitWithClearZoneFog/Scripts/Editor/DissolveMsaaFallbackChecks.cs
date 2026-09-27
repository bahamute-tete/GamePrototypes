using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DissolveMsaaFallbackChecks
{
    [MenuItem("Tools/LiangZhu/Dissolve/Validate A2C Dither Fallback")]
    public static void Run()
    {
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(!pipeline||pipeline.msaaSampleCount<4)throw new InvalidOperationException("For this A/B check, use the existing 4x MSAA pipeline.");
        var shader=Shader.Find("Custom/LiangZhu/Opaque_Dissolve_Lit");
        foreach(var m in ShaderUtil.GetShaderMessages(shader))if(m.severity.ToString()=="Error")throw new Exception(m.message);
        var original=SceneManager.GetActiveScene();bool dirty=original.isDirty;
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Isolated A2C Dither Check");SceneManager.MoveGameObjectToScene(root,scene);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.transform.localScale=Vector3.one*2;
        var material=new Material(shader);quad.GetComponent<Renderer>().sharedMaterial=material;
        material.SetColor("_BaseColor",Color.black);material.SetColor("_EmissionColor",Color.white);
        material.SetFloat("_DissolveEnabled",1);material.SetFloat("_DissolveCoverageWidth",.5f);material.SetFloat("_FresnelIntensity",0);material.SetFloat("_FogEnable",0);material.SetFloat("_DissolveBrightnessFade",0);
        var c=root.AddComponent<DissolveController>();c.controlledRenderers.Add(quad.GetComponent<Renderer>());c.space=DissolveController.DissolveSpace.World;c.mode=DissolveController.DissolveMode.Direction;c.worldOrigin=root.transform;c.axisDirection=Vector3.up;c.planeOffset=0;c.edgeNoiseStrength=0;c.DissolveEdgeIntensity=0;c.ForceRefresh();
        var camera=new GameObject("Check Camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.scene=scene;
        camera.transform.position=new Vector3(0,0,-3);camera.orthographic=true;camera.orthographicSize=1;camera.enabled=false;camera.allowMSAA=true;camera.allowHDR=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=false;extra.antialiasing=AntialiasingMode.None;extra.requiresColorOption=CameraOverrideOption.Off;extra.requiresDepthOption=CameraOverrideOption.Off;
        bool fog=RenderSettings.fog;Texture2D single=null,multi=null,repeat=null;
        try {
            RenderSettings.fog=false;Directory.CreateDirectory("Temp/DissolveMsaaFallbackChecks");
            single=Capture(camera,1);multi=Capture(camera,4);repeat=Capture(camera,1);
            var a=single.GetPixels();var b=multi.GetPixels();var again=repeat.GetPixels();
            int black=0,white=0,intermediateSingle=0,intermediateMulti=0;
            for(int y=119;y<137;y++)for(int x=40;x<216;x++){
                float av=a[y*256+x].r,bv=b[y*256+x].r;
                if(av<.05f)black++;else if(av>.95f)white++;else intermediateSingle++;
                if(bv>.05f&&bv<.95f)intermediateMulti++;
            }
            if(black<500||white<500||intermediateSingle>10)throw new Exception($"1x must have binary dither: black={black}, white={white}, partial={intermediateSingle}");
            if(intermediateMulti<1000)throw new Exception("4x must retain fractional A2C coverage: "+intermediateMulti);
            if(!a.SequenceEqual(again))throw new Exception("Dither changed between identical frames");
            c.planeOffset=3;c.ForceRefresh();var gone=Capture(camera,1);
            try{if(gone.GetPixels().Any(p=>p.r>.05f))throw new Exception("Fully dissolved pixels survive");}finally{Object.DestroyImmediate(gone);}
            c.planeOffset=-3;c.ForceRefresh();var full=Capture(camera,1);
            try{if(full.GetPixel(128,128).r<.95f)throw new Exception("Fully visible coverage clipped");}finally{Object.DestroyImmediate(full);}
            File.WriteAllBytes("Temp/DissolveMsaaFallbackChecks/NoMSAA_Dither.png",single.EncodeToPNG());
            File.WriteAllBytes("Temp/DissolveMsaaFallbackChecks/MSAA4_A2C.png",multi.EncodeToPNG());
            Debug.Log($"DISSOLVE_MSAA_FALLBACK_PASSED: 1x black={black}, white={white}, fractional={intermediateSingle}; 4x fractional={intermediateMulti}; stable pattern; coverage endpoints; scene untouched");
        }finally{
            RenderSettings.fog=fog;if(single)Object.DestroyImmediate(single);if(multi)Object.DestroyImmediate(multi);if(repeat)Object.DestroyImmediate(repeat);
            Object.DestroyImmediate(root);Object.DestroyImmediate(material);EditorSceneManager.ClosePreviewScene(scene);
            if(SceneManager.GetActiveScene()!=original||original.isDirty!=dirty)Debug.LogError("Fallback test changed user scene state");
        }
    }
    static Texture2D Capture(Camera camera,int samples)
    {
        var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){antiAliasing=samples};
        var resolved=new RenderTexture(256,256,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        try{
            target.Create();resolved.Create();camera.targetTexture=target;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            Graphics.Blit(target,resolved);RenderTexture.active=resolved;
            var image=new Texture2D(256,256,TextureFormat.RGB24,false,true);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();return image;
        }finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();resolved.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(resolved);}
    }
}
