using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

[UpdateAfter(typeof(GraphConnectionSystem))]
public partial class NavigationSystem : SystemBase
{
    private BuildPhysicsWorld physicsWorld;
    private EndSimulationEntityCommandBufferSystem end;
    private EntityQuery waypointQuery;

    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        waypointQuery = GetEntityQuery(ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<Translation>());
        physicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
    }

    [WithAll(typeof(AwaitingNavigationTag))]
    private partial struct AStarNavigationJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;
        [ReadOnly] public NativeParallelHashMap<int, Entity> waypointEntityArray;
        [ReadOnly] public BufferFromEntity<Connections> waypointBuffers;
        public int waypointCount;

        [ReadOnly] public CollisionWorld collisionWorld;

        public EntityCommandBuffer.ParallelWriter ecbpw;

        private int StartFinder(Translation t)
        {
            int minDistKey = 0;
            float minDist = math.INFINITY;

            for (int i = 0; i < waypointArray.Count(); i++)
            {
                var dist = math.distance(t.Value, waypointArray[i].Value);
                var input = new RaycastInput
                {
                    Start = t.Value,
                    End = waypointArray[i].Value,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = 1 << 0,
                        CollidesWith = 3 << 1
                    }
                };

                if (dist <= minDist && !collisionWorld.CastRay(input))
                {
                    minDistKey = i;
                    minDist = dist;
                }
            }

            return minDistKey;
        }

        private int MinimumFinder(NativeList<int> frontier, NativeParallelHashMap<int, float2> aStarValues)
        {
            int minFKey = 0;
            float minF = math.INFINITY;

            for (int i = 0; i < frontier.Length; i++)
            {
                var targetKey = frontier[i];

                if (aStarValues[targetKey].y <= minF)
                {
                    minFKey = targetKey;
                    minF = aStarValues[targetKey].y;
                }
            }

            return minFKey;
        }

        private void RemoveGivenKey(ref NativeList<int> list, int thing)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == thing)
                {
                    list.RemoveAtSwapBack(i);
                }
            }
        }

        private void ConstructPath(Entity e, int entityInQueryIndex, NativeParallelHashMap<int, int> parents, int goal, int start)
        {
            DynamicBuffer<WaypointList> buffer;
            var path = new NativeList<WaypointList>(Allocator.Temp);
            bool endReached = false;
            int current = goal;

            while (!endReached)
            {
                path.Add(new WaypointList
                {
                    key = current
                });

                if (current == start)
                {
                    endReached = true;
                }
                else
                {
                    current = parents[current];
                }
            }

            buffer = ecbpw.AddBuffer<WaypointList>(entityInQueryIndex, e);

            for (int i = path.Length - 1; i >= 0; i--)
            {
                buffer.Add(path[i]);
            }
        }

        // Pseudocode kindly provided by ChatGPT, implemented by me
        // ChatGPT lied to me. This is now been modified according to actual A* pseudocode
        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, in WaypointFollower f, in Translation t)
        {
            // Each float2 will contain the following information about the waypoint: g, f. The key is the index
            var aStarValues = new NativeParallelHashMap<int, float2>(waypointCount, Allocator.Temp);
            var parents = new NativeParallelHashMap<int, int>(waypointCount, Allocator.Temp);
            var frontier = new NativeList<int>(Allocator.Temp);

            var start = StartFinder(t);
            int current;
            var goal = f.goalKey;

            // Initialize the A* values HashMap
            foreach (int key in waypointArray.GetKeyArray(Allocator.Temp))
            {
                aStarValues.TryAdd(key, math.float2(math.INFINITY, math.INFINITY));
            }

            aStarValues[start] = math.float2(0, math.distance(waypointArray[start].Value, waypointArray[goal].Value));
            
            // Add the starting node to the frontier
            frontier.Add(start);

            while (frontier.Length != 0)
            {
                current = MinimumFinder(frontier, aStarValues);

                if (current == goal)
                {
                    ConstructPath(e, entityInQueryIndex, parents, current, start);
                }

                RemoveGivenKey(ref frontier, current);

                foreach (Connections connection in waypointBuffers[waypointEntityArray[current]])
                {
                    int neighbour = connection.key;
                    float tentativeG;
                    float2 newValues;
                    
                    tentativeG = aStarValues[current][0] + math.distance(waypointArray[current].Value, waypointArray[neighbour].Value);

                    if (tentativeG < aStarValues[neighbour][0])
                    {
                        newValues = math.float2(tentativeG, tentativeG + math.distance(waypointArray[neighbour].Value, waypointArray[goal].Value));

                        aStarValues[neighbour] = newValues;

                        if (parents.ContainsKey(neighbour))
                        {
                            parents[neighbour] = current;
                        }
                        else
                        {
                            parents.Add(neighbour, current);
                        }

                        if (!frontier.Contains(neighbour))
                        {
                            frontier.Add(neighbour);
                        }
                    }
                }
            }

            ecbpw.RemoveComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
        }
    }

    protected override void OnUpdate()
    {
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
        var waypoints = new NativeParallelHashMap<int, Translation>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var waypointEntities = new NativeParallelHashMap<int, Entity>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var waypointsParallelWriter = waypoints.AsParallelWriter();
        var waypointEntitiesParallelWriter = waypointEntities.AsParallelWriter();

        BufferFromEntity<Connections> lookUp = GetBufferFromEntity<Connections>();

        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        Entities
            .ForEach((Entity e, in Waypoint w, in Translation t) =>
            {
                waypointsParallelWriter.TryAdd(w.key, t);
                waypointEntitiesParallelWriter.TryAdd(w.key, e);
            }).ScheduleParallel();

        JobHandle navigationJob = new AStarNavigationJob
        {
            waypointArray = waypoints,
            waypointEntityArray = waypointEntities,
            waypointBuffers = lookUp,
            waypointCount = waypointQuery.CalculateEntityCount(),
            collisionWorld = collisionWorld,
            ecbpw = ecb
        }.ScheduleParallel();

        waypoints.Dispose(Dependency);
        waypointEntities.Dispose(Dependency);

        end.AddJobHandleForProducer(Dependency);
    }
}
