using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct CubeAniamtionJob : IJobEntity
{

    public float time;
    public float amplitude;
    public float frequency;
    public float speed;

    public float3 followOffset;


    public void Execute(ref LocalTransform transform,in CubeComponentL2 cube)
    {


        //float height = math.sin(cube.radius * frequency + time * speed) * amplitude;

        //transform.Position = cube.basePosition + cube.normalDir * height;



        float pulse = math.sin(cube.radius * 3f - time * speed * 2f) * amplitude;

        float3 p = cube.basePosition * 0.8f;
        float fakeNoise = math.sin(p.x * 1.2f + time * 0.5f) *
                          math.cos(p.y * 1.5f - time * 0.3f) *
                          math.sin(p.z * 1.8f + time * 0.7f);
        float n = fakeNoise * 0.6f;
        // float n = noise.snoise(new float4(cube.basePosition * 0.8f, time * 0.5f)) * 0.6f;


        float angularSpeed = 20f / (0.5f + cube.radius);
        float3 orbited = math.mul(quaternion.AxisAngle(math.up(),time * angularSpeed * 0.1f), cube.basePosition);


        transform.Rotation = quaternion.AxisAngle(cube.normalDir, time * (1f + cube.radius * 0.1f));


        transform.Position = orbited + cube.normalDir * (pulse+n)+ followOffset;
        transform.Scale = 1f + pulse * 0.3f;

    }
}
