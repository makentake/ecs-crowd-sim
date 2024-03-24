using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class WallPlacer : Agent
{
    public GameObject wall; // the wall that will be spawned

    public GameObject goal;
    public GameObject spawn;

    public Vector2 spawnBounds;

    void Start()
    {
        for (int i = 0; i < 10; i++)
        {
            Instantiate(wall, new Vector3(Random.Range(0, spawnBounds.x), 0, Random.Range(0, spawnBounds.y))+transform.position, Quaternion.Euler(0, Random.Range(0, 360), 0));
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        for (int i = 0; i < actions.DiscreteActions[0]; i++)
        {
            Instantiate(wall, new Vector3(Random.Range(0, spawnBounds.x), 0, Random.Range(0, spawnBounds.y)), Quaternion.Euler(0, Random.Range(0, 360), 0));
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(spawn.transform.position);
        sensor.AddObservation(goal.transform.position);
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
    }
}
