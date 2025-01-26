using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class TerrainSensor : MonoBehaviour
{

    [SerializeField]
    GameObject[] sensorLocations;

    float[] m_SensorValues;

    // public IEnumerable<float> SenseBatched()
    // {
    //     var results = new NativeArray<RaycastHit>(sensorLocations.Length, Allocator.TempJob);
    //     var commands = new NativeArray<RaycastCommand>(sensorLocations.Length, Allocator.TempJob);
    //
    //     for (var i = 0; i < sensorLocations.Length; i++)
    //     {
    //         commands[i] = new RaycastCommand(sensorLocations[i].transform.position, Vector3.down, QueryParameters.Default);
    //     }
    //
    //     var handle = RaycastCommand.ScheduleBatch(commands, results, 1, 1);
    //     
    //     handle.Complete();
    //
    //     var hits = new float[sensorLocations.Length];
    //     for (var i = 0; i < results.Length; i++)
    //     {
    //         if (results[i].collider != null)
    //         {
    //             if (!results[i].collider.CompareTag("Asphalt"))
    //                 hits[i] = 1f / sensorLocations.Length;
    //         }
    //     }
    //     
    //     results.Dispose();
    //     commands.Dispose();
    //     
    //     return hits;
    //     
    // }

    public IEnumerable<float> Sense()
    {
        var hits = new float[sensorLocations.Length];
        
        for (var i = 0; i < sensorLocations.Length; i++)
        {
            var hit = Physics.Raycast(sensorLocations[i].transform.position, Vector3.down, out var raycastHit);
            if (hit)
            {
                if (!raycastHit.collider.CompareTag("Asphalt"))
                    hits[i] = 1f / sensorLocations.Length;
            }
        }
        
        m_SensorValues = hits;

        return hits;
    }

    void OnDrawGizmos()
    {
        if (m_SensorValues != null)
        {
            for (int i = 0; i < m_SensorValues.Length; i++)
            {
                if (m_SensorValues[i] != 0f)
                {
                    var oldColor = Gizmos.color;
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(sensorLocations[i].transform.position, 0.1f);
                    Gizmos.color = oldColor;
                }
            }
        }
    }
}
