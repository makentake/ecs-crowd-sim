using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Rendering;

//[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
public partial class RendermeshCullingSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem end;
    private EntityQuery needsConversion;

    // If there are any render meshes to start with, cull them
    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        needsConversion = GetEntityQuery(typeof(AwaitingConversionTag));

        var ecb = end.CreateCommandBuffer();

        Entities
            .WithAll<WallTag>()
            .ForEach((Entity e)  =>
            {
                var rM = EntityManager.GetSharedComponentData<RenderMesh>(e);
                rM.mesh = new Mesh();
                ecb.SetSharedComponent(e, rM);
            }).WithoutBurst().Run();

        end.AddJobHandleForProducer(Dependency);
    }

    // Run the update loop once if another system requests a render mesh cull
    protected override void OnUpdate()
    {
        if (needsConversion.CalculateEntityCount() != 0)
        {
            var ecb = end.CreateCommandBuffer();

            Entities
                .WithAll<AwaitingConversionTag>()
                .WithNone<MLAgentsWallTag>()
                .ForEach((Entity e) =>
                {
                    var rM = EntityManager.GetSharedComponentData<RenderMesh>(e);
                    rM.mesh = new Mesh();
                    ecb.SetSharedComponent(e, rM);

                    ecb.RemoveComponent<AwaitingConversionTag>(e);
                }).WithoutBurst().Run();

            Entities
                .WithAll<MLAgentsWallTag>()
                .ForEach((Entity e) =>
                {
                    ecb.RemoveComponent<AwaitingConversionTag>(e);
                }).Schedule();

            end.AddJobHandleForProducer(Dependency);
        }
    }
}
