using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEditor.VFX.Block
{
    // VFX 14 node APIs are internal; the adjacent asmref follows this project's
    // existing CrowdSDF extension pattern. No package source is modified.
    [VFXInfo(category = "Boids")]
    class BoidsApplyState : VFXBlock
    {
        public override string name => "Boids - Apply GPU State";
        public override VFXContextType compatibleContexts => VFXContextType.Init | VFXContextType.Update;
        public override VFXDataType compatibleData => VFXDataType.Particle;

        public class InputProperties
        {
            [Tooltip("Bind the exposed BoidsState property from BoidsVFXSimulation.")]
            public GraphicsBuffer State;
        }

        public override IEnumerable<VFXNamedExpression> parameters
        {
            get
            {
                var inputs = base.parameters.ToDictionary(p => p.name, p => p.exp);
                var buffer = inputs["State"];
                var count = new VFXExpressionBufferCount(buffer);
                var stride = new VFXExpressionBufferStride(buffer);
                var index = new VFXAttributeExpression(VFXAttribute.ParticleId) * VFXValue.Constant(2u);
                yield return new VFXNamedExpression(new VFXExpressionSampleBuffer(
                    typeof(Vector4), VFXValueType.Float4, "", buffer, index, stride, count), "boidPosition");
                yield return new VFXNamedExpression(new VFXExpressionSampleBuffer(
                    typeof(Vector4), VFXValueType.Float4, "", buffer, index + VFXValue.Constant(1u), stride, count), "boidVelocity");
            }
        }

        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                yield return new VFXAttributeInfo(VFXAttribute.ParticleId, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Position, VFXAttributeMode.Write);
                yield return new VFXAttributeInfo(VFXAttribute.Velocity, VFXAttributeMode.Write);
                yield return new VFXAttributeInfo(VFXAttribute.AngleY, VFXAttributeMode.Write);
            }
        }

        public override string source => @"
position = boidPosition.xyz;
velocity = boidVelocity.xyz;
angleY = boidPosition.w;
";
    }
}
