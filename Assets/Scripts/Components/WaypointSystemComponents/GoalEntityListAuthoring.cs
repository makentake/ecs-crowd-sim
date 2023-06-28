using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[InternalBufferCapacity(2)]
public struct GoalEntityList : IBufferElementData
{
    public Entity waypoint;
}

public class GoalEntityListAuthoring : MonoBehaviour
{
    public List<GameObject> nodes;
}

public class GoalEntityListConversion : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((GoalEntityListAuthoring input) =>
        {
            var spawner = GetPrimaryEntity(input);
            var buffer = DstEntityManager.AddBuffer<GoalEntityList>(spawner);

            foreach (GameObject node in input.nodes)
            {
                buffer.Add(new GoalEntityList
                {
                    waypoint = GetPrimaryEntity(node)
                });
            }
        });
    }
}