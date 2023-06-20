using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Physics;

// !!!SHOULD BE SIMPLIFIED, LOOK AT PedestrianMovementSystem FOR REFERENCE!!!

// System for moving a riotous crowd
public partial class CrowdMovementSystem : SystemBase
{
    private EndFixedStepSimulationEntityCommandBufferSystem end;
    private EntityQuery agentQuery, policeQuery;
    private Unity.Physics.Systems.BuildPhysicsWorld physWorld;

    // Set up the physics world for raycasting and the entity command buffer system
    protected override void OnStartRunning()
    {
        end = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        physWorld = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
    }

    protected override void OnUpdate()
    {
        policeQuery = GetEntityQuery(typeof(Police));
        var dt = Time.DeltaTime;
        var collisionWorld = physWorld.PhysicsWorld.CollisionWorld;
        int agentCount = agentQuery.CalculateEntityCount();

        NativeArray<float3> agents = new NativeArray<float3>(agentCount, Allocator.TempJob);
        NativeArray<quaternion> agentsRot = new NativeArray<quaternion>(agentCount, Allocator.TempJob);
        NativeArray<Entity> officers = policeQuery.ToEntityArray(Allocator.TempJob);
        
        // Get all the agents
        Entities
            .WithStoreEntityQueryInField(ref agentQuery)
            .WithAll<Agent>().ForEach((int entityInQueryIndex, in Translation t, in Rotation r) =>
            {
                agents[entityInQueryIndex] = t.Value;
                agentsRot[entityInQueryIndex] = r.Value;
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
            .WithReadOnly(agents)
            .WithReadOnly(agentsRot)
            .WithAll<MovingTag>()
            .WithNone<Police>()
            .ForEach((int entityInQueryIndex, ref Agent a, in Translation t, in Rotation r, in Goal g) =>
            {
                a.attraction = new float3(0, 0, 0);
                a.repulsion = new float3(0, 0, 0);

                a.attractors = 0;
                a.repellors = 0;

                //float3 avoidance = v.obstacle;
                float3 avoidance = math.INFINITY;

                if (avoidance.x != math.INFINITY)
                {
                    a.target = new float3(avoidance.x, 0, avoidance.z);
                }
                else
                {
                    a.target = g.goal.Value - t.Value;

                    for (int i = 0; i < agents.Length; i++)
                    {
                        float3 target = agents[i];
                        float dist = math.distance(t.Value, target);
                        float angle = math.atan2(r.Value.value.y, agentsRot[i].value.y);

                        if (dist >= 0.01)
                        {
                            if (dist < a.minDist)
                            {
                                a.repulsion += (t.Value - target) / dist;
                                a.repellors++;
                            }

                            else if (angle < math.radians(a.attractionRot) && angle != 0)
                            {
                                if (dist <= a.maxDist)
                                {
                                    a.attraction += (target - t.Value) / angle;
                                    a.attractors++;
                                }
                            }
                        }
                    }
                }
            }).ScheduleParallel();

        // Calculate vectors for rioting agents
        Entities
            .WithReadOnly(agents)
            .WithReadOnly(agentsRot)
            .WithAll<RiotingTag>()
            .WithNone<Police>()
            .ForEach((int entityInQueryIndex, ref Agent a, in Translation t, in Rotation r, in Goal g) =>
            {
                a.attraction = new float3(0, 0, 0);
                a.repulsion = new float3(0, 0, 0);
                a.target = new float3(0, 0, 0);

                a.attractors = 0;
                a.repellors = 0;

                //float3 avoidance = v.obstacle;
                float3 avoidance = math.INFINITY;

                if (avoidance.x != math.INFINITY)
                {
                    a.target = new float3(avoidance.x, 0, avoidance.z);
                }
                else
                {
                    for (int i = 0; i < agents.Length; i++)
                    {
                        float3 target = agents[i];
                        float dist = math.distance(t.Value, target);
                        float angle = math.atan2(r.Value.value.y, agentsRot[i].value.y);

                        if (dist >= 0.01)
                        {
                            if (dist < a.minDist)
                            {
                                a.repulsion += (t.Value - target) / dist;
                                a.repellors++;
                            }

                            else if (angle < math.radians(a.attractionRot) && angle != 0)
                            {
                                if (dist <= a.maxDist)
                                {
                                    a.attraction += (target - t.Value) / angle;
                                    a.attractors++;
                                }
                            }
                        }
                    }
                }
            }).ScheduleParallel();

        // Calculate the vector of fleeing agents
        Entities
            .WithReadOnly(agents)
            .WithReadOnly(agentsRot)
            .WithAll<FleeingTag>()
            .WithNone<Police>()
            .ForEach((int entityInQueryIndex, ref Agent a, in Translation t, in Rotation r, in Goal g) =>
            {
                a.attraction = new float3(0, 0, 0);
                a.repulsion = new float3(0, 0, 0);

                a.attractors = 0;
                a.repellors = 0;

                //float3 avoidance = v.obstacle;
                float3 avoidance = math.INFINITY;

                if (avoidance.x != math.INFINITY)
                {
                    a.target = new float3(avoidance.x, 0, avoidance.z);
                }
                else
                {
                    a.target = g.exit.Value - t.Value;

                    for (int i = 0; i < agents.Length; i++)
                    {
                        float3 target = agents[i];
                        float dist = math.distance(t.Value, target);
                        float angle = math.atan2(r.Value.value.y, agentsRot[i].value.y);

                        if (dist >= 0.01)
                        {
                            if (dist < a.minDist)
                            {
                                a.repulsion += (t.Value - target) / dist;
                                a.repellors++;
                            }

                            else if (angle < math.radians(a.attractionRot) && angle != 0)
                            {
                                if (dist <= a.maxDist)
                                {
                                    a.attraction += (target - t.Value) / angle;
                                    a.attractors++;
                                }
                            }
                        }
                    }
                }
            }).ScheduleParallel();

        agents.Dispose(Dependency);
        agentsRot.Dispose(Dependency);
        officers.Dispose(Dependency);

        // Calculate final values and move the agents
        var ecb = end.CreateCommandBuffer().AsParallelWriter();

        // Operate on moving and rioting entities
        Entities
            .WithNone<FleeingTag, Interacting>()
            .WithNone<Police>().ForEach((Entity e, int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Agent a, in Goal g) =>
            {
                float3 target = a.target, attraction = a.attraction, repulsion = a.repulsion;

                if (a.attractors != 0)
                {
                    attraction /= a.attractors;
                    attraction = math.normalize(attraction);
                }

                if (a.repellors != 0)
                {
                    repulsion /= a.repellors;
                    repulsion = math.normalize(repulsion);
                }
    
                target = target.x == 0 && target.y == 0 && target.z == 0 ? target : math.normalize(target);

                float3 final = ((target * a.targetFac) +
                (attraction * a.attractionFac) +
                (repulsion * a.repulsionFac)) / 3;

                bool isZero = final.x == 0 && final.y == 0 && final.z == 0;

                final = isZero ? final : math.normalize(final);

                t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
                rot.Value.value.x = 0;
                rot.Value.value.z = 0;
                velocity.Angular = 0;

                if (!isZero)
                {
                    rot.Value = math.slerp(rot.Value, quaternion.LookRotation(final, math.up()), dt * a.rotSpeed);
                    velocity.Linear = math.forward(rot.Value) * a.speed;
                }
                else
                {
                    velocity.Linear = math.float3(0, 0, 0);
                }

                a.heading = rot.Value;
            }).ScheduleParallel();

        // Operate on interacting entities
        Entities
            .WithAll<Interacting>()
            .ForEach((int entityInQueryIndex, ref PhysicsVelocity v, ref Agent a, ref Translation t, ref Rotation r, in Interacting i, in Goal g) =>
            {
                float3 target = i.position.Value - t.Value,
                repulsion = a.repulsion;
                float dist = math.distance(t.Value, i.position.Value);

                target = target.x == 0 && target.y == 0 && target.z == 0 ? target : math.normalize(target);

                float3 final = ((target * a.targetFac) + (repulsion * a.repulsionFac)) / 2;

                bool isZero = final.x == 0 && final.y == 0 && final.z == 0;

                final = isZero ? final : math.normalize(final);

                t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
                r.Value.value.x = 0;
                r.Value.value.y = 0;
                r.Value.value.z = 0;
                v.Angular = 0;

                if (dist > 0.1)
                {
                    v.Linear = final * a.speed;
                }
                else
                {
                    v.Linear = math.float3(0, 0, 0);
                }
            }).ScheduleParallel();

        // Operate on fleeing entities
        Entities
            .WithAll<FleeingTag>()
            .WithNone<Interacting>()
            .WithNone<Police>().ForEach((Entity e, int entityInQueryIndex, ref PhysicsVelocity velocity, ref Translation t, ref Rotation rot, ref Agent a, in Goal g) =>
            {
                float3 target = a.target, attraction = a.attraction, repulsion = a.repulsion;
                float dist = math.distance(t.Value, g.exit.Value);

                if (a.attractors != 0)
                {
                    attraction /= a.attractors;
                    attraction = math.normalize(attraction);
                }

                if (a.repellors != 0)
                {
                    repulsion /= a.repellors;
                    repulsion = math.normalize(repulsion);
                }

                target = target.x == 0 && target.y == 0 && target.z == 0 ? target : math.normalize(target);

                float3 final = math.normalize(((target * a.targetFac) +
                (attraction * a.attractionFac) +
                (repulsion * a.repulsionFac)) / 3);

                bool isZero = final.x == 0 && final.y == 0 && final.z == 0;

                final = isZero ? final : math.normalize(final);

                t.Value -= math.float3(0, t.Value.y - 1.5f, 0);
                rot.Value.value.x = 0;
                rot.Value.value.z = 0;
                velocity.Angular = 0;

                if (dist > a.baseTolerance)
                {
                    rot.Value = math.slerp(rot.Value, quaternion.LookRotation(final, math.up()), dt * a.rotSpeed);
                    velocity.Linear = math.forward(rot.Value) * a.speed;
                }
                else
                {
                    velocity.Linear = math.float3(0, 0, 0);
                }

                a.heading = rot.Value;

                if (dist < a.tolerance)
                {
                    ecb.DestroyEntity(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

        end.AddJobHandleForProducer(Dependency);
    }
}
