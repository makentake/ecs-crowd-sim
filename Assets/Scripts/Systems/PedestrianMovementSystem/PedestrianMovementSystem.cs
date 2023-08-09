using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Jobs;
using Unity.Burst;

// System for moving a peaceful crowd
[UpdateAfter(typeof(CrowdMovementSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class PedestrianMovementSystem : SystemBase
{
    private EndFixedStepSimulationEntityCommandBufferSystem end;
    private EntityQuery pedestrianQuery, lightQuery, waypointQuery;
    private Unity.Physics.Systems.BuildPhysicsWorld physicsWorld;

    // Set up the physics world for raycasting and the entity command buffer system
    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        physicsWorld = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
    }

    [BurstCompile]
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
            bool hasHit;

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
            hasHit = collisionWorld.CastRay(input, out hit);

            distance = from.x == hit.Position.x && from.y == hit.Position.y && from.z == hit.Position.z ? 0 : math.distance(from, hit.Position);

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
                Unity.Physics.RaycastHit hit = new Unity.Physics.RaycastHit();
                bool haveHit = collisionWorld.CastRay(input, out hit);

                //RaycastVisualization(i, input);

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

    [BurstCompile]
    [WithNone(typeof(FleeingTag))]
    private partial struct LocalAgentCalculationJob : IJobEntity
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

            p.target = g.goal.Value - t.Value;

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

    [BurstCompile]
    [WithNone(typeof(FleeingTag))]
    private partial struct FinalVectorCalculationJob : IJobEntity
    {
        public float deltaTime;
        public EntityCommandBuffer.ParallelWriter ecbpw;

        public void Execute(Entity e, [EntityInQueryIndex] int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Pedestrian p, in Goal g)
        {
            float3 target = p.target, attraction = p.attraction, repulsion = p.repulsion, obstacle = p.obstacle, lightAttraction = p.lightAttraction;
            float dist = math.distance(t.Value, g.goal.Value);
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

            if (dist < p.tolerance*3)
            {
                ecbpw.AddComponent<FleeingTag>(entityInQueryIndex, e);
            }
        }
    }

    protected override void OnUpdate()
    {
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        var dt = Time.DeltaTime;
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

        JobHandle core = new LocalAgentCalculationJob
        {
            pedestrianArray = pedestrians,
            pedestrianRotArray = pedestrianRot,
            pedestrianSpeedArray = pedestrianSpeed
        }.ScheduleParallel();

        JobHandle fleeingCore = new FleeingLocalAgentCalculationJob
        {
            pedestrianArray = pedestrians,
            pedestrianRotArray = pedestrianRot,
            pedestrianSpeedArray = pedestrianSpeed
        }.ScheduleParallel();

        JobHandle waypointCore = new WaypointLocalAgentCalculationJob
        {
            pedestrianArray = pedestrians,
            pedestrianRotArray = pedestrianRot,
            pedestrianSpeedArray = pedestrianSpeed,
            waypointArray = waypoints//,
            //ecbpw = ecb
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
            ecbpw = ecb
        }.ScheduleParallel();
        
        // Calculate obstacle avoidance
        JobHandle obstacle = new ObjectAvoidanceJob
        {
            collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld
        }.ScheduleParallel();

        // this job is in another partial class
        JobHandle youngObstacle = new YoungObjectAvoidanceJob
        {
            collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld
        }.ScheduleParallel();

        JobHandle waypointObstacle = new WaypointObstacleAvoidanceJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            ecbpw = ecb
        }.ScheduleParallel();

        // Calculate final values for leaving agents and move them
        JobHandle fleeingFinal = new FleeingFinalVectorCalculationJob
        {
            deltaTime = dt,
            ecbpw = ecb
        }.ScheduleParallel();

        // Calculate final values for normal agents and move them
        JobHandle final = new FinalVectorCalculationJob
        {
            deltaTime = dt,
            ecbpw = ecb
        }.ScheduleParallel();

        JobHandle waypointFinal = new WaypointFinalVectorCalculationJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            deltaTime = dt,
            ecbpw = ecb
        }.ScheduleParallel();

        JobHandle goalAdvancement = new WaypointGoalAdvancementJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            deltaTime = dt,
            ecbpw = ecb
        }.ScheduleParallel();

        JobHandle rendezvousGoalAdvancement = new WaypointRendezvousGoalAdvancementJob
        {
            collisionWorld = collisionWorld,
            waypointArray = waypoints,
            deltaTime = dt,
            ecbpw = ecb
        }.ScheduleParallel();

        end.AddJobHandleForProducer(Dependency);

        pedestrians.Dispose(Dependency);
        pedestrianRot.Dispose(Dependency);
        pedestrianSpeed.Dispose(Dependency);
        lightTranslation.Dispose(Dependency);
        waypoints.Dispose(Dependency);
    }
}
