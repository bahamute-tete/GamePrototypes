using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlaneContactChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    [MenuItem("Tools/LiangZhu/Plane Contact/Run Validation")]
    public static void Run()
    {
        var messages = new List<string>();
        foreach (var name in new[] { "Hidden/LiangZhu/ContactMask", "Custom/LiangZhu/ContactPlane" })
        {
            var shader = Shader.Find(name); Require(shader != null && shader.isSupported, "Shader unavailable: " + name);
            foreach (var m in ShaderUtil.GetShaderMessages(shader))
                Require(m.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, name + ": " + m.message);
        }
        messages.Add("PASS shader availability and compilation");
        Vector2 a, b;
        Require(DissolvePlaneContact.IntersectTriangle(new Vector3(-1,0,-1), new Vector3(1,0,1), new Vector3(0,1,1), out a, out b), "Crossing triangle missing");
        Require(!DissolvePlaneContact.IntersectTriangle(new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(0,1,1),out a,out b), "Separated triangle contacted");
        Require(DissolvePlaneContact.IntersectTriangle(new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,1),out a,out b), "On-plane edge missing");
        Require(!DissolvePlaneContact.IntersectTriangle(Vector3.zero,Vector3.right,Vector3.up,out a,out b), "Coplanar face must be excluded");
        messages.Add("PASS triangle crossing / separation / on-plane edge / coplanar case");
        var plane = new GameObject("Contact validation plane");
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh skinMesh = null;
        try
        {
            plane.hideFlags = cube.hideFlags = HideFlags.HideAndDontSave;
            var contact = plane.AddComponent<DissolvePlaneContact>();
            contact.followControllerPlane = false; contact.size = new Vector2(3,3); contact.resolution = 256;
            cube.transform.position = new Vector3(.35f,.55f,0);
            contact.contactRenderers.Add(cube.GetComponent<Renderer>());
            contact.RefreshContact();
            Require(contact.SegmentCount > 0, "Perpendicular cube walls must produce a contour");
            var center = MaskCentroid(contact.ContactTexture);
            Require(Mathf.Abs(center.x-(.5f+.35f/3)) < .025f && Mathf.Abs(center.y-(.5f+.55f/3)) < .025f, "Mask orientation / position mismatch: " + center);
            messages.Add("PASS GPU mask nonempty and correct X/Y orientation");
            cube.transform.position = new Vector3(.35f,.55f,3); contact.RefreshContact();
            Require(contact.SegmentCount == 0 && MaskCentroid(contact.ContactTexture).x < 0, "Contact mask not cleared after separation");
            messages.Add("PASS separating object clears the texture");
            cube.transform.position = new Vector3(.35f,.55f,0);
            var original = cube.GetComponent<MeshFilter>().sharedMesh;
            skinMesh = UnityEngine.Object.Instantiate(original);
            var weights = new BoneWeight[skinMesh.vertexCount];
            for(int i=0;i<weights.Length;i++) weights[i] = new BoneWeight { boneIndex0=0, weight0=1 };
            skinMesh.boneWeights = weights; skinMesh.bindposes = new[]{Matrix4x4.identity};
            var bone = new GameObject("Contact check bone"); bone.transform.SetParent(cube.transform,false);
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<MeshRenderer>());
            var skin = cube.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh=skinMesh; skin.bones=new[]{bone.transform}; skin.rootBone=bone.transform;
            skin.localBounds = new Bounds(Vector3.zero,Vector3.one*4);
            contact.contactRenderers.Clear(); contact.contactRenderers.Add(skin);
            contact.RefreshContact(); Require(contact.SegmentCount > 0,"Skinned contact missing");
            var before = MaskCentroid(contact.ContactTexture);
            bone.transform.localPosition = new Vector3(.25f,0,0); contact.RefreshContact();
            var after = MaskCentroid(contact.ContactTexture);
            Require(after.x-before.x > .05f,"Mask does not follow bone deformation");
            messages.Add("PASS bone pose updates the contact mask");
            var controller = cube.AddComponent<DissolveController>();
            controller.mode=DissolveController.DissolveMode.Direction; controller.space=DissolveController.DissolveSpace.World;
            controller.axisDirection=new Vector3(1,2,3); controller.planeOffset=.42f; controller.worldOrigin=bone.transform;
            contact.controller=controller; contact.followControllerPlane=true;
            Vector3 p,n,p2,n2; Require(contact.TryGetPlane(out p,out n),"World plane missing");
            Require(Vector3.Distance(p,bone.transform.position+controller.axisDirection.normalized*.42f)<1e-5f,"Plane offset mismatch");
            controller.directionReverse=true; contact.TryGetPlane(out p2,out n2);
            Require(Vector3.Distance(p,p2)<1e-5f,"Reverse moved the plane");
            controller.mode=DissolveController.DissolveMode.Noise;
            contact.RefreshContact(); Require(contact.SegmentCount==0 && MaskCentroid(contact.ContactTexture).x<0,"Invalid controller leaves stale mask");
            messages.Add("PASS plane origin / direction / offset / reverse / invalid-mode clearing");
            contact.enabled=false; Require(contact.ContactTexture==null,"Disable leaked texture");
            contact.enabled=true; contact.followControllerPlane=false; contact.RefreshContact(); Require(contact.ContactTexture!=null,"Enable failed to restore texture");
            messages.Add("PASS disable/re-enable resource lifecycle");
        }
        finally { UnityEngine.Object.DestroyImmediate(plane); UnityEngine.Object.DestroyImmediate(cube); if(skinMesh!=null) UnityEngine.Object.DestroyImmediate(skinMesh); }
        Directory.CreateDirectory("Temp/PlaneContact");
        File.WriteAllLines("Temp/PlaneContact/validation.txt", messages);
        Debug.Log("PlaneContact validation: " + string.Join("; ", messages));
    }
    public static Vector2 MaskCentroid(RenderTexture rt)
    {
        var old=RenderTexture.active; var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,true);
        try
        {
            RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
            var pixels=image.GetPixels32(); double x=0,y=0,sum=0;
            for(int j=0;j<rt.height;j++) for(int i=0;i<rt.width;i++) { float value=pixels[j*rt.width+i].r; sum+=value; x+=(i+.5)*value; y+=(j+.5)*value; }
            return sum>0 ? new Vector2((float)(x/sum/rt.width),(float)(y/sum/rt.height)) : new Vector2(-1,-1);
        }
        finally { RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(image); }
    }
}
