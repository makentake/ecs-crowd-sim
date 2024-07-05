using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Physics;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial class FinalMovementSystem : SystemBase
{
    private EntityQuery waypointQuery;

    [BurstCompile]
    private partial struct FinalVectorCalculationJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int, Translation> waypointArray;

        public float deltaTime;

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

            //r.Value.value.x = 0;
            //r.Value.value.z = 0;
            v.Angular = math.float3(0, 0, 0);
            v.Linear = math.float3(0, 0, 0);

            if (!isZero)
            {
                r.Value = math.slerp(r.Value, quaternion.LookRotation(final, math.up()), math.clamp(1f, 0f, deltaTime * p.rotSpeed));

                if (p.isClimbing)
                {
                    t.Value -= math.float3(0, t.Value.y - 3.5f, 0);

                    //t.Value += math.forward(r.Value) * (p.speed / 2) * deltaTime;

                    v.Linear = math.forward(r.Value) * (p.speed / 2);
                }
                else
                {
                    t.Value -= math.float3(0, t.Value.y - 1.5f, 0);

                    //t.Value += math.forward(r.Value) * p.speed * deltaTime;

                    v.Linear = math.forward(r.Value) * p.speed;
                }
            }
        }
    }

    protected override void OnUpdate()
    {
        var dt = Time.DeltaTime;

        waypointQuery = GetEntityQuery(ComponentType.ReadOnly<Waypoint>(), ComponentType.ReadOnly<WaypointDensity>(), ComponentType.ReadOnly<Translation>());
        var waypoints = new NativeParallelHashMap<int, Translation>(waypointQuery.CalculateEntityCount(), Allocator.TempJob);
        var waypointsParallelWriter = waypoints.AsParallelWriter();

        var currentTime = GetSingleton<ElapsedTimeComponent>().elapsedTime;
        SetSingleton(new ElapsedTimeComponent
        {
            elapsedTime = currentTime + dt,
            deltaTime = dt
        });

        //Debug.Log($"deltaTime: {dt}. elapsedTime: {currentTime+dt}");

        Entities
            .ForEach((in Waypoint w, in Translation t) =>
            {
                waypointsParallelWriter.TryAdd(w.key, t);
            }).ScheduleParallel();

        JobHandle final = new FinalVectorCalculationJob
        {
            waypointArray = waypoints,
            deltaTime = dt,
        }.ScheduleParallel();

        waypoints.Dispose(Dependency);
    }
}
