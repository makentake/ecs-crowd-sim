using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Jobs;
using System.Collections.Generic;
using RaycastHit = Unity.Physics.RaycastHit;
using Unity.Burst;

[UpdateAfter(typeof(CrowdMovementSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class PedestrianMovementSystem : SystemBase
{
    private static bool WaypointVisibilityCheck(int k, NativeParallelHashMap<int, Translation> waypointArray, CollisionWorld collisionWorld, Translation t)
    {
        return collisionWorld.SphereCast(t.Value, 0.5f, math.normalizesafe(waypointArray[k].Value - t.Value), math.distance(t.Value, waypointArray[k].Value), new CollisionFilter
        {
            BelongsTo = 1 << 0,
            CollidesWith = 1 << 1
        });
    }

    [BurstCompile]
    [WithAll(typeof(WaypointFollower))]
    [WithNone(typeof(YoungTag))]
    private partial struct WaypointObstacleAvoidanceJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref Pedestrian p, in Translation t, in Rotation r, in DynamicBuffer<WaypointList> w)
        {
            var numberOfRays = 6;
            var angle = 360 / numberOfRays;
            var obstacleHits = 0;

            p.obstacle = math.float3(0, 0, 0);

            for (int i = 0; i < numberOfRays; i++)
            {
                var input = new RaycastInput
                {
                    Start = t.Value,
                    End = t.Value + (math.forward(quaternion.RotateY(math.radians(angle * i))) * p.wallTolerance),
                    Filter = new CollisionFilter
                    {
                        BelongsTo = 1 << 0,
                        CollidesWith = 3 << 1
                    }
                };

                if (collisionWorld.CastRay(input))
                {
                    p.obstacle += t.Value - input.End;
                    obstacleHits++;
                }
            }

            if (obstacleHits > 0)
            {
                p.obstacle /= obstacleHits;
            }

            if (WaypointVisibilityCheck(w[0].key, waypointArray, collisionWorld, t))
            {
                ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
            }
        }
    }

    [BurstCompile]
    [WithAll(typeof(WaypointFollower))]
    [WithAll(typeof(YoungTag))]
    private partial struct YoungWaypointObstacleAvoidanceJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        bool hasWallBelow(Translation t)
        {
            bool hasHit;
            float3 from = t.Value, to = t.Value + new float3(0, -5, 0);

            RaycastInput input = new RaycastInput()
            {
                Start = from,
                End = to,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1 << 0,
                    CollidesWith = 1 << 2,
                }
            };

            Unity.Physics.RaycastHit hit;
            hasHit = collisionWorld.CastRay(input, out hit);

            return hasHit;
        }

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref Pedestrian p, ref Translation t, in Rotation r, in DynamicBuffer<WaypointList> w)
        {
            var numberOfRays = 6;
            var angle = 360 / numberOfRays;
            var obstacleHits = 0;

            p.obstacle = math.float3(0, 0, 0);

            for (int i = 0; i < numberOfRays; i++)
            {
                var obstacleInput = new RaycastInput
                {
                    Start = t.Value,
                    End = t.Value + (math.forward(quaternion.RotateY(math.radians(angle * i))) * p.wallTolerance),
                    Filter = new CollisionFilter
                    {
                        BelongsTo = 1 << 0,
                        CollidesWith = 1 << 1
                    }
                };

                if (collisionWorld.CastRay(obstacleInput))
                {
                    p.obstacle += t.Value - obstacleInput.End;
                    obstacleHits++;
                }
            }

            if (obstacleHits > 0)
            {
                p.obstacle /= obstacleHits;
            }

            if (WaypointVisibilityCheck(w[0].key, waypointArray, collisionWorld, t))
            {
                ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
            }

            float3 from = t.Value, to = t.Value + math.forward(r.Value);

            RaycastInput input = new RaycastInput()
            {
                Start = from,
                End = to,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1 << 0,
                    CollidesWith = 1 << 2,
                }
            };

            if (collisionWorld.CastRay(input))
            {
                p.isClimbing = true;
                t.Value += math.forward(r.Value) * 0.6f;
            }
            else if (!hasWallBelow(t))
            {
                p.isClimbing = false;
            }
        }
    }

    [BurstCompile]
    private partial struct WaypointLocalAgentCalculationJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Translation> pedestrianArray;
        [ReadOnly] public NativeArray<Rotation> pedestrianRotArray;
        [ReadOnly] public NativeArray<float> pedestrianSpeedArray;

        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;

        public EntityCommandBuffer.ParallelWriter ecbpw;
        public float deltaTime;

        private void CoreVectorCalculationJob(ref Pedestrian p, in Translation t, in Rotation r, in DynamicBuffer<WaypointList> w, NativeList<Translation> lP, NativeList<Translation> hLP, NativeList<Rotation> lPR, NativeList<float> lPS, NativeList<float> lD, NativeList<float> hLD)
        {
            p.attraction = new float3(0, 0, 0);
            p.repulsion = new float3(0, 0, 0);

            p.attractors = 0;
            p.repellors = 0;

            p.target = waypointArray[w[0].key].Value - t.Value;

            for (int i = 0; i < hLP.Length; i++)
            {
                /*var originalVector = t.Value - hLP[i].Value;
                var originalDist = math.distance(t.Value, hLP[i].Value);

                p.repulsion += (math.normalizesafe(math.forward(quaternion.LookRotation(math.normalizesafe(originalVector), math.up())) + math.forward(quaternion.RotateY(210))) * originalDist) / hLD[i];*/
                p.repulsion += (t.Value - hLP[i].Value) / hLD[i];
                p.repellors++;
            }

            for (int i = 0; i < lP.Length; i++)
            {
                float angle = math.atan2(r.Value.value.y, lPR[i].Value.value.y);

                if (angle <= math.radians(p.attractionRot) && angle >= math.radians(-p.attractionRot) && math.abs(p.speed - lPS[i]) <= p.attractionSpeedTolerance)
                {
                    p.attraction += (lP[i].Value - t.Value) / lD[i];
                    p.attractors++;
                }
            }
        }

        private void DensityCalculationJob(Entity e, int entityInQueryIndex, ref DensityAvoidanceBrain b,
            ref Pedestrian p, in Translation t, NativeList<Translation> lP)
        {
            float percentFull = lP.Length / (math.PI * math.pow(p.maxDist, 2));
            float modifier = percentFull <= 0.99f ? percentFull : 0.99f;

            p.speed = p.baseSpeed - (p.baseSpeed * modifier);

            p.minDist = p.baseMinDist - (p.baseMinDist * modifier);
            p.minDist = p.minDist < 2f ? 2f : p.minDist;

            if (b.elapsedTime >= b.maxTime)
            {
                if (modifier >= b.maxDensityTolerance || modifier <= b.minDensityTolerance)
                {
                    ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
                }

                b.elapsedTime = 0;
            }
            else
            {
                b.elapsedTime += deltaTime;
            }
        }

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref DensityAvoidanceBrain b,
            ref Pedestrian p, in Translation t, in Rotation r, in DynamicBuffer<WaypointList> w)
        {
            var localPedestrians = new NativeList<Translation>(Allocator.Temp);
            var localPedestrianRots = new NativeList<Rotation>(Allocator.Temp);
            var localPedestrianSpeeds = new NativeList<float>(Allocator.Temp);
            var localDistances = new NativeList<float>(Allocator.Temp);

            var highlyLocalPedestrians = new NativeList<Translation>(Allocator.Temp);
            var highlyLocalDistances = new NativeList<float>(Allocator.Temp);

            for (int i = 0; i < pedestrianArray.Length; i++)
            {
                float3 target = pedestrianArray[i].Value;
                float dist = math.distance(t.Value, target);

                if (dist <= p.maxDist)
                {
                    if (dist <= p.minDist)
                    {
                        if (dist > 0.001f)
                        {
                            highlyLocalPedestrians.Add(pedestrianArray[i]);
                            highlyLocalDistances.Add(dist);
                        }
                    }
                    else
                    {
                        localPedestrians.Add(pedestrianArray[i]);
                        localPedestrianRots.Add(pedestrianRotArray[i]);
                        localPedestrianSpeeds.Add(pedestrianSpeedArray[i]);
                        localDistances.Add(dist);
                    }
                }
            }

            CoreVectorCalculationJob(ref p, t, r, w, localPedestrians, highlyLocalPedestrians, localPedestrianRots, localPedestrianSpeeds, localDistances, highlyLocalDistances);
            DensityCalculationJob(e, entityInQueryIndex, ref b,
                ref p, t, localPedestrians);
        }
    }

    [BurstCompile]
    private partial struct WaypointRendezvousProgressionJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;

        public float deltaTime;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public void Execute(ref Pedestrian p, ref Wait w, ref DynamicBuffer<GoalKeyList> g, in Translation t)
        {
            //Debug.Log("running light attraction");
            p.lightAttraction = new float3(0, 0, 0);
            p.lightAttractors = 0;

            // Calculate light attraction
            var rendezvousPoint = waypointArray[g[0].key];
            var distance = math.distance(t.Value, rendezvousPoint.Value);
            /*RaycastInput input;
             bool hasHit;


             float3 from = t.Value, to = light.Value;

             input = new RaycastInput()
             {
                 Start = from,
                 End = to,
                 Filter = new CollisionFilter
                 {
                     BelongsTo = 1 << 0,
                     CollidesWith = 1 << 1,
                 }
             };

             hasHit = collisionWorld.CastRay(input);*/

            //if (distance <= p.maxDist && !hasHit)
            if (distance <= p.tolerance)
            {
                w.elapsedTime += deltaTime;
            }
        }
    }

    [BurstCompile]
    private partial struct WaypointFinalVectorCalculationJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;

        public float deltaTime;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public void Execute(ref PhysicsVelocity v, ref Translation t, ref Rotation r, ref Pedestrian p)
        {
            float3 target = p.target, attraction = p.attraction, repulsion = p.repulsion, obstacle = p.obstacle, lightAttraction = p.lightAttraction;
            bool isZero;

            //Debug.DrawRay(t.Value, p.target, Color.blue);
            //Debug.DrawRay(t.Value, p.attraction, Color.green);
            //Debug.DrawRay(t.Value, p.repulsion, Color.red);
            //Debug.DrawRay(t.Value, p.obstacle, Color.yellow);
            //Debug.DrawRay(t.Value, p.lightAttraction, Color.black);

            if (p.attractors != 0)
            {
                attraction /= p.attractors;
                attraction = math.normalize(attraction);
            }

            if (p.repellors != 0)
            {
                repulsion /= p.repellors;
                repulsion = math.normalize(repulsion);
            }

            if (p.lightAttractors != 0)
            {
                lightAttraction /= p.lightAttractors;
                lightAttraction = math.normalize(lightAttraction);
            }

            target = target.x == 0 && target.y == 0 && target.z == 0 ? target : math.normalize(target);

            obstacle = obstacle.x == 0 && obstacle.y == 0 && obstacle.z == 0 ? obstacle : math.normalize(obstacle);

            float3 final = ((target * p.targetFac) +
            (attraction * p.attractionFac) +
            (repulsion * p.repulsionFac) + (obstacle * p.obstacleFac) + (lightAttraction * p.lightFac)) / 5;

            //Debug.DrawRay(t.Value, final, Color.cyan);

            isZero = final.x == 0 && final.y == 0 && final.z == 0;

            final = isZero ? final : math.normalize(final);

            r.Value.value.x = 0;
            r.Value.value.z = 0;
            v.Angular = 0;

            if (!isZero)
            {
                r.Value = math.slerp(r.Value, quaternion.LookRotation(final, math.up()), deltaTime * p.rotSpeed);

                if (p.isClimbing)
                {
                    t.Value -= math.float3(0, t.Value.y - 3.5f, 0);

                    v.Linear = math.forward(r.Value) * (p.speed/2);
                }
                else
                {
                    t.Value -= math.float3(0, t.Value.y - 1.5f, 0);

                    v.Linear = math.forward(r.Value) * p.speed;
                }
            }
            else
            {
                v.Linear = math.float3(0, 0, 0);
            }

            
        }
    }

    [BurstCompile]
    [WithNone(typeof(Wait))]
    private partial struct WaypointGoalAdvancementJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;

        public float deltaTime;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public float elapsedTime;
        public NativeList<float> results;

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref Translation t, ref Pedestrian p, ref DynamicBuffer<WaypointList> w, ref DynamicBuffer<GoalKeyList> g)
        {
            float dist = math.distance(t.Value, waypointArray[w[0].key].Value);

            /*Debug.Log("Start of waypoint list");

            for (int i = 0; i < w.Length; i++)
            {
                var key = w[i].key;

                Debug.Log($"Waypoint key: {key}, waypoint position: {waypointArray[key].Value}");
            }

            Debug.Log("End of waypoint list");*/

            if (dist < p.tolerance)
            {
                if (w.Length > 1 && !WaypointVisibilityCheck(w[0].key, waypointArray, collisionWorld, t) && !WaypointVisibilityCheck(w[1].key, waypointArray, collisionWorld, t))
                {
                    w.RemoveAt(0);
                }
                else if (w.Length == 1 && !WaypointVisibilityCheck(w[0].key, waypointArray, collisionWorld, t))
                {
                    if (w[0].key == g[0].key)
                    {
                        if (g.Length > 1)
                        {
                            //Debug.Log("Removing in main loop");
                            g.RemoveAt(0);

                            ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
                        }
                        else if (math.distance(t.Value, waypointArray[g[0].key].Value) < p.tolerance)
                        {
                            results.Add(0.1f - (0.1f*(elapsedTime / 60)));
                            ecbpw.DestroyEntity(entityInQueryIndex, e);
                        }
                        else
                        {
                            ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
                        }
                    }
                    else
                    {
                        ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
                    }
                }
            }
        }
    }

    [BurstCompile]
    [WithAll(typeof(Wait))]
    private partial struct WaypointRendezvousGoalAdvancementJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;

        public float deltaTime;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref Translation t, ref Pedestrian p, ref DynamicBuffer<WaypointList> w, ref DynamicBuffer<GoalKeyList> g, in Wait wait)
        {
            //Debug.Log("Waiting bro");

            float dist = math.distance(t.Value, waypointArray[w[0].key].Value);

            if (dist < p.tolerance)
            {
                if (w.Length > 1 && !WaypointVisibilityCheck(w[0].key, waypointArray, collisionWorld, t) && !WaypointVisibilityCheck(w[1].key, waypointArray, collisionWorld, t))
                {
                    w.RemoveAt(0);
                }
                else if (w.Length == 1 && w[0].key != g[0].key)
                {
                    ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
                }
            }
            
            if (wait.elapsedTime >= wait.maxTime)
            {
                //Debug.Log("Removing in wait loop");
                g.RemoveAt(0);

                ecbpw.RemoveComponent<Wait>(entityInQueryIndex, e);
                ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
            }
        }
    }
}
