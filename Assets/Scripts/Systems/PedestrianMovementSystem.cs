using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Jobs;
using Unity.Entities.UniversalDelegates;

// System for moving a peaceful crowd
[UpdateAfter(typeof(CrowdMovementSystem))]
public partial class PedestrianMovementSystem : SystemBase
{
    private EndFixedStepSimulationEntityCommandBufferSystem end;
    private EntityQuery pedestrianQuery, policeQuery;
    private Unity.Physics.Systems.BuildPhysicsWorld physWorld;

    // Set up the physics world for raycasting and the entity command buffer system
    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        physWorld = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
    }

    /*private partial struct ObjectAvoidanceJob : IJobEntity
    {
        public Unity.Physics.Systems.BuildPhysicsWorld physicsWorld;
        public Unity.Physics.CollisionWorld collisionWorld;

        public void Excecute(Entity entity, int entityInQueryIndex, ref Pedestrian p, in Translation t, in Rotation r, in ObstacleAvoidance o)
        {
            float3 origin = t.Value;
            float3 direction = new float3(0, 0, 1); // this value would change depending on what direction is 'forward' for the agent
            direction = math.mul(r.Value, direction);

            uint collideBitmask = 1 << 1; // To collide with Buildings (layer 1, so bitshift once)

            quaternion leftmostRotation = quaternion.RotateY(math.radians(-o.visionAngle / 2));
            quaternion angleBetweenRays;// = quaternion.RotateY(math.radians(visionAngle / raysPerAgent));

            float3 leftmostRay = math.mul(leftmostRotation, direction);

            float3 resultingMovement = float3.zero;
            bool hitOccurred = false;


            float3 firstNoHitVector = float3.zero; // Should make this backwards
            bool foundFirstNoHitVector = false;

            int maxNoHitRayNum = -1;
            int minNoHitRayNum = -1;

            int multiplier = -1;
            //if have left tendency, multiply count by -1
            int rayNumber = (o.numberOfRays - 1) / 2;
            int midRayNumber = rayNumber;
            //string resultString = "";

            for (int i = 0; i < o.numberOfRays; i++, multiplier *= -1)
            {
                float angle = rayNumber * math.radians(o.visionAngle / (o.numberOfRays - 1));

                //Debug.Log(string.Format("Angle for ray {0}: {1}",
                //   i + 1, angle));
                angleBetweenRays = quaternion.RotateY(angle);
                //Debug.Log(string.Format("Ray {0}'s angle = {1}",
                //    i, angleBetweenRays));
                direction = math.mul(angleBetweenRays, leftmostRay);
                //Debug.Log(string.Format("Ray {0}'s direction = {1}",
                //    i + 1, direction));
                RaycastInput input = new RaycastInput()
                {
                    Start = origin,
                    End = (origin + direction * o.visionLength),
                    Filter = new CollisionFilter()
                    {
                        BelongsTo = ~0u,
                        CollidesWith = collideBitmask,//~0u, // all 1s, so all layers, collide with everything
                        GroupIndex = 0
                    }
                };

                Unity.Physics.RaycastHit hit = new Unity.Physics.RaycastHit();
                //Debug.Log(string.Format("Firing ray number {0}",
                //    i + 1));
                bool haveHit = collisionWorld.CastRay(input, out hit);
                if (haveHit)
                {
                    //Debug.Log("Ray number " + (rayNumber + 1) + " hit something");
                    //Debug.Log("Ray number " + (i+1) + " hit something. How far along: " + hit.Fraction);

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

                //Debug.Log("Max ray:" + maxNoHitRayNum + ", Min ray: " + minNoHitRayNum);
                // Calculate the next ray number
                rayNumber += multiplier * i;
                //resultString += rayNumber + " ";
            }
            //Debug.Log(resultString);


            if (hitOccurred) // if there was at least one hit
            {
                //Debug.Log("Some hit occurred!");
                //if (math.distance(resultingMovement, float3.zero) > 0.1f)// if the resulting movement is over some threshold
                //{
                //    Debug.Log("AND WE CHANGED THE OBSTACLE AVOIDANCE");
                //    movementValues.obstacleAvoidanceMovement = resultingMovement;
                //}

                p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;
            }
            else
            {
                p.obstacle = float3.zero;
            }
        }
    }*/

    protected override void OnUpdate()
    {
        policeQuery = GetEntityQuery(typeof(Police));
        var dt = Time.DeltaTime;
        var collisionWorld = physWorld.PhysicsWorld.CollisionWorld;
        int pedestrianCount = pedestrianQuery.CalculateEntityCount();

        NativeArray<float3> pedestrians = new NativeArray<float3>(pedestrianCount, Allocator.TempJob);
        NativeArray<quaternion> pedestrianRot = new NativeArray<quaternion>(pedestrianCount, Allocator.TempJob);
        //NativeArray<Entity> officers = policeQuery.ToEntityArray(Allocator.TempJob);

        // Get all the agents
        Entities
            .WithStoreEntityQueryInField(ref pedestrianQuery)
            .WithAll<Pedestrian>().ForEach((int entityInQueryIndex, in Translation t, in Rotation r) =>
            {
                pedestrians[entityInQueryIndex] = t.Value;
                pedestrianRot[entityInQueryIndex] = r.Value;
            }).Schedule();

        // Do the collision avoidance
        /*Entities.WithReadOnly(collisionWorld).ForEach((int entityInQueryIndex, ref Agent a, in Translation t, in Rotation r) =>
        {
            float3 from = t.Value, to = t.Value + math.forward(a.heading) * a.maxDist;

            RaycastInput input = new RaycastInput()
            {
                Start = from,
                End = to,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1 << 0,
                    CollidesWith = 1 << 1,
                }
            };

            RaycastHit hit = new RaycastHit();
            bool haveHit = collisionWorld.CastRay(input, out hit);

            if (haveHit)
            {
                float3 target = math.normalize(math.reflect(hit.Position - from, math.normalize(hit.SurfaceNormal)));
                v.obstacle = math.isnan(target.x) ? from - to : target;
            }
            else
            {
                v.obstacle = math.INFINITY;
            }
        }).ScheduleParallel();*/

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
                            if (dist < p.minDist)
                            {
                                p.repulsion += (t.Value - target) / dist;
                                p.repellors++;
                            }

                            else if (angle < math.radians((float)p.attractionRot) && angle != 0)
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

        pedestrians.Dispose(Dependency);
        pedestrianRot.Dispose(Dependency);
        //officers.Dispose(Dependency);

        // Calculate obstacle avoidance
        /*JobHandle obstacle = new ObstacleAvoidanceJob
        {
            physicsWorld = physWorld,
            collisionWorld = physWorld.PhysicsWorld.CollisionWorld
        }.Schedule();*/

        Entities
            .ForEach((Entity entity, int entityInQueryIndex, ref Pedestrian p, in Translation t, in Rotation r, in ObstacleAvoidance o) =>
            {
                float3 origin = t.Value;
                float3 direction = new float3(0, 0, 1); // this value would change depending on what direction is 'forward' for the agent
                direction = math.mul(r.Value, direction);

                //Debug.DrawRay(origin + math.forward(r.Value), direction, Color.red);

                //quaternion leftmostRotation = quaternion.RotateY(math.radians(-o.visionAngle / 2));
                quaternion leftmostRotation = quaternion.RotateY(math.radians(-o.visionAngle / 2));
                quaternion angleBetweenRays;// = quaternion.RotateY(math.radians(visionAngle / raysPerAgent));

                float3 leftmostRay = math.mul(leftmostRotation, direction);

                float3 resultingMovement = float3.zero;
                bool hitOccurred = false;

                float3 firstNoHitVector = float3.zero; // Should make this backwards
                bool foundFirstNoHitVector = false;

                int maxNoHitRayNum = -1;
                int minNoHitRayNum = -1;

                int multiplier = -1;
                //if have left tendency, multiply count by -1
                int rayNumber = (o.numberOfRays - 1) / 2;
                int midRayNumber = rayNumber;
                //string resultString = "";

                for (int i = 0; i < o.numberOfRays; i++, multiplier *= -1)
                {
                    float angle = rayNumber * math.radians(o.visionAngle / (o.numberOfRays - 1));

                    //Debug.Log(string.Format("Angle for ray {0}: {1}", i + 1, angle));
                    angleBetweenRays = quaternion.RotateY(angle);
                   // Debug.Log(string.Format("Ray {0}'s angle = {1}", i, angleBetweenRays));
                    direction = math.mul(angleBetweenRays, leftmostRay);
                    //Debug.Log(string.Format("Ray {0}'s direction = {1}", i + 1, direction));
                    RaycastInput input = new RaycastInput()
                    {
                        Start = origin + (math.forward(r.Value)/2),
                        End = origin + (math.forward(r.Value) / 2) + (direction * o.visionLength),
                        Filter = new CollisionFilter()
                        {
                            BelongsTo = 1 << 0,
                            CollidesWith = 1 << 1,
                        }
                    };

                    //Debug.DrawRay(input.Start, leftmostRay, Color.yellow);
                    Debug.DrawLine(input.Start, input.End);

                    Unity.Physics.RaycastHit hit = new Unity.Physics.RaycastHit();
                    //Debug.Log(string.Format("Firing ray number {0}",
                    //    i + 1));
                    bool haveHit = collisionWorld.CastRay(input, out hit);
                    if (haveHit)
                    {
                        Debug.Log("yay we hit something");
                        Debug.Log("Hit data: " + hit.ToString());

                        Debug.DrawRay(hit.Position, math.forward(), Color.green);

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

                    //Debug.Log("Max ray:" + maxNoHitRayNum + ", Min ray: " + minNoHitRayNum);
                    // Calculate the next ray number
                    rayNumber += multiplier * i;
                    //resultString += rayNumber + " ";
                }
                //Debug.Log(resultString);


                if (hitOccurred) // if there was at least one hit
                {
                    //Debug.Log("Some hit occurred!");
                    //if (math.distance(resultingMovement, float3.zero) > 0.1f)// if the resulting movement is over some threshold
                    //{
                    //    Debug.Log("AND WE CHANGED THE OBSTACLE AVOIDANCE");
                    //    movementValues.obstacleAvoidanceMovement = resultingMovement;
                    //}

                    p.obstacle = new float3(resultingMovement.x, 0, resultingMovement.z);//firstNoHitVector;

                    //Debug.Log("Something hit!");
                    //Debug.Log("Movement: " + resultingMovement);

                    if (minNoHitRayNum == -1 && maxNoHitRayNum == -1)
                    {
                        Debug.Log("all rays hit");
                        //p.obstacle = math.mul(quaternion.RotateY(180), math.forward(r.Value));

                        /*float3 from = t.Value, to = t.Value + math.forward(r.Value) * o.visionLength;

                        RaycastInput input = new RaycastInput()
                        {
                            Start = from,
                            End = to,
                            Filter = new CollisionFilter
                            {
                                BelongsTo = 1 << 0,
                                CollidesWith = 1 << 1,
                            }
                        };

                        Unity.Physics.RaycastHit hit = new Unity.Physics.RaycastHit();
                        bool haveHit = collisionWorld.CastRay(input, out hit);

                        if (haveHit)
                        {
                            float3 target = math.normalize(math.reflect(hit.Position - from, math.normalize(hit.SurfaceNormal)));
                            p.obstacle = math.isnan(target.x) ? from - to : target;
                        }
                        else
                        {
                            p.obstacle = float3.zero;
                        }*/
                    }
                }
                else
                {
                    p.obstacle = float3.zero;
                }
            }).WithoutBurst().Run();

        

        // Calculate final values and move the agents
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        // Operate on moving and rioting entities
        Entities
            .WithNone<Police>()
            .ForEach((Entity e, int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Pedestrian p, in Goal g) =>
            {
                float3 target = p.target, attraction = p.attraction, repulsion = p.repulsion, obstacle = p.obstacle;

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

                target = target.x == 0 && target.y == 0 && target.z == 0 ? target : math.normalize(target);

                obstacle = obstacle.x == 0 && obstacle.y == 0 && obstacle.z == 0 ? obstacle : math.normalize(obstacle);

                float3 final = ((target * p.targetFac) +
                (attraction * p.attractionFac) +
                (repulsion * p.repulsionFac) + (obstacle * p.obstacleFac)) / 4;

                bool isZero = final.x == 0 && final.y == 0 && final.z == 0;

                final = isZero ? final : math.normalize(final);

                t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
                rot.Value.value.x = 0;
                rot.Value.value.z = 0;
                velocity.Angular = 0;

                if (!isZero)
                {
                    rot.Value = math.slerp(rot.Value, quaternion.LookRotation(final, math.up()), dt * p.rotSpeed);
                    velocity.Linear = math.forward(rot.Value) * p.speed;
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
