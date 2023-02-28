using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Jobs;

// System for moving a peaceful crowd
[UpdateAfter(typeof(CrowdMovementSystem))]
public partial class PedestrianMovementSystem : SystemBase
{
    private EndFixedStepSimulationEntityCommandBufferSystem end;
    private EntityQuery pedestrianQuery, policeQuery, lightQuery;
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

        bool SingleRay(int angle, bool isYoung, Translation t, Rotation r, ObstacleAvoidance o)
        {
            RaycastInput input;

            float3 from = t.Value, to = t.Value + (math.mul(quaternion.RotateY(angle), math.forward(r.Value)) * o.visionLength);

            // Check if the agent is young
            if (!isYoung)
            {
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
            else
            {
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

                Unity.Physics.RaycastHit hit;
                bool hasHit = collisionWorld.CastRay(input, out hit);

                distance = from.x == hit.Position.x && from.y == hit.Position.y && from.z == hit.Position.z ? 0 : math.distance(from, hit.Position);

                return hasHit;
            }
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

        RaycastInput RayArcInput(float3 leftmostRay, float3 origin, ObstacleAvoidance o, int rayNumber, Rotation r, bool isYoung)
        {
            float angle = rayNumber * math.radians(o.visionAngle / (o.numberOfRays));

            //Debug.Log(string.Format("Angle for ray {0}: {1}", i, angle));
            angleBetweenRays = quaternion.RotateY(angle);
            // Debug.Log(string.Format("Ray {0}'s angle = {1}", i - 1, angleBetweenRays));
            direction = math.mul(angleBetweenRays, leftmostRay);
            //Debug.Log(string.Format("Ray {0}'s direction = {1}", i, direction));
            if (!isYoung)
            {
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
            else
            {
                return new RaycastInput()
                {
                    Start = origin,
                    End = origin + (direction * o.visionLength),
                    Filter = new CollisionFilter()
                    {
                        BelongsTo = 1 << 0,
                        CollidesWith = 1 << 1,
                    }
                };
            }
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
            minDistance = o.visionLength / 3;

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

        void CastRayArc(ObstacleAvoidance o, Rotation r, bool isYoung)
        {
            for (int i = 1; i <= o.numberOfRays; i++, multiplier *= -1)
            {
                var input = RayArcInput(leftmostRay, origin, o, rayNumber, r, isYoung);

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
                        resultingMovement = o.movementPerRay * math.normalize(input.End - input.Start);
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

            CastRayArc(o, r, p.isYoung);

            if (hitOccurred) // if there was at least one hit
            {
                if (minNoHitRayNum == -1 && maxNoHitRayNum == -1)
                {
                    //Debug.Log("all rays hit");

                    bool left = SingleRay(-90, p.isYoung, t, r, o);
                    bool right = SingleRay(90, p.isYoung, t, r, o);
                    //Debug.Log(distance);

                    SingleRay(0, p.isYoung, t, r, o);
                    //Debug.Log(distance);

                    if (distance <= minDistance)
                    {
                        if (!right)
                        {
                            SetUpVariables(t, r, o);

                            leftmostRay = math.mul(quaternion.RotateY(90), leftmostRay);

                            CastRayArc(o, r, p.isYoung);

                            p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;
                        }
                        else if (!left)
                        {
                            SetUpVariables(t, r, o);

                            leftmostRay = math.mul(quaternion.RotateY(-90), leftmostRay);

                            CastRayArc(o, r, p.isYoung);

                            p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;
                        }
                        else
                        {
                            SetUpVariables(t, r, o);

                            leftmostRay = math.mul(quaternion.RotateY(180), leftmostRay);

                            CastRayArc(o, r, p.isYoung);

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

            // Raise agents to move them over walls
            if (p.isYoung)
            {
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

                //Unity.Physics.RaycastHit hit;
                //bool hasHit = collisionWorld.CastRay(input, out hit);

                //return hasHit;
                if (collisionWorld.CastRay(input))
                {
                    p.isClimbing = true;
                    t.Value += math.forward(r.Value)*0.6f;
                }
                else if (!hasWallBelow(t))
                {
                    p.isClimbing = false;
                }
            }
        }
    }

    protected override void OnUpdate()
    {
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        policeQuery = GetEntityQuery(typeof(Police));
        var dt = Time.DeltaTime;
        var collisionWorld = physWorld.PhysicsWorld.CollisionWorld;
        int pedestrianCount = pedestrianQuery.CalculateEntityCount();
        var lightCount = lightQuery.CalculateEntityCount();

        NativeArray<float3> pedestrians = new NativeArray<float3>(pedestrianCount, Allocator.TempJob);
        NativeArray<quaternion> pedestrianRot = new NativeArray<quaternion>(pedestrianCount, Allocator.TempJob);
        //NativeArray<Entity> officers = policeQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<float3> lightTranslation = new NativeArray<float3>(lightCount, Allocator.TempJob);

        // Get all the agents
        Entities
            .WithStoreEntityQueryInField(ref pedestrianQuery)
            .WithAll<Pedestrian>().ForEach((int entityInQueryIndex, in Translation t, in Rotation r) =>
            {
                pedestrians[entityInQueryIndex] = t.Value;
                pedestrianRot[entityInQueryIndex] = r.Value;
            }).Schedule();

        Entities
            .WithStoreEntityQueryInField(ref lightQuery)
            .WithAll<LightTag>().ForEach((int entityInQueryIndex, in Translation t) =>
            {
                lightTranslation[entityInQueryIndex] = t.Value;
            }).Schedule();

        // Calculate the vectors for moving agents
        Entities
            .WithReadOnly(pedestrians)
            .WithReadOnly(pedestrianRot)
            .WithNone<Police>()
            .ForEach((int entityInQueryIndex, ref Pedestrian p, in Translation t, in Rotation r, in Goal g) =>
            {
                p.attraction = new float3(0, 0, 0);
                p.repulsion = new float3(0, 0, 0);

                p.attractors = 0;
                p.repellors = 0;

                // !! THE AVOIDANCE VARIABLE IS DEPRACATED AND UNUSED !!
                //float3 avoidance = v.obstacle;
                float3 avoidance = math.INFINITY;

                if (avoidance.x != math.INFINITY)
                {
                    p.target = new float3(avoidance.x, 0, avoidance.z);
                }
                else
                {
                    p.target = g.goal.Value - t.Value;

                    for (int i = 0; i < pedestrians.Length; i++)
                    {
                        float3 target = pedestrians[i];
                        float dist = math.distance(t.Value, target);
                        float angle = math.atan2(r.Value.value.y, pedestrianRot[i].value.y);

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
                }
            }).ScheduleParallel();

        Entities
            .WithReadOnly(lightTranslation)
            .ForEach((Entity e, int entityInQueryIndex, ref Pedestrian p, ref WaitTag w, in Translation t) =>
            {
                p.lightAttraction = new float3(0, 0, 0);
                p.lightAttractors = 0;

                w.currentTime += dt;

                if (w.currentTime >= w.maxTime)
                {
                    ecb.RemoveComponent<WaitTag>(entityInQueryIndex, e);
                }
                else
                {
                    // Calculate light attraction
                    foreach (float3 light in lightTranslation)
                    {
                        var distance = math.distance(t.Value, light);

                        if (distance <= p.lightRange)
                        {
                            p.lightAttraction += light - t.Value;
                            p.lightAttractors++;
                        }
                    }
                }
            }).ScheduleParallel();

        pedestrians.Dispose(Dependency);
        pedestrianRot.Dispose(Dependency);
        //officers.Dispose(Dependency);
        lightTranslation.Dispose(Dependency);
        
        // Calculate obstacle avoidance
        JobHandle obstacle = new ObjectAvoidanceJob
        {
            collisionWorld = physWorld.PhysicsWorld.CollisionWorld
        }.ScheduleParallel();

        // Calculate final values for normal agents and move them
        Entities
            .WithNone<Police>()
            .ForEach((Entity e, int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Pedestrian p, in Goal g) =>
            {
                float3 target = p.target, attraction = p.attraction, repulsion = p.repulsion, obstacle = p.obstacle, lightAttraction = p.lightAttraction;

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
                        velocity.Linear = math.forward(rot.Value) * p.speed *0.5f;
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

                p.heading = rot.Value;
            }).ScheduleParallel();

        end.AddJobHandleForProducer(Dependency);
    }
}
