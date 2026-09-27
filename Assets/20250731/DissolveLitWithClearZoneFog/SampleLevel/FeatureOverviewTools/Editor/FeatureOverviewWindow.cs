using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

namespace LiangZhu.Demo.Editor
{
    public sealed class FeatureOverviewWindow : EditorWindow
    {
        int panel;
        bool queued;
        Vector2 scroll;
        static readonly string[] Titles={"Noise / 噪声溶解","Direction / 定向溶解","Radial / 径向溶解","Local / World 空间","Timeline / 无需手动打帧","Timeline / 多参数自由控制"};
        [MenuItem("Tools/LiangZhu/Feature Overview")]
        public static void Open(){var w=GetWindow<FeatureOverviewWindow>("A2C 溶解 Demo");w.minSize=new Vector2(440,530);}
        void OnGUI()
        {
            var d=FindObjectOfType<FeatureOverviewData>();
            if(!d||!d.multiDirector){EditorGUILayout.HelpBox("请打开新版 FeatureOverview 场景。首次升级使用 Rebuild A2C Feature Overview（自动备份）。",MessageType.Info);return;}
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("不透明物体 A2C 溶解",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("无需 Play。设置参数后刷新对应画面。Timeline 值来自真实 Clip 求值；预览不会改变原控制器状态。",MessageType.Info);
            panel=EditorGUILayout.Popup("画面",panel,Titles);
            Undo.RecordObject(d,"Change A2C Demo Settings");EditorGUI.BeginChangeCheck();
            if(panel==0){
                d.noiseSpace=(DissolveController.DissolveSpace)EditorGUILayout.EnumPopup("Noise Space",d.noiseSpace);
                d.proceduralNoise=EditorGUILayout.Toggle("使用过程噪声",d.proceduralNoise);
                for(int i=0;i<3;i++)d.noiseAmounts[i]=EditorGUILayout.Slider("Amount "+(i+1),d.noiseAmounts[i],0,1);
            }
            if(panel==1)for(int i=0;i<2;i++)d.planeOffsets[i]=EditorGUILayout.Slider("Plane Offset "+(i+1)+" (m)",d.planeOffsets[i],-.2f,2.5f);
            if(panel==2)d.radialRadius=EditorGUILayout.Slider("Radius (m)",d.radialRadius,.05f,2.5f);
            if(panel==3){d.localAmount=EditorGUILayout.Slider("Local Amount",d.localAmount,0,1);d.worldOffset=EditorGUILayout.Slider("World Plane Offset (m)",d.worldOffset,-.2f,2.5f);}
            if(EditorGUI.EndChangeCheck()){EditorUtility.SetDirty(d);EditorSceneManager.MarkSceneDirty(d.gameObject.scene);}
            if(panel>=4){
                bool multi=panel==5;var director=multi?d.multiDirector:d.director;
                EditorGUI.BeginChangeCheck();float t=EditorGUILayout.Slider("预览时间 (s)",multi?d.multiPreviewTime:d.previewTime,0,(float)director.duration);
                if(EditorGUI.EndChangeCheck()){
                    if(multi)d.multiPreviewTime=t;else d.previewTime=t;
                    if(!queued){queued=true;int requested=panel;EditorApplication.delayCall+=()=>{queued=false;if(d)Refresh(d,requested);};}
                }
                if(GUILayout.Button("打开对应 Timeline")){Selection.activeGameObject=director.gameObject;EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");}
                if(GUILayout.Button("刷新 Timeline 画面"))Refresh(d,panel);
                EditorGUILayout.HelpBox("在 Dissolve Track 选择参数；在 Clip 配置 Start Value / End Value / Progress Curve / After Clip。修改资产后刷新即可。\n支持 Amount、Plane Offset、Radius、Edge Width、Edge Intensity；可用参数随模式变化。",MessageType.None);
                var texture=d.previews[multi?16:12];
                if(texture){var r=GUILayoutUtility.GetAspectRect(372f/300);EditorGUI.DrawPreviewTexture(r,texture,null,ScaleMode.ScaleToFit);}
                if(multi){var v=d.multiValues[3];EditorGUILayout.LabelField($"实际值：P {v.x:0.000} m  /  W {v.y:0.000} m  /  Intensity {v.z:0.000}");}
                else EditorGUILayout.LabelField("实际 Amount："+d.timelineAmounts[3].ToString("0.000"));
            }
            EditorGUILayout.Space();
            if(GUILayout.Button("刷新单格"))Refresh(d,panel);
            if(GUILayout.Button("刷新全部"))Refresh(d,-1);
            if(GUILayout.Button("恢复默认展示参数")){
                d.noiseAmounts=new[]{0f,.35f,.7f};d.planeOffsets=new[]{.65f,1.45f};d.radialRadius=1.05f;
                d.localAmount=.5f;d.worldOffset=1.1f;d.noiseSpace=DissolveController.DissolveSpace.Local;d.proceduralNoise=false;
                d.previewTime=1.2f;d.multiPreviewTime=3;Refresh(d,-1);
            }
            EditorGUILayout.HelpBox("恢复默认展示参数不覆盖你编辑过的 Timeline。重建 Demo 会备份场景/Timeline，再恢复完整默认布局和 Clip。",MessageType.None);
            if(GUILayout.Button("导出带标签 4K PNG"))Export(d,true);
            if(GUILayout.Button("导出无标签 4K PNG"))Export(d,false);
            EditorGUILayout.EndScrollView();
        }
        static void Refresh(FeatureOverviewData d,int panel)
        {
            try{FeatureOverviewRenderer.Refresh(d,panel);EditorSceneManager.SaveScene(d.gameObject.scene);}
            catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("Demo 刷新失败",e.Message,"确定");}
        }
        static void Export(FeatureOverviewData d,bool labels)
        {
            string path=EditorUtility.SaveFilePanel("导出 4K PNG","",labels?"OpaqueA2CDissolve":"OpaqueA2CDissolve_Clean","png");
            if(string.IsNullOrEmpty(path))return;
            bool oldLabels=d.labels;var oldTexture=d.boardImage.texture;
            try{d.labels=labels;FeatureOverviewRenderer.Refresh(d,-1);File.Copy(AssetDatabase.GetAssetPath(d.boardImage.texture),path,true);}
            finally{d.labels=oldLabels;d.boardImage.texture=oldTexture;EditorSceneManager.SaveScene(d.gameObject.scene);}
        }
    }
    [CustomEditor(typeof(FeatureOverviewData))]
    public sealed class FeatureOverviewDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){if(GUILayout.Button("打开 A2C Demo 面板"))FeatureOverviewWindow.Open();DrawDefaultInspector();}
    }
}
