using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Entities.UniversalDelegates;

// A partial class containing a variant of the ObjectAvoidanceJob for young pedestrians
[UpdateAfter(typeof(CrowdMovementSystem))]
public partial class PedestrianMovementSystem : SystemBase
{
    private partial struct YoungObjectAvoidanceJob : IJobEntity
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
                    CollidesWith = 1 << 1,
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
                    CollidesWith = 1 << 1,
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

        public void Execute(ref Pedestrian p, ref Translation t, in Rotation r, in ObstacleAvoidance o, in YoungTag y)
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
                t.Value += math.forward(r.Value) * 0.6f;
            }
            else if (!hasWallBelow(t))
            {
                p.isClimbing = false;
            }
        }
    }
}
