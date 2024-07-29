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

    // stuff for averaging
    private List<float> resultsList;

    void Start()
    {
        /*for (int i = 0; i < 10; i++)
        {
            Instantiate(wall, new Vector3(Random.Range(0, spawnBounds.x), 0, Random.Range(0, spawnBounds.y))+transform.position, Quaternion.Euler(0, Random.Range(0, 360), 0));
        }*/

        //t0 = 0f;

        resultsList = new List<float>(32);

        RequestDecision();

        //World.DefaultGameObjectInjectionWorld.GetExistingSystem<GraphConnectionSystem>().onDemand = true;
    }

    private void Update()
    {
        var pms = World.DefaultGameObjectInjectionWorld.GetExistingSystem<PedestrianMovementSystem>();
        //World.DefaultGameObjectInjectionWorld.GetExistingSystem<GraphConnectionSystem>().onDemand = true;

        //Debug.Log(pms.elapsedTime);

        if (pms.elapsedTime >= 60f)// || (pms.rewards.IsCreated && pms.rewards.Length >= 600))
        {
            float totalReward = 0f;

            foreach (var reward in pms.rewards)
            {
                totalReward += reward;
            }

            Debug.Log($"Total reward: {totalReward}, elapsed time: {pms.elapsedTime}");

            SetReward(totalReward);

            // stuff for averaging
            resultsList.Add(totalReward);

            if (resultsList.Count >= 32)
            {
                Debug.Log($"32 RUN AVERAGE: {resultsList.Average()}");
                Debug.Break();
            }

            pms.rewards.Clear();
            //pms.elapsedTime = 0f;

            EndEpisode();
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        /*for (int i = 0; i < actions.DiscreteActions[0] + 1; i++)
        {
            var newWall = Instantiate(wall, 
                new Vector3(spawnBounds.x * Mathf.Abs(actions.ContinuousActions[i]), 0, spawnBounds.y * Mathf.Abs(actions.ContinuousActions[i+1])) + transform.position, 
                Quaternion.Euler(0, 360 * Mathf.Abs(actions.ContinuousActions[i+2]), 0));
            newWall.transform.localScale = new Vector3(newWall.transform.localScale.x, newWall.transform.localScale.y, 
                newWall.transform.localScale.z*Mathf.Clamp(Mathf.Abs(actions.ContinuousActions[i + 3]), 0.1f, 1f));

            Debug.Log($"# of walls: {actions.DiscreteActions[0]}\n" +
                $"x: {spawnBounds.x * Mathf.Abs(actions.ContinuousActions[i])}\n" +
                $"y: {spawnBounds.y * Mathf.Abs(actions.ContinuousActions[i + 1])}\n" +
                $"rotation: {360 * Mathf.Abs(actions.ContinuousActions[i + 2])}\n" +
                $"size: {newWall.transform.localScale.z * Mathf.Clamp(Mathf.Abs(actions.ContinuousActions[i + 3]), 0.1f, 1f)}");
        }*/

        /*var newWall = Instantiate(wall,
                new Vector3(spawnBounds.x * Mathf.Abs(actions.ContinuousActions[0]), 0, spawnBounds.y * Mathf.Abs(actions.ContinuousActions[1])) + transform.position,
                Quaternion.Euler(0, 360 * Mathf.Abs(actions.ContinuousActions[2]), 0));
        newWall.transform.localScale = new Vector3(newWall.transform.localScale.x, newWall.transform.localScale.y,
            newWall.transform.localScale.z * Mathf.Clamp(Mathf.Abs(actions.ContinuousActions[3]), 0.1f, 1f));*/

        /*Debug.Log($"x: {spawnBounds.x * Mathf.Abs(actions.ContinuousActions[0])}\n" +
            $"y: {spawnBounds.y * Mathf.Abs(actions.ContinuousActions[1])}\n" +
            $"rotation: {360 * Mathf.Abs(actions.ContinuousActions[2])}\n" +
            $"size: {newWall.transform.localScale.z * Mathf.Clamp(Mathf.Abs(actions.ContinuousActions[3]), 0.1f, 1f)}");*/

        // SCALE TRAINING
        /*var newWall = Instantiate(wall,
                new Vector3(spawnBounds.x * 0.5f, 0, spawnBounds.y * 0.5f) + transform.position,
                Quaternion.Euler(0, 360 * 0, 0));
        newWall.transform.localScale = new Vector3(newWall.transform.localScale.x, newWall.transform.localScale.y,
            newWall.transform.localScale.z * Mathf.Clamp(Mathf.Abs(actions.ContinuousActions[0]), 0.1f, 1f));

        Debug.Log("Scale: " + (newWall.transform.localScale.z * Mathf.Clamp(Mathf.Abs(actions.ContinuousActions[0]), 0.1f, 1f)));*/

        // ROTATION TRAINING
        var newWall = Instantiate(wall,
                new Vector3(spawnBounds.x * 0.5f, 0, spawnBounds.y * 0.5f) + transform.position,
                Quaternion.Euler(0, 360 * Mathf.Abs(actions.ContinuousActions[0]), 0));
        newWall.transform.localScale = new Vector3(newWall.transform.localScale.x, newWall.transform.localScale.y, 57.74611f);

        Debug.Log("Rotation: " + (360 * Mathf.Abs(actions.ContinuousActions[0])));
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        //var discActions = actionsOut.DiscreteActions;
        var contActions = actionsOut.ContinuousActions;

        //discActions[0] = Random.Range(0, 10);
        //discActions[0] = 0;

        /*for (int i = 0; i < discActions[0] + 1; i += 3)
        {
            contActions[i] = Random.Range(0f, 1f);
            contActions[i+1] = Random.Range(0f, 1f);
            contActions[i+2] = Random.Range(0f, 1f);
            contActions[i+3] = Random.Range(0f, 1f);
        }*/

        contActions[0] = Random.Range(0f, 1f);
        //contActions[1] = Random.Range(0f, 1f);
        //contActions[2] = Random.Range(0f, 1f);
        //contActions[3] = Random.Range(0f, 1f);

        //Debug.Log(contActions[3]);
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
        RequestDecision();
        //Debug.Log("Episode begins");

        World.DefaultGameObjectInjectionWorld.GetExistingSystem<GraphConnectionSystem>().onDemand = true;
    }
}
