using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

[UpdateAfter(typeof(CrowdMovementSystem))]
public partial class PedestrianMovementSystem : SystemBase
{
    [WithAll(typeof(FleeingTag))]
    private partial struct FleeingLocalAgentCalculationJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Translation> pedestrianArray;
        [ReadOnly] public NativeArray<Rotation> pedestrianRotArray;
        [ReadOnly] public NativeArray<float> pedestrianSpeedArray;

        private void CoreVectorCalculationJob(ref Pedestrian p, in Translation t, in Rotation r, in Goal g, NativeList<Translation> lP, NativeList<Translation> hLP, NativeList<Rotation> lPR, NativeList<float> lPS, NativeList<float> lD, NativeList<float> hLD)
        {
            p.attraction = new float3(0, 0, 0);
            p.repulsion = new float3(0, 0, 0);

            p.attractors = 0;
            p.repellors = 0;

            p.target = g.exit.Value - t.Value;

            for (int i = 0; i < hLP.Length; i++)
            {
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

        private void DensityCalculationJob(ref Pedestrian p, NativeList<Translation> lP)
        {
            float percentFull = lP.Length / (math.PI * math.pow(p.maxDist, 2));
            float modifier = percentFull <= 0.99f ? percentFull : 0.99f;

            p.speed = p.baseSpeed - (p.baseSpeed * modifier);

            p.minDist = p.baseMinDist - (p.baseMinDist * modifier);
            p.minDist = p.minDist < 1.1f ? 1.1f : p.minDist;
        }

        public void Execute(ref Pedestrian p, in Translation t, in Rotation r, in Goal g)
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

            CoreVectorCalculationJob(ref p, t, r, g, localPedestrians, highlyLocalPedestrians, localPedestrianRots, localPedestrianSpeeds, localDistances, highlyLocalDistances);
            DensityCalculationJob(ref p, localPedestrians);
        }
    }

    [WithAll(typeof(FleeingTag))]
    private partial struct FleeingFinalVectorCalculationJob : IJobEntity
    {
        public float deltaTime;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Pedestrian p, in Goal g)
        {
            float3 target = p.target, attraction = p.attraction, repulsion = p.repulsion, obstacle = p.obstacle, lightAttraction = p.lightAttraction;
            float dist = math.distance(t.Value, g.exit.Value);
            bool isZero;

            /*Debug.DrawRay(t.Value, p.target, Color.blue);
            Debug.DrawRay(t.Value, p.attraction, Color.green);
            Debug.DrawRay(t.Value, p.repulsion, Color.red);
            Debug.DrawRay(t.Value, p.obstacle, Color.yellow);*/
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

            rot.Value.value.x = 0;
            rot.Value.value.z = 0;
            velocity.Angular = 0;

            if (!isZero)
            {
                rot.Value = math.slerp(rot.Value, quaternion.LookRotation(final, math.up()), deltaTime * p.rotSpeed);

                if (p.isClimbing)
                {
                    velocity.Linear = math.forward(rot.Value) * p.speed * 0.5f;
                }
                else
                {
                    velocity.Linear = math.forward(rot.Value) * p.speed;
                }
            }
            else
            {
                velocity.Linear = math.float3(0, 0, 0);
            }

            if (p.isClimbing)
            {
                t.Value -= math.float3(0, t.Value.y - 3.5f, 0);
            }
            else
            {
                t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
            }

            if (dist < p.tolerance)
            {
                ecbpw.DestroyEntity(entityInQueryIndex, e);
            }
        }
    }
}
