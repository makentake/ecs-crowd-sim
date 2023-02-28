using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Rendering;

public partial class RendermeshCullingSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem end;

    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

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

        Enabled = false;
    }

    protected override void OnUpdate()
    {
        
    }
}
