using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;
using Unity.Collections;

[UpdateAfter(typeof(CrowdMovementSystem))]
[UpdateAfter(typeof(GraphConnectionSystem))]
public partial class SpawningSystem : SystemBase
{
    private EndFixedStepSimulationEntityCommandBufferSystem end;

    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();

        // Get the current time to use for random number generation
        uint osTime = (uint)System.DateTime.Now.Ticks;

        // Initialize all the random components
        Entities.ForEach((int entityInQueryIndex, ref RioterSpawner s) =>
        {
            s.random.InitState((osTime + (uint)entityInQueryIndex) | 0b1);
        }).ScheduleParallel();

        Entities.ForEach((int entityInQueryIndex, ref PedestrianSpawner s) =>
        {
            s.random.InitState((osTime + (uint)entityInQueryIndex) | 0b1);
        }).ScheduleParallel();

        Entities.ForEach((int entityInQueryIndex, ref WaypointPedestrianSpawner s) =>
        {
            s.random.InitState((osTime + (uint)entityInQueryIndex) | 0b1);
        }).ScheduleParallel();
    }

    protected override void OnUpdate()
    {
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        bool spawnTest = true;

        if (!spawnTest)
        {
            // Spawn agents
            Entities.ForEach((int entityInQueryIndex, ref RioterSpawner s, ref Goal g, in Translation translation) =>
            {
                if (!s.done)
                {
                    // Calculate the goal position
                    float3 minValue = translation.Value;
                    float3 maxValue = s.spawnRadius + minValue;
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
                    if (s.spawned >= s.toSpawn)
                    {
                        s.done = true;
                    }
                }
            }).ScheduleParallel();

            // Spawn agents
            Entities.ForEach((int entityInQueryIndex, ref PedestrianSpawner s, ref Goal g, in Translation translation) =>
            {
                if (!s.done)
                {
                    // Calculate the goal position
                    float3 minValue = translation.Value;
                    float3 maxValue = s.spawnRadius + minValue;
                    Translation goalPos = GetComponentDataFromEntity<Translation>(true)[s.goal];

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

                        if (s.random.NextFloat() <= 0.25)
                        {
                            Wait tag = new Wait
                            {
                                maxTime = 60,
                                elapsedTime = 0
                            };

                            ecb.AddComponent(entityInQueryIndex, newAgent, tag);
                        }

                        if (s.random.NextFloat() <= 0.25)
                        {
                            var tag = new YoungTag();

                            ecb.AddComponent(entityInQueryIndex, newAgent, tag);
                        }
                    }

                    s.spawned += s.crowdSize;

                    // Stop spawning if the target number of agents has been reached
                    if (s.spawned >= s.toSpawn)
                    {
                        s.done = true;
                    }
                }
            }).ScheduleParallel();
        }
        else
        {
            EntityQuery pedestrian = GetEntityQuery(ComponentType.ReadOnly<Goal>(), ComponentType.ReadOnly<Pedestrian>(), ComponentType.ReadOnly<Translation>());
            NativeArray<Goal> pedestrianArray = pedestrian.ToComponentDataArray<Goal>(Allocator.TempJob);

            var waypointFollower = GetEntityQuery(ComponentType.ReadOnly<WaypointFollower>(), ComponentType.ReadOnly<Pedestrian>(), ComponentType.ReadOnly<Translation>());
            var waypointFollowerArray = waypointFollower.ToComponentDataArray<WaypointFollower>(Allocator.TempJob);

            // Spawn agents
            Entities.WithReadOnly(pedestrianArray).ForEach((int entityInQueryIndex, ref PedestrianSpawner s, ref Goal g, in Translation translation) =>
            {
                s.spawned = 0;

                // Calculate the goal position
                float3 minValue = translation.Value;
                float3 maxValue = s.spawnRadius + minValue;
                Translation goalPos = GetComponentDataFromEntity<Translation>(true)[s.goal];

                g.goal = goalPos;
                g.exit = GetComponentDataFromEntity<Translation>(true)[s.exit];

                foreach (Goal agent in pedestrianArray)
                {
                    if (agent.exit.Value.x == g.exit.Value.x && agent.exit.Value.y == g.exit.Value.y && agent.exit.Value.z == g.exit.Value.z)
                    {
                        s.spawned++;
                    }
                }

                int spawnsNeeded = s.toSpawn - s.spawned;

                // Create crowd members
                for (int i = 0; i < spawnsNeeded; i++)
                {
                    float3 spawnPos = s.random.NextFloat3(minValue, maxValue);
                    Translation pos = new Translation { Value = spawnPos };

                    Entity newAgent = ecb.Instantiate(entityInQueryIndex, s.agent);
                    ecb.SetComponent(entityInQueryIndex, newAgent, pos);
                    ecb.SetComponent(entityInQueryIndex, newAgent, g);

                    if (s.random.NextFloat() <= 0.25)
                    {
                        Wait tag = new Wait
                        {
                            maxTime = 60,
                            elapsedTime = 0
                        };

                        ecb.AddComponent(entityInQueryIndex, newAgent, tag);
                    }

                    if (s.random.NextFloat() <= 0.25)
                    {
                        var tag = new YoungTag();

                        ecb.AddComponent(entityInQueryIndex, newAgent, tag);
                    }
                }
            }).ScheduleParallel();

            Entities.WithReadOnly(waypointFollowerArray).ForEach((int entityInQueryIndex, ref WaypointPedestrianSpawner s, in DynamicBuffer<GoalEntityList> p, in DynamicBuffer<RendezvousEntityList> r, in Translation t) =>
            {
                s.spawned = 0;
                int spawnsNeeded;

                // Calculate the goal position
                float3 minValue = t.Value;
                float3 maxValue = s.spawnRadius + minValue;
                int goalKey = GetComponentDataFromEntity<Waypoint>(true)[p[p.Length-1].waypoint].key;

                foreach (WaypointFollower agent in waypointFollowerArray)
                {
                    if (agent.goalKey == goalKey)
                    {
                        s.spawned++;
                    }
                }

                spawnsNeeded = s.toSpawn - s.spawned;

                /*if (spawnsNeeded == 0)
                {
                    s.done = true;
                }

                if (s.done)
                {
                    spawnsNeeded = 0;
                }*/

                // Create crowd members
                for (int i = 0; i < spawnsNeeded; i++)
                {
                    //  !!! DO NOT FORGET TO UPDATE THESE WHEN YOU ADD MORE CRAP TO COMPONENTS, OTHERWISE YOUR HEAD WILL HURT !!!
                    float3 spawnPos = s.random.NextFloat3(minValue, maxValue);
                    Translation pos = new Translation { Value = spawnPos };
                    WaypointFollower follower = new WaypointFollower 
                    { 
                        weight = 2,
                        goalKey = goalKey,
                        lastSavedMinimum = math.INFINITY
                    };
                    DynamicBuffer<GoalKeyList> givenWaypoints;
                    DynamicBuffer<RendezvousPosList> givenRendezvousPoints;

                    Entity newAgent = ecb.Instantiate(entityInQueryIndex, s.agent);
                    ecb.SetComponent(entityInQueryIndex, newAgent, pos);
                    ecb.SetComponent(entityInQueryIndex, newAgent, follower);

                    if (s.random.NextFloat() <= 0.1)
                    {
                        ecb.AddComponent(entityInQueryIndex, newAgent, new WillRendezvousTag());
                        givenRendezvousPoints = ecb.AddBuffer<RendezvousPosList>(entityInQueryIndex, newAgent);

                        for (int j = 0; j < r.Length; j++)
                        {
                            givenRendezvousPoints.Add(new RendezvousPosList
                            {
                                pos = GetComponentDataFromEntity<Translation>(true)[r[j].point].Value
                            });
                        }
                    }

                    if (s.random.NextFloat() <= 0.25)
                    {
                        ecb.AddComponent(entityInQueryIndex, newAgent, new YoungTag());
                    }

                    givenWaypoints = ecb.AddBuffer<GoalKeyList>(entityInQueryIndex, newAgent);

                    for (int j = 0; j < p.Length; j++)
                    {
                        givenWaypoints.Add(new GoalKeyList
                        {
                            key = GetComponentDataFromEntity<Waypoint>(true)[p[j].waypoint].key
                        });
                    }
                }
            }).ScheduleParallel();

            pedestrianArray.Dispose(Dependency);
            waypointFollowerArray.Dispose(Dependency);
        }

        end.AddJobHandleForProducer(Dependency);
    }
}
