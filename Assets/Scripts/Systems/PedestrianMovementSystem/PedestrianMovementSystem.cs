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
[UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
//[UpdateAfter(typeof(CrowdMovementSystem))]
//[UpdateBefore(typeof(TransformSystemGroup))]
public partial class PedestrianMovementSystem : SystemBase
{
    public NativeList<float> rewards;
    public float elapsedTime;

    private EndVariableRateSimulationEntityCommandBufferSystem end;
    //private EndSimulationEntityCommandBufferSystem end;
    private EntityQuery pedestrianQuery, lightQuery, waypointQuery;
    private Unity.Physics.Systems.BuildPhysicsWorld physicsWorld;

    // Set up the physics world for raycasting and the entity command buffer system
    protected override void OnStartRunning()
    {
        /*var rateManager = new RateUtils.VariableRateManager(17);
        var variableRateSystem = World.GetExistingSystem<VariableRateSimulationSystemGroup>();
        variableRateSystem.RateManager = rateManager;*/

        end = World.GetOrCreateSystem<EndVariableRateSimulationEntityCommandBufferSystem>();
        //end = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
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

        var dt = UnityEngine.Time.fixedDeltaTime * UnityEngine.Time.timeScale;
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

        elapsedTime += dt;

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
            deltaTime = dt,
            waypointArray = waypoints,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

        Entities
            .WithNone<WaypointFollower>()
            .WithReadOnly(lightTranslation)
            .WithReadOnly(collisionWorld)
            .ForEach((Entity e, int entityInQueryIndex, ref Pedestrian p, ref Wait w, in Translation t) =>
            {
                p.lightAttraction = new float3(0, 0, 0);
                p.lightAttractors = 0;

                if (w.elapsedTime >= w.maxTime)
                {
                    ecb.RemoveComponent<Wait>(entityInQueryIndex, e);
                }
                else
                {
                    // Calculate light attraction
                    foreach (Translation light in lightTranslation)
                    {
                        RaycastInput input;
                        bool hasHit;
                        var distance = math.distance(t.Value, light.Value);

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

                        hasHit = collisionWorld.CastRay(input);

                        if (distance <= p.lightRange && !hasHit)
                        {
                            w.elapsedTime += dt;
                            p.lightAttraction += light.Value - t.Value;
                            p.lightAttractors++;
                        }
                    }
                }
            }).ScheduleParallel();

        JobHandle waypointRendezvousProgressionJob = new WaypointRendezvousProgressionJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            deltaTime = dt,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

        JobHandle youngWaypointObstacle = new YoungWaypointObstacleAvoidanceJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

        JobHandle waypointFinal = new WaypointFinalVectorCalculationJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            deltaTime = dt,
            ecbpw = end.CreateCommandBuffer().AsParallelWriter()
        }.ScheduleParallel();

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
