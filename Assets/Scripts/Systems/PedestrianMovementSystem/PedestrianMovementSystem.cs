using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Jobs;
using Unity.Burst;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

// System for moving a peaceful crowd
//[UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
//[UpdateAfter(typeof(CrowdMovementSystem))]
//[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(NavigationSystem))]
public partial class PedestrianMovementSystem : SystemBase
{
    public NativeList<float> rewards;
    public float elapsedTime;

    //private EndVariableRateSimulationEntityCommandBufferSystem end;
    private EndSimulationEntityCommandBufferSystem end;
    private EntityQuery pedestrianQuery, lightQuery, waypointQuery;
    private Unity.Physics.Systems.BuildPhysicsWorld physicsWorld;

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
        //public float deltaTime; // only needed for elapsed time
        public float elapsedTime;

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

            if (elapsedTime - b.startTime >= b.maxTime)
            {
                //if (modifier >= b.maxDensityTolerance || modifier <= b.minDensityTolerance)
                /*if (modifier >= b.maxDensityTolerance)
                {
                    //Debug.Log("recalculating");

                    ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);
                }*/

                //Debug.Log("recalculating");

                ecbpw.AddComponent<AwaitingNavigationTag>(entityInQueryIndex, e);

                b.startTime = elapsedTime;
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
        //public float elapsedTime;
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
                //w.elapsedTime += elapsedTime - w.elapsedTime; // NEEDS FIXING
                w.elapsedTime += deltaTime;
            }
        }
    }

    [BurstCompile]
    [WithNone(typeof(Wait))]
    private partial struct WaypointGoalAdvancementJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld collisionWorld;
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;

        public EntityCommandBuffer ecb;

        public NativeList<float> results;
        public float elapsedTime;

        public void Execute(Entity e, ref Translation t, ref Pedestrian p, ref DynamicBuffer<WaypointList> w, ref DynamicBuffer<GoalKeyList> g)
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

                            ecb.AddComponent<AwaitingNavigationTag>(e);
                        }
                        else if (math.distance(t.Value, waypointArray[g[0].key].Value) < p.tolerance)
                        {
                            if (results.IsCreated)
                            {
                                //Debug.Log($"Reward: {0.1f - (0.1f * (elapsedTime / 60f))}");
                                results.Add(0.1f - (0.1f * (elapsedTime / 60f)));
                            }

                            ecb.DestroyEntity(e);
                        }
                        else
                        {
                            ecb.AddComponent<AwaitingNavigationTag>(e);
                        }
                    }
                    else
                    {
                        ecb.AddComponent<AwaitingNavigationTag>(e);
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

    // Set up the physics world for raycasting and the entity command buffer system
    protected override void OnStartRunning()
    {
        /*var rateManager = new RateUtils.VariableRateManager(17);
        var variableRateSystem = World.GetExistingSystem<VariableRateSimulationSystemGroup>();
        variableRateSystem.RateManager = rateManager;*/

        //end = World.GetOrCreateSystem<EndVariableRateSimulationEntityCommandBufferSystem>();
        end = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        physicsWorld = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();

        rewards = new NativeList<float>(600, Allocator.Persistent);
        elapsedTime = 0f;
    }

    protected override void OnDestroy()
    {
        rewards.Dispose();
    }

    protected override void OnUpdate()
    {
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        //var dt = UnityEngine.Time.fixedDeltaTime * UnityEngine.Time.timeScale;
        //var dt = UnityEngine.Time.deltaTime;
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        pedestrianQuery = GetEntityQuery(ComponentType.ReadOnly<Pedestrian>(), 
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<Rotation>());
        lightQuery = GetEntityQuery(ComponentType.ReadOnly<LightTag>(),
            ComponentType.ReadOnly<Translation>());
        waypointQuery = GetEntityQuery(ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<WaypointDensity>(), ComponentType.ReadOnly<Translation>());

        var pedestrians = new NativeArray<Translation>(pedestrianQuery.CalculateEntityCount(), Allocator.TempJob);
        var pedestrianRot = new NativeArray<Rotation>(pedestrianQuery.CalculateEntityCount(), Allocator.TempJob);
        var pedestrianSpeed = new NativeArray<float>(pedestrianQuery.CalculateEntityCount(), Allocator.TempJob);
        var lightTranslation = lightQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        var waypoints = new NativeParallelHashMap<int, Translation>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var waypointsParallelWriter = waypoints.AsParallelWriter();

        elapsedTime = GetSingleton<ElapsedTimeComponent>().elapsedTime;

        //Debug.Log("deltaTime: " + dt);

        // These two depend heavily on being synced up, so do that manually
        Entities
            .ForEach((int entityInQueryIndex, in Pedestrian p, in Translation t, in Rotation r) =>
            {
                pedestrians[entityInQueryIndex] = t;
                pedestrianRot[entityInQueryIndex] = r;
                pedestrianSpeed[entityInQueryIndex] = p.speed;
            }).ScheduleParallel();

        Entities
            .ForEach((in Waypoint w, in Translation t) =>
            {
                waypointsParallelWriter.TryAdd(w.key, t);
            }).ScheduleParallel();

        JobHandle waypointCore = new WaypointLocalAgentCalculationJob
        {
            pedestrianArray = pedestrians,
            pedestrianRotArray = pedestrianRot,
            pedestrianSpeedArray = pedestrianSpeed,
            //deltaTime = dt,
            elapsedTime = elapsedTime,
            waypointArray = waypoints,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

        JobHandle waypointRendezvousProgressionJob = new WaypointRendezvousProgressionJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            deltaTime = GetSingleton<ElapsedTimeComponent>().deltaTime,
            //elapsedTime = elapsedTime,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

        JobHandle youngWaypointObstacle = new YoungWaypointObstacleAvoidanceJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

        /*JobHandle waypointFinal = new WaypointFinalVectorCalculationJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            deltaTime = dt,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();*/

        JobHandle rendezvousGoalAdvancement = new WaypointRendezvousGoalAdvancementJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

        JobHandle goalAdvancement = new WaypointGoalAdvancementJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            elapsedTime = elapsedTime,
            ecb = end.CreateCommandBuffer(), // DON'T USE PARALLEL COMMAND BUFFERS IN SINGLE-THREADED JOBS
            results = rewards
            //}.ScheduleParallel();
        }.Schedule();

        pedestrians.Dispose(Dependency);
        pedestrianRot.Dispose(Dependency);
        pedestrianSpeed.Dispose(Dependency);
        lightTranslation.Dispose(Dependency);
        waypoints.Dispose(Dependency);

        end.AddJobHandleForProducer(Dependency);
    }
}
