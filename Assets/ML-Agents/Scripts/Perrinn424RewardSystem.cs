using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public class Perrinn424RewardSystem : MonoBehaviour
{
    [SerializeField]
    Perrinn424Agent perrinn424Agent;

    [SerializeField]
    float steeringPenaltyScale = 0.1f;

    [SerializeField]
    float wallContactPenaltyScale = 5e-4f;

    [SerializeField]
    float offCoursePenaltyScale = 5e-4f;

    float m_PreviousTimeOffCourse;
    float m_PreviousWallHitTime;
    
    void OnEnable()
    {
        Academy.Instance.AgentPreStep += AddStepReward;
        perrinn424Agent.wallHit += OnWallHit;
    }

    void AddStepReward(int stepCount)
    {
        
    }

    public void OnWallHit()
    {
        // var velocity = perrinn424Agent.Velocity;
        // perrinn424Agent.AddReward(- Mathf.Pow(velocity.magnitude, 2) * wallContactPenaltyScale);
        perrinn424Agent.AddReward(-10f);
        perrinn424Agent.EndEpisode();
    }
    
}
