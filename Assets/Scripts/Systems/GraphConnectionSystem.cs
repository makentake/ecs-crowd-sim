using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.Systems;
using Unity.Mathematics;

public partial class GraphConnectionSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem end;
    private EntityQuery waypointQuery;
    private BuildPhysicsWorld physicsWorld;

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

                for (int i = 0; i < waypoints.Count(); i++)
                {
                    float3 from = t.Value;
                    float3 to = waypoints[i].Value;
                    bool haveHit;

                    if (math.distance(from, to) <= math.sqrt(math.pow(voxelData.voxelSpacing, 2) + math.pow(voxelData.voxelSpacing, 2)))
                    {
                        var input = new RaycastInput
                        {
                            Start = from,
                            End = to,
                            Filter = new CollisionFilter
                            {
                                BelongsTo = 1 << 0,
                                CollidesWith = 1 << 1
                            }
                        };

                        haveHit = collisionWorld.CastRay(input);

                        if (!haveHit && w.key != i)
                        {
                            connections.Add(new Connections
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
        waypointQuery = GetEntityQuery(ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<Translation>());
        var waypoints = new NativeParallelHashMap<int, Translation>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var parallelWriter = waypoints.AsParallelWriter();

        Entities
            .ForEach((in Waypoint w, in Translation t) =>
            {
                parallelWriter.TryAdd(w.key, t);
            }).ScheduleParallel();

        Entities.ForEach((in Translation t, in DynamicBuffer<Connections> b) =>
        {
            for (int i = 0; i < b.Length; i++)
            {
                Debug.DrawLine(t.Value, waypoints[b[i].key].Value, Color.green);
            }
        }).WithoutBurst().Run();

        waypoints.Dispose(Dependency);
    }
}
