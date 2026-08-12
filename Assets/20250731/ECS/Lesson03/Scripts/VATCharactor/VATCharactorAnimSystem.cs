using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(EntitiesGraphicsSystem))]
partial struct VATCharactorAnimSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        var query = SystemAPI.QueryBuilder().WithAll<VATCharactorComponent, NeedRandomOffsetTag>().Build();

        state.RequireForUpdate(query);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var vatCharactorParameters = SystemAPI.QueryBuilder()
            .WithAll<VATCharactorComponent, NeedRandomOffsetTag>()
            .Build();

        state.Dependency = new VATCharactorAnimJob
        {
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
        }.ScheduleParallel(vatCharactorParameters, state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}


[BurstCompile]
public partial struct VATCharactorAnimJob : IJobEntity
{
    public float ElapsedTime;

    public void Execute(
        Entity entity,
        ref VATCharactorComponent vatCharactorComponent,
        EnabledRefRW<NeedRandomOffsetTag> needRandomOffset)
    {
        var random = Random.CreateFromIndex((uint)entity.Index);
        var animationTimeOffset = random.NextFloat(0f, 10f);

        vatCharactorComponent.GameTimeStartFrame = ElapsedTime - animationTimeOffset;
        needRandomOffset.ValueRW = false;
    }
}
