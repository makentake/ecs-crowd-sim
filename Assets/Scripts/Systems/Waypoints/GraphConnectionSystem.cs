using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Burst;

//[UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
//[UpdateBefore(typeof(PedestrianMovementSystem))]
public partial class GraphConnectionSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem end;
    //private EndVariableRateSimulationEntityCommandBufferSystem end;
    private EntityQuery waypointQuery;
    private BuildPhysicsWorld physicsWorld;

    public bool onDemand = false; // for all your on-demand grid recalculation needs

    private bool ready;
    private bool finished;

    [BurstCompile]
    private partial struct RecalculateConnectionsJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypoints;
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public VoxelSpawner voxelData;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, in Waypoint w, in Translation t)
        {
            var connections = ecbpw.AddBuffer<Connections>(entityInQueryIndex, e);
            var barricadeConnections = ecbpw.AddBuffer<BarricadeConnections>(entityInQueryIndex, e);

            for (int i = 0; i < waypoints.Count(); i++)
            {
                float3 from = t.Value;
                float3 to = waypoints[i].Value;
                float dist = math.distance(from, to);
                bool haveHit;

                if (dist <= math.sqrt(math.pow(voxelData.voxelSpacing, 2) + math.pow(voxelData.voxelSpacing, 2)))
                {
                    haveHit = collisionWorld.SphereCast(from, 0.5f, math.normalizesafe(to - from), dist, new CollisionFilter
                    {
                        BelongsTo = 1 << 0,
                        CollidesWith = 3 << 1
                    });

                    if (!haveHit && w.key != i)
                    {
                        connections.Add(new Connections
                        {
                            key = i
                        });
                    }

                    haveHit = collisionWorld.SphereCast(from, 0.5f, math.normalizesafe(to - from), dist, new CollisionFilter
                    {
                        BelongsTo = 1 << 0,
                        CollidesWith = 1 << 1
                    });

                    if (!haveHit && w.key != i)
                    {
                        barricadeConnections.Add(new BarricadeConnections
                        {
                            key = i
                        });
                    }
                }
            }
        }
    }

    protected override void OnStartRunning()
    {
        var voxelData = GetSingleton<VoxelSpawner>();

        waypointQuery = GetEntityQuery(ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<Translation>());
        var waypoints = new NativeParallelHashMap<int, Translation>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var parallelWriter = waypoints.AsParallelWriter();

        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        end = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        //end = World.GetOrCreateSystem<EndVariableRateSimulationEntityCommandBufferSystem>();
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        Entities
            .ForEach((int entityInQueryIndex, ref Waypoint w, in Translation t) =>
            {
                w.key = entityInQueryIndex;

                parallelWriter.TryAdd(entityInQueryIndex, t);
            }).ScheduleParallel();

        /*Entities
            .WithReadOnly(waypoints)
            .WithReadOnly(collisionWorld)
            .ForEach((Entity e, int entityInQueryIndex, in Waypoint w, in Translation t) =>
            {
                var connections = ecb.AddBuffer<Connections>(entityInQueryIndex, e);
                var barricadeConnections = ecb.AddBuffer<BarricadeConnections>(entityInQueryIndex, e);

                for (int i = 0; i < waypoints.Count(); i++)
                {
                    float3 from = t.Value;
                    float3 to = waypoints[i].Value;
                    float dist = math.distance(from, to);
                    bool haveHit;

                    if (dist <= math.sqrt(math.pow(voxelData.voxelSpacing, 2) + math.pow(voxelData.voxelSpacing, 2)))
                    {
                        haveHit = collisionWorld.SphereCast(from, 0.5f, math.normalizesafe(to-from), dist, new CollisionFilter
                        {
                            BelongsTo = 1 << 0,
                            CollidesWith = 3 << 1
                        });

                        if (!haveHit && w.key != i)
                        {
                            connections.Add(new Connections
                            {
                                key = i
                            });
                        }

                        haveHit = collisionWorld.SphereCast(from, 0.5f, math.normalizesafe(to - from), dist, new CollisionFilter
                        {
                            BelongsTo = 1 << 0,
                            CollidesWith = 1 << 1
                        });

                        if (!haveHit && w.key != i)
                        {
                            barricadeConnections.Add(new BarricadeConnections
                            {
                                key = i
                            });
                        }
                    }
                }
            }).ScheduleParallel();*/

        new RecalculateConnectionsJob
        {
            waypoints = waypoints,
            collisionWorld = collisionWorld,
            voxelData = voxelData,
            ecbpw = ecb
        }.ScheduleParallel();

        waypoints.Dispose(Dependency);
        end.AddJobHandleForProducer(Dependency);
    }

    protected override void OnUpdate()
    {
        var voxelData = GetSingleton<VoxelSpawner>();
        waypointQuery = GetEntityQuery(ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<Translation>());
        var waypoints = new NativeParallelHashMap<int, Translation>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var parallelWriter = waypoints.AsParallelWriter();
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
        var ecb = end.CreateCommandBuffer().AsParallelWriter();
        var needsConversion = GetEntityQuery(typeof(AwaitingConversionTag));

        Entities
            .ForEach((in Waypoint w, in Translation t) =>
            {
                parallelWriter.TryAdd(w.key, t);
            }).ScheduleParallel();

        if (needsConversion.CalculateEntityCount() > 0)
        {
            ready = true;
        }

        // I think this whole system interacts with the RendermeshCullingSystem?
        // It waits for the aformentioned system to do its thing then recalculates the paths
        if (onDemand || (!finished && ready && needsConversion.CalculateEntityCount() == 0))
        //if (needsConversion.CalculateEntityCount() == 0)
        {
            /*Entities
            .WithReadOnly(waypoints)
            .WithReadOnly(collisionWorld)
            .ForEach((Entity e, int entityInQueryIndex, in Waypoint w, in Translation t) =>
            {
                var connections = ecb.AddBuffer<Connections>(entityInQueryIndex, e);
                var barricadeConnections = ecb.AddBuffer<BarricadeConnections>(entityInQueryIndex, e);

                for (int i = 0; i < waypoints.Count(); i++)
                {
                    float3 from = t.Value;
                    float3 to = waypoints[i].Value;
                    float dist = math.distance(from, to);
                    bool haveHit;

                    if (dist <= math.sqrt(math.pow(voxelData.voxelSpacing, 2) + math.pow(voxelData.voxelSpacing, 2)))
                    {
                        haveHit = collisionWorld.SphereCast(from, 0.5f, math.normalizesafe(to - from), dist, new CollisionFilter
                        {
                            BelongsTo = 1 << 0,
                            CollidesWith = 3 << 1
                        });

                        if (!haveHit && w.key != i)
                        {
                            connections.Add(new Connections
                            {
                                key = i
                            });
                        }

                        haveHit = collisionWorld.SphereCast(from, 0.5f, math.normalizesafe(to - from), dist, new CollisionFilter
                        {
                            BelongsTo = 1 << 0,
                            CollidesWith = 1 << 1
                        });

                        if (!haveHit && w.key != i)
                        {
                            barricadeConnections.Add(new BarricadeConnections
                            {
                                key = i
                            });
                        }
                    }
                }
            }).ScheduleParallel();
            //}).WithoutBurst().Run();*/

            new RecalculateConnectionsJob
            {
                waypoints = waypoints,
                collisionWorld = collisionWorld,
                voxelData = voxelData,
                ecbpw = ecb
            }.ScheduleParallel();

            Entities.WithAll<WaypointFollower>().ForEach((Entity e, int entityInQueryIndex) =>
            {
                ecb.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
            }).ScheduleParallel();

            finished = true;
            onDemand = false;

            //Debug.Log("running");
        }

        //Debug.Log("Needs conversion: " + needsConversion.CalculateEntityCount());

        Entities.ForEach((in Translation t, in DynamicBuffer<BarricadeConnections> b) =>
        {
            for (int i = 0; i < b.Length; i++)
            {
                Debug.DrawLine(t.Value, waypoints[b[i].key].Value, Color.red);
            }
        }).WithoutBurst().Run();

        Entities.ForEach((in Translation t, in DynamicBuffer<Connections> b) =>
        {
            for (int i = 0; i < b.Length; i++)
            {
                Debug.DrawLine(t.Value, waypoints[b[i].key].Value, Color.green);
            }
        }).WithoutBurst().Run();

        waypoints.Dispose(Dependency);
        end.AddJobHandleForProducer(Dependency);

        Dependency.Complete();

    }
}
