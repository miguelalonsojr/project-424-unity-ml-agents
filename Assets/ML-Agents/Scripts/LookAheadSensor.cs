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
    Rigidbody rb;

    [SerializeField]
    float trackWidth = 7.5f;
    
    [SerializeField]
    float debugSphereRadius = 0.1f;

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

        // var newCenterline = new Vector3[lookAheadNumber + 1];
        // var newInnerline = new Vector3[lookAheadNumber + 1];
        // var newOuterline = new Vector3[lookAheadNumber + 1];

        var newCenterline = new Vector3[lookAheadNumber];
        var newInnerline = new Vector3[lookAheadNumber];
        var newOuterline = new Vector3[lookAheadNumber];
        // newCenterline[0] = GetPointAtLinearDistance(m_CenterLineSpline, t, 0, out var resultT) + (float3)m_SplineRoot;
        // ;
        // var (outerPoint, innerPoint) = CalculateTrackPoints(newCenterline[0], SplineUtility.EvaluateTangent(m_CenterLineSpline, t));
        // newInnerline[0] = innerPoint;
        // newOuterline[0] = outerPoint;
        // if (Physics.Raycast(newCenterline[0], Vector3.down, out var centerHit, 10))
        // {
        //     newCenterline[0] = centerHit.point;
        // }
        //
        // if (Physics.Raycast(newInnerline[0], Vector3.down, out var innerHit, 10))
        // {
        //     newInnerline[0] = innerHit.point;
        // }
        //
        // if (Physics.Raycast(newOuterline[0], Vector3.down, out var outterHit, 10))
        // {
        //     newOuterline[0] = outterHit.point;
        // }
        
        var initialPoint = transform.InverseTransformDirection(rb.velocity).magnitude * lookAheadMinTime;
        
        Vector3 innerPoint, outerPoint;
        RaycastHit centerHit, innerHit, outterHit;
        float resultT;

        for (int i = 0; i < lookAheadNumber; i++)
        {
            var deltaTime = (lookAheadMaxTime - lookAheadMinTime) / (lookAheadNumber + 1);
            var relativePoint = Mathf.Max(minLookAheadVelocity, transform.InverseTransformDirection(rb.velocity).magnitude) * deltaTime * i;
            newCenterline[i] = GetPointAtLinearDistance(m_CenterLineSpline, t, relativePoint + initialPoint, out resultT) + (float3)m_SplineRoot;
            (outerPoint, innerPoint) = CalculateTrackPoints(newCenterline[i], SplineUtility.EvaluateTangent(m_CenterLineSpline, resultT));
            newInnerline[i] = innerPoint;
            newOuterline[i] = outerPoint;
            if (Physics.Raycast(newCenterline[i], Vector3.down, out centerHit, 10))
            {
                newCenterline[i] = centerHit.point;
            }

            ;

            if (Physics.Raycast(newInnerline[i], Vector3.down, out innerHit, 10))
            {
                newInnerline[i] = innerHit.point;
            }

            ;

            if (Physics.Raycast(newOuterline[i], Vector3.down, out outterHit, 10))
            {
                newOuterline[i] = outterHit.point;
            }

            ;
        }

        var progress = new Vector2(Mathf.Sin(2 * Mathf.PI * t), Mathf.Cos(2 * Mathf.PI * t));

        m_CenterlineBuffer = newCenterline;
        m_InnerlineBuffer = newInnerline;
        m_OuterlineBuffer = newOuterline;

        return (centerLineAngle, centerLineOffset, newCenterline, progress, t, newOuterline, newInnerline);
    }

    public static float3 GetPointAtLinearDistance<T>(T spline, float fromT, float relativeDistance, out float resultPointT) where T : ISpline
    {
        var point = SplineUtility.GetPointAtLinearDistance(spline, fromT, relativeDistance, out resultPointT);
        
        // This assumes a looped spline. 
        if (resultPointT >= 1)
        {
            var length = spline.GetLength();
            resultPointT = (fromT + relativeDistance / length) % 1;
            point = SplineUtility.EvaluatePosition(spline, resultPointT);

        }
        return point;
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
        if (Physics.Raycast(newPosition, Vector3.down, out var hit, 10))
        {
            newPosition = hit.point;
        }
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
                    Gizmos.DrawSphere(point, debugSphereRadius);
                    Gizmos.color = oldColor;
                }

                foreach (var point in m_InnerlineBuffer)
                {
                    var oldColor = Gizmos.color;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(point, debugSphereRadius);
                    Gizmos.color = oldColor;
                }

                foreach (var point in m_OuterlineBuffer)
                {
                    var oldColor = Gizmos.color;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(point, debugSphereRadius);
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
