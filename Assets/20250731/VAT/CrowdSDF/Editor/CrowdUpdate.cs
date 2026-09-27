using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.VFX.Block
{
    [VFXInfo(category = "VAT Crowd")]
    class CrowdUpdate : VFXBlock
    {
        public override string name => "Follow Path + Avoid SDF + Slide + Animate";
        public override VFXContextType compatibleContexts => VFXContextType.Update;
        public override VFXDataType compatibleData => VFXDataType.Particle;
        public override IEnumerable<string> includes { get { yield return "Assets/20250731/VAT/CrowdSDF/Shaders/CrowdSDF.hlsl"; } }
        public class InputProperties : CrowdInitialize.InputProperties
        {
            public float WalkSpeed = 2;
            public float LookAhead = 2;
            public float AvoidStrength = 1.3f;
            public float TurnRate = 6;
            public float FPS = 30;
            public float PlaybackSpeed = 0.3f;
            public float ReferenceSpeed = 2;
        }
        public override IEnumerable<VFXNamedExpression> parameters
        {
            get
            {
                foreach(var p in base.parameters) yield return p;
                yield return new VFXNamedExpression(VFXBuiltInExpression.DeltaTime,"deltaTime");
            }
        }
        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                yield return new VFXAttributeInfo(VFXAttribute.ParticleId,VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Position,VFXAttributeMode.ReadWrite);
                yield return new VFXAttributeInfo(VFXAttribute.Velocity,VFXAttributeMode.ReadWrite);
                yield return new VFXAttributeInfo(VFXAttribute.AngleY,VFXAttributeMode.ReadWrite);
                foreach(var n in new[]{"crowdPathT","crowdFrame","crowdSide","crowdLaps"})
                    yield return new VFXAttributeInfo(CrowdInitialize.Custom(n),VFXAttributeMode.ReadWrite);
            }
        }
        public override string source => @"
float phase = crowdFrame-1;
CrowdStep(DistanceField,FieldCenter,FieldSize,P0,P1,P2,P3,
    WalkSpeed,Radius,LookAhead,AvoidStrength,TurnRate,BodyHeight,GroundY,
    FrameCount,FPS,PlaybackSpeed,ReferenceSpeed,deltaTime,particleId,
    position,velocity,crowdPathT,phase,angleY,crowdSide,crowdLaps);
crowdFrame = phase+1;
";
    }
}
