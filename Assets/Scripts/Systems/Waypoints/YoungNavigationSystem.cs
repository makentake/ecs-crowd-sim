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
using System.Linq;
using Unity.Entities.UniversalDelegates;
using Unity.Burst;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public partial class NavigationSystem : SystemBase
{
    [BurstCompile]
    [WithAll(typeof(AwaitingNavigationTag))]
    [WithAll(typeof(YoungTag))]
    private partial struct YoungAStarNavigationJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;
        [ReadOnly] public NativeParallelHashMap<int, Entity> waypointEntityArray;
        [ReadOnly] public BufferFromEntity<BarricadeConnections> waypointBuffers;
        public int waypointCount;

        [ReadOnly] public CollisionWorld collisionWorld;

        public EntityCommandBuffer.ParallelWriter ecbpw;

        // Density stuff
        [ReadOnly] public NativeParallelHashMap<int, WaypointDensity> waypointDensityArray;

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
        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref AwaitingNavigationTag a, ref WaypointFollower f, in Pedestrian p, in Translation t, in DynamicBuffer<GoalKeyList> g)
        {
            // Each float2 will contain the following information about the waypoint: g, f. The key is the index
            var aStarValues = new NativeParallelHashMap<int, float2>(waypointCount, Allocator.Temp);
            var parents = new NativeParallelHashMap<int, int>(waypointCount, Allocator.Temp);
            var frontier = new NativeList<int>(Allocator.Temp);

            var start = f.startKey;
            int current;
            var goal = g[0].key;

            f.lastSavedMinimum = math.INFINITY;

            // Initialize the A* values HashMap
            foreach (int key in waypointArray.GetKeyArray(Allocator.Temp))
            {
                aStarValues.TryAdd(key, math.float2(math.INFINITY, math.INFINITY));
            }

            aStarValues[start] = math.float2(0, f.weight * math.distance(waypointArray[start].Value, waypointArray[goal].Value));

            // Add the starting node to the frontier
            frontier.Add(start);

            while (frontier.Length != 0)
            {
                current = MinimumFinder(frontier, aStarValues);

                if (current == goal)
                {
                    ConstructPath(e, entityInQueryIndex, parents, current, start);
                    f.lastSavedMinimum = aStarValues[current][0];
                    break;
                }

                RemoveGivenKey(ref frontier, current);

                foreach (BarricadeConnections connection in waypointBuffers[waypointEntityArray[current]])
                {
                    int neighbour = connection.key;
                    float tentativeG;
                    float2 newValues;

                    tentativeG = aStarValues[current][0] + math.distance(waypointArray[current].Value, waypointArray[neighbour].Value)//;
                                                                                                                                      //+ (math.pow(waypointDensityArray[neighbour].currentAgents/ waypointDensityArray[neighbour].maxAgents, 2)*2* waypointDensityArray[neighbour].currentAgents);
                        + waypointDensityArray[neighbour].currentAgents;

                    if (tentativeG < aStarValues[neighbour][0])
                    {
                        newValues = math.float2(tentativeG, tentativeG + (f.weight*math.distance(waypointArray[neighbour].Value, waypointArray[goal].Value)));

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

            //ecbpw.RemoveComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
            a.hasNavigated = true;
        }
    }

    [BurstCompile]
    [WithAll(typeof(AwaitingNavigationTag))]
    [WithAll(typeof(YoungTag))]
    private partial struct YoungAStarClosestPointToRendezvous : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;
        [ReadOnly] public NativeParallelHashMap<int, Entity> waypointEntityArray;
        [ReadOnly] public BufferFromEntity<BarricadeConnections> waypointBuffers;
        public int waypointCount;

        [ReadOnly] public CollisionWorld collisionWorld;

        public EntityCommandBuffer.ParallelWriter ecbpw;

        // debug
        /*public NativeParallelHashMap<int, float2>.ParallelWriter externalValues;
        [WriteOnly] public int closestKey;*/

        private int MinimumFinder(NativeParallelHashMap<int, float2> aStarValues, float maxRange, float weight)
        {
            var currentList = new NativeList<int>(Allocator.Temp);
            var previousList = new NativeList<int>(Allocator.Temp);

            int minFKey = 0;
            float minF = math.INFINITY;
            var keyList = aStarValues.GetKeyArray(Allocator.Temp);
            var increments = 10;
            var increment = (weight * maxRange) / increments;

            // Progressively tighten the circle until there are no waypoints within an acceptable range
            for (int i = 0; i < keyList.Length; i++)
            {
                var targetKey = keyList[i];

                if ((aStarValues[targetKey].y - aStarValues[targetKey].x) <= weight * maxRange)
                {
                    currentList.Add(targetKey);
                }
            }

            /*Debug.Log($"Starting list length: {currentList.Length}");
            Debug.Log($"Increment: {increment}");*/

            for (int i = 1; i <= increments; i++)
            {
                previousList.Clear();

                for (int j = 0; j < currentList.Length; j++)
                {
                    previousList.Add(currentList[j]);
                }

                currentList.Clear();

                for (int j = 0; j < previousList.Length; j++)
                {
                    //Debug.Log("Running");

                    var targetKey = previousList[j];

                    if ((aStarValues[targetKey].y - aStarValues[targetKey].x) <= (weight * maxRange) - (increment*i))
                    {
                        //Debug.Log("Adding");
                        currentList.Add(targetKey);
                    }
                }

                //Debug.Log($"The list length is {currentList.Length} after {i} iterations");

                if (currentList.Length == 0) 
                {
                    break;
                }
            }

            //Debug.Log($"Start of final list:");

            for (int i = 0; i < previousList.Length; i++)
            {
                var targetKey = previousList[i];

                //Debug.Log(aStarValues[targetKey].y);

                if (aStarValues[targetKey].y <= minF && (aStarValues[targetKey].y - aStarValues[targetKey].x) <= weight * maxRange)
                {
                    minFKey = targetKey;
                    minF = aStarValues[targetKey].y;
                }
            }

            //Debug.Log($"End of final list.");

            //Debug.Log($"Final position: {waypointArray[minFKey].Value}");

            return minFKey;
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
        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref WaypointFollower f, ref DynamicBuffer<GoalKeyList> g, ref DynamicBuffer<RendezvousPosList> r, in Pedestrian p, in Translation t)
        {
            // Each float2 will contain the following information about the waypoint: g, f. The key is the index
            var aStarValues = new NativeParallelHashMap<int, float2>(waypointCount, Allocator.Temp);
            var parents = new NativeParallelHashMap<int, int>(waypointCount, Allocator.Temp);
            var frontier = new NativeList<int>(Allocator.Temp);

            if (r.Length != 0)
            {
                var start = f.startKey;
                int current;
                var goal = r[0].pos;
                int closest;

                // Initialize the A* values HashMap
                foreach (int key in waypointArray.GetKeyArray(Allocator.Temp))
                {
                    aStarValues.TryAdd(key, math.float2(math.INFINITY, math.INFINITY));
                }

                aStarValues[start] = math.float2(0, f.weight * math.distance(waypointArray[start].Value, goal));

                // Add the starting node to the frontier
                frontier.Add(start);

                while (frontier.Length != 0)
                {
                    current = MinimumFinder(frontier, aStarValues);

                    RemoveGivenKey(ref frontier, current);

                    foreach (BarricadeConnections connection in waypointBuffers[waypointEntityArray[current]])
                    {
                        int neighbour = connection.key;
                        float tentativeG;
                        float2 newValues;

                        tentativeG = aStarValues[current][0] + math.distance(waypointArray[current].Value, waypointArray[neighbour].Value);

                        if (tentativeG < aStarValues[neighbour][0])
                        {
                            newValues = math.float2(tentativeG, tentativeG + (f.weight * math.distance(waypointArray[neighbour].Value, goal)));

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

                closest = MinimumFinder(aStarValues, p.tolerance, f.weight);

                //closestKey = closest;

                /*Debug.Log($"Closest: {closest}, closest F: {aStarValues[closest].y}, closest G: {aStarValues[closest].x}, closest position: {waypointArray[closest].Value}, lastSavedMinimum: {f.lastSavedMinimum}");
                Debug.Log($"Start: {start}, start position: {waypointArray[start].Value}, raw distance {math.distance(waypointArray[start].Value, waypointArray[closest].Value)}");*/

                if (f.lastSavedMinimum > aStarValues[closest].y)
                {
                    g.Insert(0, new GoalKeyList
                    {
                        key = closest
                    });

                    r.RemoveAt(0);

                    ecbpw.AddComponent(entityInQueryIndex, e, new Wait
                    {
                        maxTime = 60,
                        elapsedTime = 0
                    });

                    ConstructPath(e, entityInQueryIndex, parents, closest, start);
                }

                /*var keys = aStarValues.GetKeyArray(Allocator.Temp);

                foreach (int key in keys)
                {
                    externalValues.TryAdd(key, aStarValues[key]);
                }*/
            }

            ecbpw.RemoveComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
        }
    }
}
