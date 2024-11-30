using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using Random = UnityEngine.Random;

public class LookAheadSensor : MonoBehaviour
{
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
    bool debug = true;
    
    [SerializeField]
    public Rigidbody rb;
    
    Spline m_CenterLineSpline;
    Vector3 m_SplineRoot;
    Vector3 m_PreviousNearestPoint;
    Vector3 m_CurrentNearestPoint;
    float m_Alignment;
    Vector3[] m_LookAheadBuffer;
    float m_TrackLength;

    void Awake()
    {
        m_CenterLineSpline = trackCenterLineSplineContainer[0];
        m_SplineRoot = trackCenterLineSplineContainer.transform.position;
        m_TrackLength = trackCenterLineSplineContainer.CalculateLength();
    }
    
    public (float, float3, Vector3[], Vector2) Sense()
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
        var centerLineAngle = -Vector2.SignedAngle(new Vector2(centerLineDirection.x, centerLineDirection.z), new Vector2(forward.x, forward.z));
        m_Alignment = Vector3.Dot(centerLineDirection, forward);
        var newLookahead = new Vector3[lookAheadNumber];

        for (int i = 0; i < lookAheadNumber; i++)
        {
            var deltaTime = (lookAheadMaxTime - lookAheadMinTime) / lookAheadNumber;
            var relativePoint = Mathf.Max(minLookAheadVelocity,transform.InverseTransformDirection(rb.velocity).magnitude) * deltaTime * (i + 1);
            newLookahead[i] = SplineUtility.GetPointAtLinearDistance(m_CenterLineSpline, t, relativePoint, out _) + (float3)m_SplineRoot;
        }
        
        var progress = new Vector2(Mathf.Sin(2 * Mathf.PI * t), Mathf.Cos(2 * Mathf.PI * t));
        
        m_LookAheadBuffer = newLookahead;
        
        return (centerLineAngle, centerLineOffset, newLookahead, progress);
    }
    
    public (Vector3, Vector3) SampleStartingPosition()
    {
        var randT = Random.Range(0f, 1f);
        var newPosition = trackCenterLineSplineContainer.EvaluatePosition(randT);
        var newForwardDirection = trackCenterLineSplineContainer.EvaluateTangent(randT);
        return (newPosition, newForwardDirection);
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
                    Gizmos.DrawSphere(point, 0.25f);
                    Gizmos.color = oldColor;
                }
            }
        }
    }
    
}
