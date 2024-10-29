using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Mathematics;

//[UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
[UpdateBefore(typeof(GraphConnectionSystem))]
public partial class VoxelSpawningSystem : SystemBase
{
    private VoxelizationGenerationEntityCommandBuffer voxelization;
    private BuildPhysicsWorld physicsWorld;

    protected override void OnStartRunning()
    {
        voxelization = World.GetOrCreateSystem<VoxelizationGenerationEntityCommandBuffer>();
        var ecb = voxelization.CreateCommandBuffer().AsParallelWriter();
        physicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        Entities.ForEach((int entityInQueryIndex, in VoxelSpawner s, in Translation t) =>
        {
            var xCount = (int) math.floor(s.x / s.voxelSpacing);
            var yCount = (int) math.floor(s.y / s.voxelSpacing);

            for (int i = 0; i < xCount; i++)
            {
                for (int j = 0; j < yCount; j++)
                {
                    var newSpace = ecb.Instantiate(entityInQueryIndex, s.waypoint);

                    ecb.SetComponent(entityInQueryIndex, newSpace, new Translation
                    {
                        Value = math.float3(t.Value.x + (s.voxelSpacing*i), t.Value.y, t.Value.z + (s.voxelSpacing * j))
                    });
                }
            }
        }).ScheduleParallel();

        voxelization.AddJobHandleForProducer(Dependency);
    }

    protected override void OnUpdate()
    {
        
    }
}
