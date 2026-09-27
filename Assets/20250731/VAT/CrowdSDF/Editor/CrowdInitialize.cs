using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEditor.VFX.Block
{
    [VFXInfo(category = "VAT Crowd")]
    class CrowdInitialize : VFXBlock
    {
        public override string name => "Initialize VAT Crowd (SDF)";
        public override VFXContextType compatibleContexts => VFXContextType.Init;
        public override VFXDataType compatibleData => VFXDataType.Particle;
        public override IEnumerable<string> includes { get { yield return "Assets/20250731/VAT/CrowdSDF/Shaders/CrowdSDF.hlsl"; } }
        public class InputProperties
        {
            public Texture3D DistanceField;
            public Vector3 FieldCenter;
            public Vector3 FieldSize = new Vector3(32,8,56);
            public Vector3 P0, P1, P2, P3;
            public float GroundY;
            public float BodyHeight = 0.9f;
            public float Radius = 0.4f;
            public float FrameCount = 10;
        }
        internal static VFXAttribute Custom(string n) => new VFXAttribute(n,VFXValueType.Float);
        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                yield return new VFXAttributeInfo(VFXAttribute.ParticleId,VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Position,VFXAttributeMode.Write);
                yield return new VFXAttributeInfo(VFXAttribute.Velocity,VFXAttributeMode.Write);
                yield return new VFXAttributeInfo(VFXAttribute.AngleY,VFXAttributeMode.Write);
                yield return new VFXAttributeInfo(VFXAttribute.Lifetime,VFXAttributeMode.Write);
                yield return new VFXAttributeInfo(VFXAttribute.Size,VFXAttributeMode.Write);
                foreach(var n in new[]{"crowdPathT","crowdFrame","crowdSide","crowdLaps"})
                    yield return new VFXAttributeInfo(Custom(n),VFXAttributeMode.Write);
            }
        }
        public override string source => @"
crowdPathT = CrowdHash(particleId + 29u) * 0.9;
position = CrowdBezier(crowdPathT,P0,P1,P2,P3);
position.y = GroundY;
for(int attempt=0;attempt<64;attempt++) {
    if(CrowdDistance(DistanceField,position+float3(0,BodyHeight,0),FieldCenter,FieldSize)>Radius+0.2) break;
    crowdPathT=max(0,crowdPathT-0.015);
    position=CrowdBezier(crowdPathT,P0,P1,P2,P3);position.y=GroundY;
}
velocity = 0;
float3 tangent=CrowdBezier(min(1,crowdPathT+0.01),P0,P1,P2,P3)-position;
angleY = atan2(tangent.x,tangent.z);
crowdFrame = 1 + CrowdHash(particleId+97u)*max(FrameCount,1);
crowdSide = CrowdHash(particleId+193u)>0.5?1:-1;
crowdLaps = 0;
lifetime = 1000000000;
size = 1;
";
    }
}
