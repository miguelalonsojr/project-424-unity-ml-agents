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
    public float steeringRate = 90f;
    public float throttleRate = 100f;
    public float brakeRate = 100f;
    public event Action wallHit;
    public event Action wallStay;
    public bool randomStart = true;

    public int DecisionPeriod { get; private set; }

    public float DeltaProgress => GetDeltaProgress();

    Vector3 m_InitialPosition;
    Quaternion m_InitialRotation;
    Transform m_InitialTransform;
    Vector3 m_CurrentVelocity;
    Vector3 m_CurrentAcceleration;
    Vector3 m_CurrentAngularVelocity;
    float m_CumulativeTimeOffCourse;
    float m_CumulativeWallHitTime;
    Rigidbody m_Rigidbody;
    VehicleBase m_VehicleBase;
    Steering.Settings m_SteeringSettings;
    AgentPilot m_AgentPilot;
    float m_CenterlineAlignment;
    float m_CurrentProgress;
    float m_PreviousProgress;
    Sample m_CurrentActions;
    Sample m_PreviousActions;
    bool m_WallContact = false;
    
    public Vector3 Velocity => m_CurrentVelocity;
    
    public float CumulativeTimeOffCourse => m_CumulativeTimeOffCourse;

    public float CumulativeWallHitTime => m_CumulativeWallHitTime;
    
    public bool isAligned => Mathf.Approximately(Mathf.Sign(m_CenterlineAlignment), 1f);
    
    void FixedUpdate()
    {
        m_CurrentVelocity = stateEstimator.velocity;
        m_CurrentAcceleration = stateEstimator.acceleration;
        m_CurrentAngularVelocity = stateEstimator.angularVelocity;
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
            m_WallContact = true;
            wallHit?.Invoke();
        }
    }
    
    void OnCollisionStay(Collision other)
    {
        if (other.gameObject.CompareTag("Wall"))
        {
            m_WallContact = true;
            m_CumulativeWallHitTime += Time.fixedDeltaTime;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (m_WallContact)
        {
            m_WallContact = false;
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
        DecisionPeriod = GetComponent<DecisionRequester>().DecisionPeriod;
        m_SteeringSettings = m_VehicleBase.GetInternalObject(typeof(Steering.Settings)) as Steering.Settings;
    }
    
    public override void OnEpisodeBegin()
    {
        ResetVariables();
        ResetSensors();
        ResetControlInputs();
        var (newPosition, newRotation) = (m_InitialPosition, m_InitialRotation.eulerAngles);
        if (randomStart)
            ( newPosition,  newRotation) = lookAheadSensor.SampleStartingPosition();
        StartCoroutine(ResetPhysicsAndPose(newPosition, newRotation));
        m_CumulativeTimeOffCourse = 0f;
        m_CumulativeWallHitTime = 0f;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        var (centerLineAngle, centerLineOffset, newLookahead, progress, centerlineProgress, newOuterLookahead, newInnerLookahead) = lookAheadSensor.Sense();
        m_CenterlineAlignment = lookAheadSensor.CenterlineAlignment;
        m_PreviousProgress = m_CurrentProgress;
        m_CurrentProgress = centerlineProgress;
        
        // total observations : 54
        
        // linear velocity vec3 : 3
        sensor.AddObservation(m_CurrentVelocity);
        
        // linear acceleration vec3 : 3
        sensor.AddObservation(m_CurrentAcceleration);
        
        // angular velocity vec3 : 3
        sensor.AddObservation(m_CurrentAngularVelocity);
        
        // centerline angle float : 1
        sensor.AddObservation(centerLineAngle);
        
        // centerline offset float : 1
        sensor.AddObservation(centerLineOffset);
        
        // course progress vec2 : 2
        sensor.AddObservation(progress);
        
        // previous commands float : 3
        sensor.AddObservation(m_CurrentActions.steeringAngle);
        
        sensor.AddObservation(m_CurrentActions.throttle);
        
        sensor.AddObservation(m_CurrentActions.brake);
        
        // wall contact bool : 1
        sensor.AddObservation(m_WallContact);
        
        // look ahead sensor newLookAhead.length : 11
        foreach (var point in newLookahead)
        {
           sensor.AddObservation(transform.InverseTransformPoint(point));
        }
        
        // look ahead sensor newLookAhead.length : 11
        foreach (var point in newOuterLookahead)
        {
            sensor.AddObservation(transform.InverseTransformPoint(point));
        }
        
        // look ahead sensor newLookAhead.length : 11
        foreach (var point in newInnerLookahead)
        {
            sensor.AddObservation(transform.InverseTransformPoint(point));
        }
        
        // pitch
        sensor.AddObservation(stateEstimator.pitch);
        
        // roll
        sensor.AddObservation(stateEstimator.roll);
        
        // off-course check bool : 1
        sensor.AddObservation(CheckOffCourse());
        
        // off-course sense float[] : 4
        sensor.AddObservation(terrainSensor.Sense().ToArray());
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        float physicalWheelRange = InputManager.instance.settings.physicalWheelRange;
        var steeringInput = 360f * Mathf.Clamp(actionBuffers.ContinuousActions[0] / m_SteeringSettings.steeringWheelRange * physicalWheelRange, -1.0f, 1.0f);
        steeringInput = Mathf.MoveTowards(m_CurrentActions.steeringAngle, steeringInput, steeringRate * Time.fixedDeltaTime * DecisionPeriod);
        var throttleInput = 100f * Mathf.Clamp01(actionBuffers.ContinuousActions[1]);
        throttleInput = Mathf.MoveTowards(m_CurrentActions.throttle, throttleInput, throttleRate * Time.fixedDeltaTime * DecisionPeriod);
        var brakeInput = 100f * Mathf.Clamp01(-actionBuffers.ContinuousActions[1]);
       brakeInput = Mathf.MoveTowards(m_CurrentActions.brake, brakeInput, brakeRate * Time.fixedDeltaTime * DecisionPeriod); 
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
        
        m_CurrentActions = agentActionSample;
        
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

    public bool CheckOffCourse()
    {
        var terrainSense = terrainSensor.Sense().Sum();
        return terrainSense >= 0.5f;
    }

    void CheckVertical()
    {
        var agentUp = transform.up;
        var upAngle = Vector3.Angle(Vector3.up, agentUp);
        if (upAngle > 60)
        {
            Debug.LogWarning("Up angle is greater than 60. Should be close to zero.");
            AddReward(-1f);
            EndEpisode();
        }
    }

    float GetDeltaProgress()
    {
        var radius = 2 * Mathf.PI;
        var thetaCurrent = Mathf.Rad2Deg * m_CurrentProgress / radius;
        var thetaPrevious = Mathf.Rad2Deg * m_PreviousProgress / radius;
        var deltaTheta = Mathf.DeltaAngle(thetaPrevious, thetaCurrent);
        var deltaProgress = deltaTheta * radius * lookAheadSensor.TrackLength();
        return deltaProgress;
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

    IEnumerator ResetPhysicsAndPose(Vector3 position, Vector3 direction)
    {
        Academy.Instance.AutomaticSteppingEnabled = false;
        var rotation = Quaternion.LookRotation(direction);
        m_VehicleBase.HardReposition(position, rotation, true);
        m_Rigidbody.isKinematic = false;
        yield return new WaitForSeconds(0.25f);
        Academy.Instance.AutomaticSteppingEnabled = true;
    }
}
