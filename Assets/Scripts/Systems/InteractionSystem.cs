using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(PreMovementEntityCommandBuffer))]
public partial class InteractionSystem : SystemBase
{
    /*private PreMovementEntityCommandBuffer pre;
    private EntityQuery agentQuery;

    protected override void OnStartRunning()
    {
        pre = World.GetOrCreateSystem<PreMovementEntityCommandBuffer>();

        Entities
            .ForEach((ref Police p) =>
            {
                p.interactionTarget = Entity.Null;
            }).ScheduleParallel();
    }

    */protected override void OnUpdate()
    {/*
        agentQuery = GetEntityQuery(typeof(Rioter), typeof(CivilianTag));

        NativeArray<Entity> rEntities = new NativeArray<Entity>(agentQuery.CalculateEntityCount(), Allocator.TempJob);
        NativeArray<float3> rTranslation = new NativeArray<float3>(agentQuery.CalculateEntityCount(), Allocator.TempJob);

        Entities
            .WithAll<CivilianTag>()
            .ForEach((Entity e, int entityInQueryIndex, in Translation t) =>
            {
                rioters[entityInQueryIndex] = e;
                rTranslation[entityInQueryIndex] = t.Value;
            }).Schedule();

        var ecb = pre.CreateCommandBuffer().AsParallelWriter();

        Entities
            .ForEach((int entityInQueryIndex, ref Police p, in Translation t) =>
            {
                if (p.interactionTarget == Entity.Null)
                {
                    for (int i = 0; i < rioters.Length; i++)
                    {
                        float dist = math.distance(t.Value, rTranslation[i]);

                        if (dist <= p.radius)
                        {
                            p.interactionTarget = rioters[i];
                            ecb.AddComponent<InteractingTag>();
                        }
                    }
                }
            }).ScheduleParallel();

        rioters.Dispose(Dependency);
        rTranslation.Dispose(Dependency);*/
    }
}
