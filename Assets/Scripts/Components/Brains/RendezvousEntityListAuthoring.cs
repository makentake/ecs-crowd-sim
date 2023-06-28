using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[InternalBufferCapacity(1)]
public struct RendezvousEntityList : IBufferElementData
{
    public Entity point;
}

public class RendezvousEntityListAuthoring : MonoBehaviour
{
    public List<GameObject> pointGameObjects;
}

public class RendezvousEntityListConversion : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((RendezvousEntityListAuthoring input) =>
        {
            var spawner = GetPrimaryEntity(input);
            var buffer = DstEntityManager.AddBuffer<RendezvousEntityList>(spawner);

            foreach (GameObject pointGameObject in input.pointGameObjects)
            {
                buffer.Add(new RendezvousEntityList
                {
                    point = GetPrimaryEntity(pointGameObject)
                });
            }
        });
    }
}