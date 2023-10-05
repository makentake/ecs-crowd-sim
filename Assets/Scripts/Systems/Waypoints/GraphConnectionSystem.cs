using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Mapbox.Examples.Voxels;

public partial class GraphConnectionSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem end;
    private EntityQuery waypointQuery;
    private BuildPhysicsWorld physicsWorld;
    private bool ready;
    private bool finished;

    protected override void OnStartRunning()
    {
        var voxelData = GetSingleton<VoxelSpawner>();

        waypointQuery = GetEntityQuery(ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<Translation>());
        var waypoints = new NativeParallelHashMap<int, Translation>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var parallelWriter = waypoints.AsParallelWriter();

        physicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        end = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        ready = false;
        finished = false;

        Entities
            .ForEach((int entityInQueryIndex, ref Waypoint w, in Translation t) =>
            {
                w.key = entityInQueryIndex;

                parallelWriter.TryAdd(entityInQueryIndex, t);
            }).ScheduleParallel();

        Entities
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
            }).ScheduleParallel();

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

        if (!finished && ready && needsConversion.CalculateEntityCount() == 0)
        {
            Entities
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

            Entities.WithAll<WaypointFollower>().ForEach((Entity e, int entityInQueryIndex) =>
            {
                ecb.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
            }).ScheduleParallel();

            finished = true;
        }

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
    }
}
