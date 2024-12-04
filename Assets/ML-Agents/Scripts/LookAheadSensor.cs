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
    Rigidbody rb;

    [SerializeField]
    float trackWidth = 7.5f;

    public float CenterlineAlignment => m_CenterlineAlignment;
    
    Spline m_CenterLineSpline;
    Vector3 m_SplineRoot;
    Vector3 m_PreviousNearestPoint;
    Vector3 m_CurrentNearestPoint;
    float m_CenterlineAlignment;
    Vector3[] m_LookAheadBuffer;
    Vector3[] m_CenterlineBuffer;
    Vector3[] m_InnerlineBuffer;
    Vector3[] m_OuterlineBuffer;
    float m_TrackLength;

    void Awake()
    {
        m_CenterLineSpline = trackCenterLineSplineContainer[0];
        m_SplineRoot = trackCenterLineSplineContainer.transform.position;
        m_TrackLength = trackCenterLineSplineContainer.CalculateLength();
    }
    
    public (float, float3, Vector3[], Vector2, float, Vector3[], Vector3[]) Sense()
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
        m_CenterlineAlignment = Vector3.Dot(centerLineDirection, forward);
        // var newLookahead = new Vector3[lookAheadNumber + 1];
        var newCenterline = new Vector3[lookAheadNumber + 1];
        var newInnerline = new Vector3[lookAheadNumber + 1];
        var newOuterline = new Vector3[lookAheadNumber + 1];
        newCenterline[0] = SplineUtility.GetPointAtLinearDistance(m_CenterLineSpline, t, 0, out var resultT) + (float3)m_SplineRoot;;
        var (outerPoint, innerPoint) = CalculateTrackPoints(newCenterline[0], SplineUtility.EvaluateTangent(m_CenterLineSpline, t));
        newInnerline[0] = innerPoint;
        newOuterline[0] = outerPoint;

        for (int i = 1; i < lookAheadNumber + 1; i++)
        {   var deltaTime = lookAheadMaxTime / (lookAheadNumber + 1);
            var relativePoint = Mathf.Max(minLookAheadVelocity,transform.InverseTransformDirection(rb.velocity).magnitude) * deltaTime * i;
            newCenterline[i] = SplineUtility.GetPointAtLinearDistance(m_CenterLineSpline, t, relativePoint, out resultT) + (float3)m_SplineRoot;
            (outerPoint, innerPoint) = CalculateTrackPoints(newCenterline[i], SplineUtility.EvaluateTangent(m_CenterLineSpline, resultT));
            newInnerline[i] = innerPoint;
            newOuterline[i] = outerPoint;
        }
        
        var progress = new Vector2(Mathf.Sin(2 * Mathf.PI * t), Mathf.Cos(2 * Mathf.PI * t));
        
        m_CenterlineBuffer = newCenterline;
        m_InnerlineBuffer = newInnerline;
        m_OuterlineBuffer = newOuterline;
        
        return (centerLineAngle, centerLineOffset, newCenterline, progress, t, newOuterline, newInnerline);
    }
    
    (Vector3 outerPoint, Vector3 innerPoint) CalculateTrackPoints(Vector3 point, Vector3 direction)
    {
        var go = new GameObject();
        go.transform.position = point;
        go.transform.rotation = Quaternion.LookRotation(direction);
        var innerPoint = go.transform.TransformPoint(new Vector3(trackWidth / 2, 0f, 0f));
        var outerPoint = go.transform.TransformPoint(-new Vector3(trackWidth / 2, 0f, 0f));
        DestroyImmediate(go);
        return (outerPoint, innerPoint);
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
            if (m_CenterlineBuffer?.Length > 0)
            {
                foreach (var point in m_CenterlineBuffer)
                {
                    var oldColor = Gizmos.color;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(point, 0.25f);
                    Gizmos.color = oldColor;
                }
                
                foreach (var point in m_InnerlineBuffer)
                {
                    var oldColor = Gizmos.color;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(point, 0.25f);
                    Gizmos.color = oldColor;
                }
                
                foreach (var point in m_OuterlineBuffer)
                {
                    var oldColor = Gizmos.color;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(point, 0.25f);
                    Gizmos.color = oldColor;
                }
            }
        }
    }

    public float TrackLength()
    {
        return m_CenterLineSpline.GetLength();
    }
}
