using System;
using System.Collections;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using VehiclePhysics;
using VehiclePhysics.InputManagement;
using Perrinn424.AutopilotSystem;

public class Perrinn424Agent : Agent
{
    public LookAheadSensor lookAheadSensor;
    public TerrainSensor terrainSensor;
    public StateEstimator stateEstimator;
    public event Action wallHit;

    Vector3 m_InitialPosition;
    Quaternion m_InitialRotation;
    Transform m_InitialTransform;
    Vector3 m_CurrentVelocity;
    Vector3 m_CurrentAcceleration;
    float m_CumulativeTimeOffCourse;
    Rigidbody m_Rigidbody;
    VehicleBase m_VehicleBase;
    Steering.Settings m_SteeringSettings;
    AgentPilot m_AgentPilot;
    
    public Vector3 Velocity => m_CurrentVelocity;
    
    void FixedUpdate()
    {
        m_CurrentVelocity = stateEstimator.velocity;
        m_CurrentAcceleration = stateEstimator.acceleration;
        CheckVertical();
        if (CheckOffCourse())
        {
            m_CumulativeTimeOffCourse += Time.fixedDeltaTime;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            wallHit?.Invoke();
        }
    }

    public override void Initialize()
    {
        var rootTransform = transform;
        m_InitialTransform = rootTransform;
        m_InitialPosition = rootTransform.position;
        m_InitialRotation = rootTransform.rotation;
        m_Rigidbody = GetComponent<Rigidbody>();
        m_AgentPilot = GetComponentInChildren<AgentPilot>();
        m_VehicleBase = GetComponentInChildren<VehicleBase>();
        m_SteeringSettings = m_VehicleBase.GetInternalObject(typeof(Steering.Settings)) as Steering.Settings;
    }
    
    public override void OnEpisodeBegin()
    {
        ResetVariables();
        ResetSensors();
        ResetControlInputs();
        StartCoroutine(ResetPhysicsAndPose());
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        lookAheadSensor.Sense();
        var terrainSense = terrainSensor.Sense().Sum();
        
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        float physicalWheelRange = InputManager.instance.settings.physicalWheelRange;
        var steeringInput = 360f * Mathf.Clamp(actionBuffers.ContinuousActions[0] / m_SteeringSettings.steeringWheelRange * physicalWheelRange, -1.0f, 1.0f);
        var throttleInput = 100f * Mathf.Clamp01(actionBuffers.ContinuousActions[1]);
        var brakeInput = 100f * Mathf.Clamp01(-actionBuffers.ContinuousActions[1]);
        var agentActionSample = new Sample();
        if (Academy.Instance.AutomaticSteppingEnabled)
        {
            agentActionSample = new Sample()
            {
                steeringAngle = steeringInput, 
                throttle = throttleInput, 
                brake = brakeInput,
                drsPosition = 0,
                gear = 1,
            };
        }
        
        
        m_AgentPilot.currentAgentSample = agentActionSample;
        
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (Academy.Instance.AutomaticSteppingEnabled)
        {
            var continuousActionsOut = actionsOut.ContinuousActions;
            continuousActionsOut[0] = Input.GetAxis("Horizontal");
            continuousActionsOut[1] = Input.GetAxis("Vertical");
        }
    }

    bool CheckOffCourse()
    {
        var terrainSense = terrainSensor.Sense().Sum();
        return terrainSense >= 0.75f;
    }

    void CheckVertical()
    {
        var agentUp = transform.up;
        var upAngle = Vector3.Angle(Vector3.up, agentUp);
        if (upAngle > 45)
        {
            AddReward(-10f);
            EndEpisode();
        }
    }

    void ResetVariables()
    {
        m_CumulativeTimeOffCourse = 0f;
        m_CurrentVelocity = Vector3.zero;
        m_CurrentAcceleration = Vector3.zero;
    }

    void ResetSensors()
    { 
        stateEstimator.Reset();
    }

    void ResetControlInputs()
    {
        m_AgentPilot.currentAgentSample = new Sample()
        {
            steeringAngle = 0f, 
            throttle = 0f, 
            brake = 0f,
            drsPosition = 0,
            gear = 0,
        };
    }

    IEnumerator ResetPhysicsAndPose()
    {
        Academy.Instance.AutomaticSteppingEnabled = false;
        m_VehicleBase.enabled = false;
        m_Rigidbody.isKinematic = false;
        yield return new WaitForSeconds(0.25f);
        m_Rigidbody.velocity = Vector3.zero;
        m_Rigidbody.angularVelocity = Vector3.zero;
        yield return new WaitForSeconds(0.25f);
        m_Rigidbody.isKinematic = true;
        yield return new WaitForSeconds(0.25f);
        m_Rigidbody.Move(m_InitialPosition + new Vector3(0f, 0.25f, 0f), m_InitialRotation);
        yield return new WaitForSeconds(0.25f);
        m_Rigidbody.isKinematic = false;
        yield return new WaitForSeconds(0.25f);
        m_Rigidbody.velocity = Vector3.zero;
        m_Rigidbody.angularVelocity = Vector3.zero;
        Academy.Instance.AutomaticSteppingEnabled = true;
        m_VehicleBase.enabled = true;
    }
}
