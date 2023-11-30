using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;

public class CityConversionPrep : MonoBehaviour
{
    GameObject[] map;

    // Update is called once per frame
    void LateUpdate()
    {
        map = GameObject.FindGameObjectsWithTag("City");

        foreach (GameObject thing in map)
        {
            // Ensure only one material is being used
            var renderer = thing.GetComponent<MeshRenderer>();

            renderer.materials = new Material[]
            {
                renderer.material
            };

            thing.AddComponent<AwaitingConversionTagAuthoring>();
            thing.AddComponent<WallTagAuthoring>();
            var body = thing.AddComponent<PhysicsBodyAuthoring>();
            var shape = thing.AddComponent<PhysicsShapeAuthoring>();

            body.MotionType = BodyMotionType.Static;

            shape.BelongsTo = new PhysicsCategoryTags { Value = 1 << 1 };
            shape.CollidesWith = new PhysicsCategoryTags { Value = 1 << 0 };
            shape.SetMesh(thing.GetComponent<MeshCollider>().sharedMesh);

            thing.AddComponent<ConvertToEntity>();
        }

        Destroy(gameObject);
    }
}
