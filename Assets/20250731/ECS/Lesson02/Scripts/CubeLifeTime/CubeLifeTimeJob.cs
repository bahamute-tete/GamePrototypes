using Unity.Entities;

public partial struct CubeLifeTimeJob : IJobEntity
{

    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;
    public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, ref CubeLifeTimeComponent cubeLifeTime)
    {
        cubeLifeTime.lifeTime -= deltaTime;
        if (cubeLifeTime.lifeTime <= 0f)
        {
            ecb.DestroyEntity(entityIndex, entity);
        }
    }
}
