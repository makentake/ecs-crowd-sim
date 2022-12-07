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

        // Get the current time to use for random number generation
        uint osTime = (uint)System.DateTime.Now.Ticks;

        // Initialize all the random components
        Entities.ForEach((int entityInQueryIndex, ref Spawner s) =>
        {
            s.random.InitState(osTime + (uint)entityInQueryIndex + 0b1);
        }).ScheduleParallel();
    }

    protected override void OnUpdate()
    {
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        // Spawn agents
        Entities.ForEach((int entityInQueryIndex, ref Spawner s, ref Goal g, in Translation translation) =>
        {
            if (!s.done)
            {
                // Calculate the goal position
                float3 minValue = translation.Value;
                float3 maxValue = s.spawnRadius + minValue;
                //g.goal = GetComponentDataFromEntity<Translation>(true)[s.goal];
                Translation goalPos = GetComponentDataFromEntity<Translation>(true)[s.goal];
                goalPos.Value += math.float3(0, 0, s.random.NextFloat(-30, 30));

                g.goal = goalPos;
                g.exit = GetComponentDataFromEntity<Translation>(true)[s.exit];

                // Create crowd members
                for (int i = 0; i < s.crowdSize; i++)
                {
                    float3 spawnPos = s.random.NextFloat3(minValue, maxValue);
                    Translation pos = new Translation { Value = spawnPos };

                    Entity newAgent = ecb.Instantiate(entityInQueryIndex, s.agent);
                    ecb.SetComponent(entityInQueryIndex, newAgent, pos);
                    ecb.SetComponent(entityInQueryIndex, newAgent, g);
                }

                // Create instigators
                for (int i = 0; i < s.antifaSize; i++)
                {
                    float3 spawnPos = s.random.NextFloat3(minValue, maxValue);
                    Translation pos = new Translation { Value = spawnPos };

                    Entity newAgent = ecb.Instantiate(entityInQueryIndex, s.antifa);
                    ecb.SetComponent(entityInQueryIndex, newAgent, pos);
                    ecb.SetComponent(entityInQueryIndex, newAgent, g);
                }

                s.spawned += s.crowdSize + s.antifaSize;

                // Stop spawning if the target number of agents has been reached
                if (s.spawned >= 100)
                {
                    s.done = true;
                }
            }
        }).ScheduleParallel();

        end.AddJobHandleForProducer(Dependency);
    }
}
