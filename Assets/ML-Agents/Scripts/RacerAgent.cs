using System;
using System.Collections;
using System.Linq;
using Perrinn424;
using Unity.Mathematics;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Splines;
using VehiclePhysics;
using VehiclePhysics.InputManagement;
using Random = UnityEngine.Random;

public class RacerAgent : Agent
{
    [SerializeField]
    Perrinn424Input carControllerInput;
    
    [SerializeField]
    SplineContainer trackCenterLineSplineContainer;

    [SerializeField]
    float lookAheadMinTime = 1f;
    
    [SerializeField]
    float lookAheadMaxTime = 5f;
    
    [SerializeField]
    float minLookAheadVelocity = 5f;

    [SerializeField]
    int lookAheadNumber = 10;

    [SerializeField]
    float lookAheadPointSpacing = 10f;

    [SerializeField]
    float maxInitialVelocity = 28f;
    
    [SerializeField]
    TerrainSensor terrainSensor;

    [SerializeField]
    bool randomStart = true;
    
    [SerializeField]
    bool debug = true;

    public int DecisionPeriod { get; private set; }

    public float Progress => GetProgress() * Mathf.Sign(m_Alignment);

    
    [SerializeField]
    public event Action wallHit;

    public float[] CurrentActions => m_CurrentActions;
    public float[] PreviousActions => m_PreviousActions;

    public Vector3 Velocity => m_Velocity;

    public float CumulativeTimeOffCourse => m_CumulativeTimeOffCourse;

    public float CumulativeWallHitTime => m_CumulativeWallHitTime;

    Rigidbody m_RigidBody;
    Vector3 m_InitialPosition;
    Transform m_InitialTransform;
    VehicleBase m_Vehicle;
    Steering.Settings m_SteeringSettings;
    Vector3 m_PreviousVelocity;
    float[] m_PreviousActions;
    float[] m_CurrentActions;
    Spline m_CenterLineSpline;
    Vector3[] m_LookAheadBuffer;
    Vector3 m_SplineRoot;
    Vector3 m_CurrentNearestPoint;
    Vector3 m_PreviousNearestPoint;
    float m_Alignment;
    Vector3 m_Acceleration;
    Vector3 m_Velocity;
    float m_TrackLength;
    float m_CumulativeTimeOffCourse;
    float m_CumulativeWallHitTime;

    void FixedUpdate()
    {
        m_Velocity = m_RigidBody.velocity;
        m_Acceleration = (m_Velocity - m_PreviousVelocity) / Time.fixedDeltaTime;
        CheckVertical();
        if (CheckOffCourse())
        {
            m_CumulativeTimeOffCourse += Time.fixedDeltaTime;
        }
    }

    public override void Initialize()
    {
        var rootTransform = transform;
        m_InitialTransform = rootTransform;
        m_InitialPosition = rootTransform.position;
        m_RigidBody = GetComponent<Rigidbody>();
        m_Vehicle = GetComponentInChildren<VehicleBase>();
        m_Vehicle.data.Set(Channel.Custom, Perrinn424Data.EnableProcessedInput, 1);
        m_Vehicle.data.Set(Channel.Custom, Perrinn424Data.InputGear, 1);
        m_SteeringSettings = m_Vehicle.GetInternalObject(typeof(Steering.Settings)) as Steering.Settings;
        m_PreviousActions = new float[3];
        m_CurrentActions = new float[3];
        m_CenterLineSpline = trackCenterLineSplineContainer[0];
        m_SplineRoot = trackCenterLineSplineContainer.transform.position;
        DecisionPeriod = GetComponent<DecisionRequester>().DecisionPeriod;
        m_TrackLength = trackCenterLineSplineContainer.CalculateLength();
    }

    public override void OnEpisodeBegin()
    {
        Vector3 newPosition, newForwardDirection;
        
        if (randomStart)
            ( newPosition,  newForwardDirection) = SampleStartingPosition();
        else
        {
             newPosition = m_InitialPosition;
             newForwardDirection = m_InitialTransform.forward;
        }
        
        Array.Clear(m_PreviousActions, 0, m_PreviousActions.Length);
        Array.Clear(m_CurrentActions, 0, m_CurrentActions.Length);
        m_PreviousVelocity = Vector3.zero;
        
        StartCoroutine(ResetPhysicsAndPose(newPosition + new Vector3(0f, 0.5f, 0f), newForwardDirection));
    }

    (Vector3, Vector3) SampleStartingPosition()
    {
        var randT = Random.Range(0f, 1f);
        var newPosition = trackCenterLineSplineContainer.EvaluatePosition(randT);
        var newForwardDirection = trackCenterLineSplineContainer.EvaluateTangent(randT);
        return (newPosition, newForwardDirection);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // total obs space 50

        // linear velocity vec3
        sensor.AddObservation(transform.InverseTransformDirection(m_RigidBody.velocity));

        // linear acceleration vec3
        sensor.AddObservation(transform.InverseTransformDirection(m_Acceleration));

        // angular velocity vec3
        sensor.AddObservation(transform.InverseTransformDirection(m_RigidBody.angularVelocity));

        // previous actions float[4]
        sensor.AddObservation(m_CurrentActions);

        // center line angle float, center line offset float, look ahead float[10], progress along track (sin and cos components) float[2]
        var (centerLineAngle, centerLineOffset, lookAhead, progress) = GetSplineObservations();
        
        sensor.AddObservation(centerLineAngle);
        
        m_LookAheadBuffer = lookAhead;
        
        foreach (var point in lookAhead)
        {
            sensor.AddObservation(transform.InverseTransformPoint(point));
        }

        // sensor.AddObservation(progress);
        
        // sensor.AddObservation(transform.InverseTransformDirection(centerLineOffset));

        // wall collisions (binary 1 = hit) float - Not sure we need this if we reset on collision - tbd
        
        // off-course check float
        sensor.AddObservation(CheckOffCourse());
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        float physicalWheelRange = InputManager.instance.settings.physicalWheelRange;
        var steeringInput = 360f * Mathf.Clamp(actionBuffers.ContinuousActions[0] / m_SteeringSettings.steeringWheelRange * physicalWheelRange, -1.0f, 1.0f);
        // var steeringInput = (m_PreviousActions[0] + 360f * Mathf.Clamp(actionBuffers.ContinuousActions[0] / m_SteeringSettings.steeringWheelRange * physicalWheelRange, -1.0f, 1.0f)) / 2f;
        m_Vehicle.data.Set(Channel.Custom, Perrinn424Data.InputSteerAngle, (int)(steeringInput * 10000f));
        var throttleInput = Mathf.Clamp01(actionBuffers.ContinuousActions[1]);
        var brakeInput = Mathf.Clamp01(-actionBuffers.ContinuousActions[1]);
        // var drsInput = Mathf.Clamp01((actionBuffers.ContinuousActions[2] + 1) / 2);
        m_Vehicle.data.Set(Channel.Custom, Perrinn424Data.InputThrottlePosition, (int)(throttleInput * 10000f));
        m_Vehicle.data.Set(Channel.Custom, Perrinn424Data.InputBrakePosition, (int)(brakeInput * 10000f));
        // m_Vehicle.data.Set(Channel.Custom, Perrinn424Data.InputDrsPosition, (int)(drsInput * 1000f));
        m_CurrentActions.CopyTo(m_PreviousActions, 0);
        m_CurrentActions[0] = steeringInput;
        m_CurrentActions[1] = throttleInput;
        m_CurrentActions[2] = brakeInput;
        // m_CurrentActions[3] = drsInput;
        
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
        // continuousActionsOut[2] = 0f;
    }

    IEnumerator ResetPhysicsAndPose(Vector3 pose, Vector3 direction)
    {
        carControllerInput.externalBrake = 0f;
        carControllerInput.externalThrottle = 0f;
        carControllerInput.externalSteer = 0f;
        yield return new WaitForSeconds(1f / 500);
        m_RigidBody.isKinematic = true;
        var rotation = Quaternion.LookRotation(direction);
        m_RigidBody.Move(pose, rotation);
        SplineUtility.GetNearestPoint(m_CenterLineSpline, pose - m_SplineRoot, out var nearestPoint, out var t);
        m_CurrentNearestPoint = nearestPoint;
        m_PreviousNearestPoint = m_CurrentNearestPoint;
        yield return new WaitForSeconds(1f / 500);
        m_RigidBody.isKinematic = false;
        var initialVelocity = Random.Range(0f, maxInitialVelocity) * m_RigidBody.transform.forward;
        m_RigidBody.velocity = initialVelocity;
        yield return new WaitForSeconds(1f / 500);

    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Wall"))
        {
            // EndEpisode();
            wallHit?.Invoke();
        }
    }

    void OnCollisionStay(Collision other)
    {
        if (other.gameObject.CompareTag("Wall"))
        {
            m_CumulativeWallHitTime += Time.fixedDeltaTime;
        }
    }

    (float, float3, Vector3[], Vector2) GetSplineObservations()
    {
        var currentTransform = transform;
        var position = currentTransform.position;
        SplineUtility.GetNearestPoint(m_CenterLineSpline, position - m_SplineRoot, out float3 nearestPoint, out float t);
        var point = nearestPoint + (float3)m_SplineRoot;
        var centerLineOffset = point - (float3)position;
        m_PreviousNearestPoint = m_CurrentNearestPoint;
        m_CurrentNearestPoint = nearestPoint;
        var centerLineDirection = SplineUtility.EvaluateTangent(m_CenterLineSpline, t);
        var forward = currentTransform.forward;
        // var up = currentTransform.up;
        var centerLineAngle = Vector2.SignedAngle(new Vector2(centerLineDirection.x, centerLineDirection.z), new Vector2(forward.x, forward.z));
        // var centerLineAngle = Vector3.SignedAngle(centerLineDirection, forward, up);
        m_Alignment = Vector3.Dot(centerLineDirection, forward);
        var newLookahead = new Vector3[lookAheadNumber];

        for (int i = 0; i < lookAheadNumber; i++)
        {
            // newLookahead[i] = SplineUtility.GetPointAtLinearDistance(m_CenterLineSpline, t, (i + 1) * lookAheadPointSpacing, out _) + (float3)m_SplineRoot;

            var deltaTime = (lookAheadMaxTime - lookAheadMinTime) / lookAheadNumber;
            var relativePoint = Mathf.Max(minLookAheadVelocity,transform.InverseTransformDirection(m_RigidBody.velocity).magnitude) * deltaTime * (i + 1);
            newLookahead[i] = SplineUtility.GetPointAtLinearDistance(m_CenterLineSpline, t, relativePoint, out _) + (float3)m_SplineRoot;
        }
        
        var progress = new Vector2(Mathf.Sin(2 * Mathf.PI * t), Mathf.Cos(2 * Mathf.PI * t));
        
        return (centerLineAngle, centerLineOffset, newLookahead, progress);
    }

    float GetProgress()
    {
        return Vector3.Distance(m_CurrentNearestPoint, m_PreviousNearestPoint);
    }

    public bool CheckOffCourse()
    {
        var terrainSense = terrainSensor.Sense().Sum();
        return terrainSense >= 0.75f;
    }

    void CheckVertical()
    {
        var agentUp = transform.up;
        var upAngle = Vector3.Angle(Vector3.up, agentUp);
        if (upAngle > 60)
        {
            AddReward(-10f);
            EndEpisode();
        }
    }

    void OnDrawGizmos()
    {
        if (debug)
        {
            if (m_LookAheadBuffer?.Length > 0)
            {
                foreach (var point in m_LookAheadBuffer)
                {
                    var oldColor = Gizmos.color;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(point, 1);
                    Gizmos.color = oldColor;
                }
            }
        }
    }
}
