using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(CrowdMovementSystem))]
public partial class SpawningSystem : SystemBase
{
    private EndFixedStepSimulationEntityCommandBufferSystem end;

    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();

        Entities.ForEach((int entityInQueryIndex, ref Spawner s) =>
        {
            s.random = Random.CreateFromIndex((uint)entityInQueryIndex);
        }).ScheduleParallel();
    }

    protected override void OnUpdate()
    {
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        Entities.ForEach((int entityInQueryIndex, ref Spawner s, ref Goal g, in Translation translation) =>
        {
            if (!s.done)
            {
                float3 minValue = translation.Value;
                float3 maxValue = s.spawnRadius + minValue;
                //g.goal = GetComponentDataFromEntity<Translation>(true)[s.goal];
                Translation goalPos = GetComponentDataFromEntity<Translation>(true)[s.goal];
                goalPos.Value += math.float3(0, 0, s.random.NextFloat(-30, 30));

                g.goal = goalPos;
                g.exit = GetComponentDataFromEntity<Translation>(true)[s.exit];

                for (int i = 0; i < s.crowdSize; i++)
                {
                    float3 spawnPos = s.random.NextFloat3(minValue, maxValue);
                    Translation pos = new Translation { Value = spawnPos };

                    Entity newAgent = ecb.Instantiate(entityInQueryIndex, s.agent);
                    ecb.SetComponent(entityInQueryIndex, newAgent, pos);
                    ecb.SetComponent(entityInQueryIndex, newAgent, g);
                }

                for (int i = 0; i < s.antifaSize; i++)
                {
                    float3 spawnPos = s.random.NextFloat3(minValue, maxValue);
                    Translation pos = new Translation { Value = spawnPos };

                    Entity newAgent = ecb.Instantiate(entityInQueryIndex, s.antifa);
                    ecb.SetComponent(entityInQueryIndex, newAgent, pos);
                    ecb.SetComponent(entityInQueryIndex, newAgent, g);
                }

                s.spawned += s.crowdSize + s.antifaSize;

                if (s.spawned >= 100)
                {
                    s.done = true;
                }
            }
        }).ScheduleParallel();

        end.AddJobHandleForProducer(Dependency);
    }
}
