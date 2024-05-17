using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.Burst;
using Unity.Jobs;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Scenes;
using Unity.Collections;
using System.Linq;



public class WallPlacer : Agent
{
    public GameObject wall; // the wall that will be spawned

    public GameObject goal;
    public GameObject spawn;

    public Vector2 spawnBounds;

    float t0; // the previous time the thing started at

    void Start()
    {
        /*for (int i = 0; i < 10; i++)
        {
            Instantiate(wall, new Vector3(Random.Range(0, spawnBounds.x), 0, Random.Range(0, spawnBounds.y))+transform.position, Quaternion.Euler(0, Random.Range(0, 360), 0));
        }*/

        t0 = 0f;

        RequestDecision();
    }

    private void Update()
    {
        var pms = World.DefaultGameObjectInjectionWorld.GetExistingSystem<PedestrianMovementSystem>();

        //Debug.Log(Time.timeSinceLevelLoad);

        if (Time.timeSinceLevelLoad - t0 >= 60 || (pms.rewards.IsCreated && pms.rewards.Length >= 100))
        {
            float totalReward = 0;

            foreach (var reward in pms.rewards)
            {
                totalReward += reward;
            }

            Debug.Log($"Total reward: {totalReward}");

            SetReward(totalReward);

            pms.rewards.Clear();

            EndEpisode();
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        /*Debug.Log(actions.DiscreteActions[0]);
        Debug.Log(actions.ContinuousActions[0]);
        Debug.Log(actions.ContinuousActions[1]);
        Debug.Log(actions.ContinuousActions[2]);*/

        for (int i = 0; i < actions.DiscreteActions[0]; i++)
        {
            //Debug.Log(actions.ContinuousActions[i]);
            Instantiate(wall, new Vector3(spawnBounds.x * Mathf.Abs(actions.ContinuousActions[i]), 0, spawnBounds.y * Mathf.Abs(actions.ContinuousActions[i+1])) + transform.position, Quaternion.Euler(0, 360 * Mathf.Abs(actions.ContinuousActions[i+2]), 0));
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discActions = actionsOut.DiscreteActions;
        var contActions = actionsOut.ContinuousActions;

        discActions[0] = Random.Range(0, 10);

        for (int i = 0; i < discActions[0]; i += 3)
        {
            contActions[i] = Random.Range(0f, 1f);
            contActions[i+1] = Random.Range(0f, 1f);
            contActions[i+2] = Random.Range(0f, 1f);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //sensor.AddObservation(spawn.transform.position);
        //sensor.AddObservation(goal.transform.position);
        sensor.AddObservation(spawnBounds);
    }

    public override void OnEpisodeBegin()
    {
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<SpawningSystem>().finished = true;
        t0 = Time.timeSinceLevelLoad;
        RequestDecision();
        //Debug.Log("Episode begins");
    }
}
