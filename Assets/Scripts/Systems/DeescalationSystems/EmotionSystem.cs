using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;

// !!!SHOULD BE SIMPLIFIED, LOOK AT PedestrianMovementSystem FOR REFERENCE!!!

[UpdateBefore(typeof(PreMovementEntityCommandBuffer))]
public partial class EmotionSystem : SystemBase
{
    private PreMovementEntityCommandBuffer pre;
    private EntityQuery agentQuery, policeQuery, antifaQuery;

    // Debug stuff
    private EntityQuery tags;

    protected override void OnStartRunning()
    {
        pre = World.GetOrCreateSystem<PreMovementEntityCommandBuffer>();

        Entities
            .ForEach((ref Police p) =>
            {
                p.interactionTarget = Entity.Null;
            }).ScheduleParallel();
    }

    protected override void OnUpdate()
    {
        policeQuery = GetEntityQuery(typeof(Police));
        antifaQuery = GetEntityQuery(typeof(Antifa));
        agentQuery = GetEntityQuery(typeof(Rioter), typeof(CivilianTag));

        var dt = Time.DeltaTime;

        // Police-related NativeArrays
        NativeArray<Police> police = new NativeArray<Police>(policeQuery.CalculateEntityCount(), Allocator.TempJob);
        NativeArray<float3> pTranslation = new NativeArray<float3>(policeQuery.CalculateEntityCount(), Allocator.TempJob);

        // Civilian-related NativeArrays
        NativeArray<Entity> rEntities = new NativeArray<Entity>(agentQuery.CalculateEntityCount(), Allocator.TempJob);
        NativeArray<float> rioters = new NativeArray<float>(agentQuery.CalculateEntityCount(), Allocator.TempJob);
        NativeArray<float3> rTranslation = new NativeArray<float3>(agentQuery.CalculateEntityCount(), Allocator.TempJob);

        // Antifa-related NativeArrays
        NativeArray<Antifa> antifa = new NativeArray<Antifa>(antifaQuery.CalculateEntityCount(), Allocator.TempJob);
        NativeArray<float3> aTranslation = new NativeArray<float3>(antifaQuery.CalculateEntityCount(), Allocator.TempJob);

        // Get police-related data
        Entities
            .ForEach((int entityInQueryIndex, in Police p, in Translation t) =>
            {
                police[entityInQueryIndex] = p;
                pTranslation[entityInQueryIndex] = t.Value;
            }).Schedule();

        // Get rioter-related data
        Entities
            .WithAll<CivilianTag>()
            .ForEach((Entity e, int entityInQueryIndex, in Rioter r, in Translation t) =>
            {
                rEntities[entityInQueryIndex] = e;
                rioters[entityInQueryIndex] = r.aggression;
                rTranslation[entityInQueryIndex] = t.Value;
            }).Schedule();

        // Get Antifa-related data
        Entities
            .ForEach((int entityInQueryIndex, in Antifa a, in Translation t) =>
            {
                antifa[entityInQueryIndex] = a;
                aTranslation[entityInQueryIndex] = t.Value;
            }).Schedule();

        var ecb = pre.CreateCommandBuffer().AsParallelWriter();

        // Complete the officer's leg of interaction generation
        Entities
            .WithReadOnly(police)
            .WithReadOnly(rEntities)
            .WithReadOnly(rTranslation)
            .ForEach((int entityInQueryIndex, ref Police p, in Translation t) =>
            {
                if (p.interactionTarget == Entity.Null)
                {
                    for (int i = 0; i < rEntities.Length; i++)
                    {
                        Entity targetCandidate = rEntities[i];
                        bool available = true;

                        for (int j = 0; j < police.Length; j++)
                        {
                            if (police[j].interactionTarget == targetCandidate)
                            {
                                available = false;
                                break;
                            }
                        }

                        if (math.distance(t.Value, rTranslation[i]) <= p.radius && available)
                        {
                            p.interactionTarget = rEntities[i];
                        }
                    }
                }
            }).Schedule();

        // Complete the agent's leg of interaction generation
        Entities
            .WithReadOnly(police)
            //.WithReadOnly(pTranslation)
            //.WithReadOnly(rEntities)
            .WithAll<CivilianTag>()
            .WithNone<Interacting>() //problem
            .ForEach((Entity e, int entityInQueryIndex, in Rioter r, in Translation t) =>
            {
                for (int i = 0; i < police.Length; i++)
                {
                    if (police[i].interactionTarget == e && r.aggression >= 10)
                    {
                        Interacting newInteraction = new Interacting
                        {
                            startingAnger = r.aggression,
                            position = t
                        };

                        ecb.AddComponent(entityInQueryIndex, e, newInteraction);
                    }
                }
            }).ScheduleParallel();

        // Police-based interaction maintinence
        Entities
            .WithReadOnly(rEntities)
            .WithReadOnly(rioters)
            .WithReadOnly(rTranslation)
            .ForEach((ref Police p, in Translation t) =>
            {
                if (p.interactionTarget != Entity.Null)
                {
                    int index = rEntities.IndexOf(p.interactionTarget);

                    if (math.distance(t.Value, rTranslation[index]) >= p.radius || rioters[index] < 10)
                    {
                        p.interactionTarget = Entity.Null;
                    }
                }
            }).ScheduleParallel();

        // Antifa effects
        Entities
            .WithReadOnly(antifa)
            .WithReadOnly(aTranslation)
            .WithAll<CivilianTag>()
            .ForEach((ref Rioter r, in Translation t) =>
            {
                for (int i = 0; i < antifa.Length; i++)
                {
                    float dist = math.distance(t.Value, aTranslation[i]);
                    float instigation = antifa[i].instigation * dt;

                    if (dist <= antifa[i].radius && r.aggression <= 254 - instigation)
                    {
                        r.aggression += instigation;
                    }
                }
            }).ScheduleParallel();

        // Emotional effects of police interaction and agent-based interaction breakoff
        Entities
            .WithReadOnly(police)
            .WithAll<CivilianTag>()
            .ForEach((Entity e, int entityInQueryIndex, ref Rioter r, ref Agent a, in Translation t, in Interacting i) =>
            {
                bool found = false;

                for (int j = 0; j < police.Length; j++)
                {
                    if (police[j].interactionTarget == e)
                    {
                        ecb.AddComponent<MovingTag>(entityInQueryIndex, e);

                        a.tolerance = 0.1f;

                        r.aggression -= police[0].angerDefuse * dt;

                        if (r.aggression < 10)
                        {
                            ecb.RemoveComponent<Interacting>(entityInQueryIndex, e);
                        }

                        found = true;
                    }
                }

                if (!found)
                {
                    ecb.RemoveComponent<Interacting>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

        // Emotional contagion
        Entities
            .WithReadOnly(rioters)
            .WithReadOnly(rTranslation)
            .WithAll<CivilianTag>()
            .ForEach((ref Rioter r, in Agent a, in Translation t) =>
            {
                float totalAnger = 0;
                int influencers = 0;

                for (int i = 0; i < rioters.Length; i++)
                {
                    float rioter = rioters[i];
                    float3 rPosition = rTranslation[i];
                    float dist = math.distance(t.Value, rPosition);

                    if (!(r.aggression == rioter || dist < 0.01) && dist < a.maxDist)
                    {
                        totalAnger += rioter;
                        influencers++;
                    }
                }

                float average = totalAnger / influencers;

                if (average < r.aggression)
                {
                    r.aggression += 1 * dt;
                }
            }).ScheduleParallel();

        Entities
            .WithAll<CivilianTag>()
            .ForEach((ref URPMaterialPropertyBaseColor c, in Rioter r) =>
            {
                c.Value = math.float4(r.aggression, 255 - r.aggression, 0, 1);
            }).ScheduleParallel();

        // Dispose of police data
        police.Dispose(Dependency);
        pTranslation.Dispose(Dependency);

        // Dispose of rioter data
        rEntities.Dispose(Dependency);
        rioters.Dispose(Dependency);
        rTranslation.Dispose(Dependency);

        // Dispose of Antifa data
        antifa.Dispose(Dependency);
        aTranslation.Dispose(Dependency);

        pre.AddJobHandleForProducer(Dependency);
    }
}