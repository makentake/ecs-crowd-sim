using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Entities.UniversalDelegates;

// System for moving a peaceful crowd
[UpdateAfter(typeof(CrowdMovementSystem))]
public partial class PedestrianMovementSystem : SystemBase
{
    private EndFixedStepSimulationEntityCommandBufferSystem end;
    private EntityQuery pedestrianQuery, lightQuery;
    private Unity.Physics.Systems.BuildPhysicsWorld physWorld;

    // Set up the physics world for raycasting and the entity command buffer system
    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        physWorld = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
    }

    private partial struct ObjectAvoidanceJob : IJobEntity
    {
        [ReadOnly] public Unity.Physics.CollisionWorld collisionWorld;
        float3 origin, direction, leftmostRay, resultingMovement, firstNoHitVector;
        quaternion leftmostRotation;
        quaternion angleBetweenRays;
        bool hitOccurred;
        bool foundFirstNoHitVector;
        int maxNoHitRayNum, minNoHitRayNum, multiplier, rayNumber, midRayNumber;
        float distance, minDistance;

        bool SingleRay(int angle, Translation t, Rotation r, ObstacleAvoidance o)
        {
            RaycastInput input;

            float3 from = t.Value, to = t.Value + (math.mul(quaternion.RotateY(angle), math.forward(r.Value)) * o.visionLength);

            input = new RaycastInput()
            {
                Start = from,
                End = to,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1 << 0,
                    CollidesWith = 3 << 1,
                }
            };

            Unity.Physics.RaycastHit hit;
            bool hasHit = collisionWorld.CastRay(input, out hit);

            distance = from.x == hit.Position.x && from.y == hit.Position.y && from.z == hit.Position.z ? 0 : math.distance(from, hit.Position);

            return hasHit;
        }

        bool hasWallBelow(Translation t)
        {
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
            bool hasHit = collisionWorld.CastRay(input, out hit);

            return hasHit;
        }

        RaycastInput RayArcInput(float3 leftmostRay, float3 origin, ObstacleAvoidance o, int rayNumber, Rotation r)
        {
            float angle = rayNumber * math.radians(o.visionAngle / (o.numberOfRays));

            angleBetweenRays = quaternion.RotateY(angle);
            direction = math.mul(angleBetweenRays, leftmostRay);

            return new RaycastInput()
            {
                Start = origin,
                End = origin + (direction * o.visionLength),
                Filter = new CollisionFilter()
                {
                    BelongsTo = 1 << 0,
                    CollidesWith = 3 << 1,
                }
            };
        }

        // A function to visualize the ray arc
        void RaycastVisualization(int i, RaycastInput input)
        {
            switch (i)
            {
                case 1:
                    //Debug.DrawLine(input.Start, input.End, Color.magenta);
                    Debug.DrawLine(input.Start, input.End);
                    break;

                case 2:
                    //Debug.DrawLine(input.Start, input.End, Color.cyan);
                    Debug.DrawLine(input.Start, input.End);
                    break;

                case 3:
                    //Debug.DrawLine(input.Start, input.End, Color.black);
                    Debug.DrawLine(input.Start, input.End);
                    break;

                case 4:
                    //Debug.DrawLine(input.Start, input.End, Color.blue);
                    Debug.DrawLine(input.Start, input.End);
                    break;

                case 5:
                    //Debug.DrawLine(input.Start, input.End, Color.grey);
                    Debug.DrawLine(input.Start, input.End);
                    break;

                case 6:
                    //Debug.DrawLine(input.Start, input.End, Color.red);
                    Debug.DrawLine(input.Start, input.End);
                    break;

                default:
                    Debug.DrawLine(input.Start, input.End);
                    break;
            }
        }

        void SetUpVariables(Translation t, Rotation r, ObstacleAvoidance o)
        {
            origin = t.Value;
            direction = new float3(0, 0, 1); // this value would change depending on what direction is 'forward' for the agent
            direction = math.mul(r.Value, direction);

            distance = -1;
            minDistance = o.visionLength / 4;

            leftmostRotation = quaternion.RotateY(math.radians(-o.visionAngle / 2));
            angleBetweenRays = new quaternion();

            leftmostRay = math.mul(leftmostRotation, direction);

            resultingMovement = float3.zero;
            hitOccurred = false;

            firstNoHitVector = float3.zero; // Should make this backwards
            foundFirstNoHitVector = false;

            maxNoHitRayNum = -1;
            minNoHitRayNum = -1;

            multiplier = -1; //if have left tendency, multiply count by -1
            rayNumber = (o.numberOfRays) / 2;
            midRayNumber = rayNumber;
        }

        void CastRayArc(ObstacleAvoidance o, Rotation r)
        {
            for (int i = 1; i <= o.numberOfRays; i++, multiplier *= -1)
            {
                var input = RayArcInput(leftmostRay, origin, o, rayNumber, r);

                //RaycastVisualization(i, input);

                Unity.Physics.RaycastHit hit = new Unity.Physics.RaycastHit();
                bool haveHit = collisionWorld.CastRay(input, out hit);
                if (haveHit)
                {
                    //Debug.DrawRay(hit.Position, math.forward(), Color.green);

                    hitOccurred = true;
                }
                else // If a hit has occurred
                {
                    if (!foundFirstNoHitVector)
                    {
                        firstNoHitVector = input.End - input.Start;
                        foundFirstNoHitVector = true;
                        resultingMovement = o.movementPerRay * math.normalize(firstNoHitVector);
                        minNoHitRayNum = rayNumber;
                        maxNoHitRayNum = rayNumber;
                    }
                    else
                    {
                        if (rayNumber > midRayNumber) // the ray is to the right
                        {
                            if (rayNumber - maxNoHitRayNum == 1) // if this ray is next to the max no hit ray
                            {
                                maxNoHitRayNum = rayNumber;// this ray is the new max no hit ray
                                resultingMovement += o.movementPerRay * math.normalize(input.End - input.Start);
                            }
                        }
                        else // the ray is to the left
                        {
                            if (minNoHitRayNum - rayNumber == 1) // if this ray is next to the min no hit ray
                            {
                                minNoHitRayNum = rayNumber;// this ray is the new max no hit ray
                                resultingMovement += o.movementPerRay * math.normalize(input.End - input.Start);
                            }
                        }
                    }
                }

                rayNumber += multiplier * i;
            }
        }

        public void Execute(ref Pedestrian p, ref Translation t, in Rotation r, in ObstacleAvoidance o)
        {
            SetUpVariables(t, r, o);

            CastRayArc(o, r);

            if (hitOccurred) // if there was at least one hit
            {
                if (minNoHitRayNum == -1 && maxNoHitRayNum == -1)
                {
                    //Debug.Log("all rays hit");

                    bool left = SingleRay(-90, t, r, o);
                    bool right = SingleRay(90, t, r, o);
                    //Debug.Log(distance);

                    SingleRay(0, t, r, o);
                    //Debug.Log(distance);

                    if (distance <= minDistance)
                    {
                        if (!right)
                        {
                            SetUpVariables(t, r, o);

                            leftmostRay = math.mul(quaternion.RotateY(90), leftmostRay);

                            CastRayArc(o, r);

                            p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;
                        }
                        else if (!left)
                        {
                            SetUpVariables(t, r, o);

                            leftmostRay = math.mul(quaternion.RotateY(-90), leftmostRay);

                            CastRayArc(o, r);

                            p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;
                        }
                        else
                        {
                            SetUpVariables(t, r, o);

                            leftmostRay = math.mul(quaternion.RotateY(180), leftmostRay);

                            CastRayArc(o, r);

                            p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;
                        }
                    }
                }
                else
                {
                    p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;
                }
            }
            else
            {
                p.obstacle = float3.zero;
            }
        }
    }

    protected override void OnUpdate()
    {
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        var dt = Time.DeltaTime;
        var collisionWorld = physWorld.PhysicsWorld.CollisionWorld;
        pedestrianQuery = GetEntityQuery(ComponentType.ReadOnly<Pedestrian>(), 
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<Rotation>());
        lightQuery = GetEntityQuery(ComponentType.ReadOnly<LightTag>(),
            ComponentType.ReadOnly<Translation>());

        var pedestrians = pedestrianQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        var pedestrianRot = pedestrianQuery.ToComponentDataArray<Rotation>(Allocator.TempJob);
        var lightTranslation = lightQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

        // Calculate the vectors for moving agents
        Entities
            .WithReadOnly(pedestrians)
            .WithReadOnly(pedestrianRot)
            .WithNone<Police, FleeingTag>()
            .ForEach((int entityInQueryIndex, ref Pedestrian p, in Translation t, in Rotation r, in Goal g) =>
            {
                p.attraction = new float3(0, 0, 0);
                p.repulsion = new float3(0, 0, 0);

                p.attractors = 0;
                p.repellors = 0;

                
                p.target = g.goal.Value - t.Value;

                for (int i = 0; i < pedestrians.Length; i++)
                {
                    float3 target = pedestrians[i].Value;
                    float dist = math.distance(t.Value, target);
                    float angle = math.atan2(r.Value.value.y, pedestrianRot[i].Value.value.y);

                    if (dist >= 0.01)
                    {
                        // Calculate repulsion
                        if (dist < p.minDist)
                        {
                            p.repulsion += (t.Value - target) / dist;
                            p.repellors++;
                        }

                        else if (angle < math.radians(p.attractionRot) && angle != 0)
                        {
                            if (dist <= p.maxDist)
                            {
                                p.attraction += (target - t.Value) / angle;
                                p.attractors++;
                            }
                        }
                    }
                }
                
            }).ScheduleParallel();

        // Calculate the vectors for leaving agents
        Entities
            .WithReadOnly(pedestrians)
            .WithReadOnly(pedestrianRot)
            .WithNone<Police>()
            .WithAll<FleeingTag>()
            .ForEach((int entityInQueryIndex, ref Pedestrian p, in Translation t, in Rotation r, in Goal g) =>
            {
                p.attraction = new float3(0, 0, 0);
                p.repulsion = new float3(0, 0, 0);

                p.attractors = 0;
                p.repellors = 0;

                p.target = g.exit.Value - t.Value;

                for (int i = 0; i < pedestrians.Length; i++)
                {
                    float3 target = pedestrians[i].Value;
                    float dist = math.distance(t.Value, target);
                    float angle = math.atan2(r.Value.value.y, pedestrianRot[i].Value.value.y);

                    if (dist >= 0.01)
                    {
                        // Calculate repulsion
                        if (dist < p.minDist)
                        {
                            p.repulsion += (t.Value - target) / dist;
                            p.repellors++;
                        }

                        else if (angle < math.radians(p.attractionRot) && angle != 0)
                        {
                            if (dist <= p.maxDist)
                            {
                                p.attraction += (target - t.Value) / angle;
                                p.attractors++;
                            }
                        }
                    }
                }

            }).ScheduleParallel();

        Entities
            .WithReadOnly(lightTranslation)
            .WithReadOnly(collisionWorld)
            .ForEach((Entity e, int entityInQueryIndex, ref Pedestrian p, ref WaitTag w, in Translation t) =>
            {
                p.lightAttraction = new float3(0, 0, 0);
                p.lightAttractors = 0;

                

                if (w.currentTime >= w.maxTime)
                {
                    ecb.RemoveComponent<WaitTag>(entityInQueryIndex, e);
                }
                else
                {
                    // Calculate light attraction
                    foreach (Translation light in lightTranslation)
                    {
                        RaycastInput input;

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

                        bool hasHit = collisionWorld.CastRay(input);

                        var distance = math.distance(t.Value, light.Value);

                        if (distance <= p.lightRange && !hasHit)
                        {
                            w.currentTime += dt;
                            p.lightAttraction += light.Value - t.Value;
                            p.lightAttractors++;
                        }
                    }
                }
            }).ScheduleParallel();
        
        // Calculate obstacle avoidance
        JobHandle obstacle = new ObjectAvoidanceJob
        {
            collisionWorld = physWorld.PhysicsWorld.CollisionWorld
        }.ScheduleParallel();

        // this job is in another partial class
        JobHandle youngObstacle = new YoungObjectAvoidanceJob
        {
            collisionWorld = physWorld.PhysicsWorld.CollisionWorld
        }.ScheduleParallel();

        // Calculate final values for leaving agents and move them
        Entities
            .WithAll<FleeingTag>()
            .ForEach((Entity e, int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Pedestrian p, in Goal g) =>
            {
                float3 target = p.target, attraction = p.attraction, repulsion = p.repulsion, obstacle = p.obstacle, lightAttraction = p.lightAttraction;
                float dist = math.distance(t.Value, g.exit.Value);

                /*Debug.DrawRay(t.Value, p.target, Color.blue);
                Debug.DrawRay(t.Value, p.attraction, Color.green);
                Debug.DrawRay(t.Value, p.repulsion, Color.red);
                Debug.DrawRay(t.Value, p.obstacle, Color.yellow);*/
                //Debug.DrawRay(t.Value, p.lightAttraction, Color.black);

                /*float repulsionMagnitude = math.distance(math.float3(0, 0, 0),
                    repulsion.x == 0 && repulsion.y == 0 && repulsion.z == 0 ? math.float3(0.25f, 0.25f, 0.25f) : repulsion);

                float speedModifier = repulsionMagnitude <= 1 ? 1 : repulsionMagnitude;*/

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

                /*float3 final = ((target * p.targetFac) +
                (attraction * p.attractionFac) +
                (obstacle * p.obstacleFac) + (lightAttraction * p.lightFac)) / 4;*/

                //Debug.DrawRay(t.Value, final, Color.cyan);

                bool isZero = final.x == 0 && final.y == 0 && final.z == 0;

                final = isZero ? final : math.normalize(final);

                if (p.isClimbing)
                {
                    t.Value -= math.float3(0, t.Value.y - 3.5f, 0);
                }
                else
                {
                    t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
                }

                rot.Value.value.x = 0;
                rot.Value.value.z = 0;
                velocity.Angular = 0;

                if (!isZero)
                {
                    rot.Value = math.slerp(rot.Value, quaternion.LookRotation(final, math.up()), dt * p.rotSpeed);

                    if (p.isClimbing)
                    {
                        velocity.Linear = (math.forward(rot.Value) * p.speed * 0.5f) - (repulsion * 0.90f);
                    }
                    else
                    {
                        velocity.Linear = (math.forward(rot.Value) * p.speed) - (repulsion * 0.90f);
                    }
                }
                else
                {
                    velocity.Linear = math.float3(0, 0, 0);
                }

                p.heading = rot.Value;

                if (dist < p.tolerance)
                {
                    ecb.DestroyEntity(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

        // Calculate final values for normal agents and move them
        Entities
            .WithNone<FleeingTag>()
            .ForEach((Entity e, int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Pedestrian p, in Goal g) =>
            {
                float3 target = p.target, attraction = p.attraction, repulsion = p.repulsion, obstacle = p.obstacle, lightAttraction = p.lightAttraction;
                float dist = math.distance(t.Value, g.goal.Value);

                /*Debug.DrawRay(t.Value, p.target, Color.blue);
                Debug.DrawRay(t.Value, p.attraction, Color.green);
                Debug.DrawRay(t.Value, p.repulsion, Color.red);
                Debug.DrawRay(t.Value, p.obstacle, Color.yellow);*/
                //Debug.DrawRay(t.Value, p.lightAttraction, Color.black);

                /*float repulsionMagnitude = math.distance(math.float3(0, 0, 0),
                    repulsion.x == 0 && repulsion.y == 0 && repulsion.z == 0 ? math.float3(0.25f, 0.25f, 0.25f) : repulsion);

                float speedModifier = repulsionMagnitude <= 1 ? 1 : repulsionMagnitude;*/

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

                /*float3 final = ((target * p.targetFac) +
                (attraction * p.attractionFac) +
                (obstacle * p.obstacleFac) + (lightAttraction * p.lightFac)) / 4;*/

                //Debug.DrawRay(t.Value, final, Color.cyan);

                bool isZero = final.x == 0 && final.y == 0 && final.z == 0;

                final = isZero ? final : math.normalize(final);

                if (p.isClimbing)
                {
                    t.Value -= math.float3(0, t.Value.y - 3.5f, 0);
                }
                else
                {
                    t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
                }
                
                rot.Value.value.x = 0;
                rot.Value.value.z = 0;
                velocity.Angular = 0;

                if (!isZero)
                {
                    rot.Value = math.slerp(rot.Value, quaternion.LookRotation(final, math.up()), dt * p.rotSpeed);

                    float encirclementTolerance = 0.1f;

                    if (repulsion.x <= encirclementTolerance && repulsion.y <= encirclementTolerance && repulsion.z <= encirclementTolerance && p.repellors != 0)
                    {
                        if (p.isClimbing)
                        {
                            velocity.Linear = math.forward(rot.Value) * p.speed * 0.2f * 0.5f;
                        }
                        else
                        {
                            velocity.Linear = math.forward(rot.Value) * p.speed * 0.2f;
                        }
                    }
                    else
                    {
                        if (p.isClimbing)
                        {
                            velocity.Linear = (math.forward(rot.Value) * p.speed * 0.5f) - (repulsion * 0.80f);
                        }
                        else
                        {
                            velocity.Linear = (math.forward(rot.Value) * p.speed) - (repulsion * 0.80f);
                        }
                    }
                                   
                }
                else
                {
                    velocity.Linear = math.float3(0, 0, 0);
                }

                p.heading = rot.Value;

                if (dist < p.tolerance*3)
                {
                    ecb.AddComponent<FleeingTag>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

        end.AddJobHandleForProducer(Dependency);

        pedestrians.Dispose(Dependency);
        pedestrianRot.Dispose(Dependency);
        lightTranslation.Dispose(Dependency);
    }
}
