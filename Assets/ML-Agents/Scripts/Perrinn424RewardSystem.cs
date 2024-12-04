using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.MLAgents;
using UnityEngine;

public class Perrinn424RewardSystem : MonoBehaviour
{
    [SerializeField]
    Perrinn424Agent perrinn424Agent;

    [SerializeField]
    float progressScale = 5e-4f;
    
    [SerializeField]
    float wallContactPenaltyScale = 5e-4f;

    [SerializeField]
    float offCoursePenaltyScale = 5e-4f;
    
    public float currentReward;

    float m_PreviousTimeOffCourse;
    float m_PreviousWallHitTime;
    
    void OnEnable()
    {
        Academy.Instance.AgentPreStep += AddStepReward;
        // perrinn424Agent.wallHit += OnWallHit;
    }

    void AddStepReward(int stepCount)
    {
        var offCourse = perrinn424Agent.CheckOffCourse();
        var isAligned = perrinn424Agent.isAligned;
        if (stepCount % perrinn424Agent.DecisionPeriod == 0)
        {
            var rewardScalingFactor = (float)perrinn424Agent.DecisionPeriod / perrinn424Agent.MaxStep;
            
            var velocitySquared = Mathf.Pow(perrinn424Agent.Velocity.magnitude, 2);

            var offCoursePenalty = - (perrinn424Agent.CumulativeTimeOffCourse - m_PreviousTimeOffCourse) * velocitySquared * offCoursePenaltyScale;

            var wallPenalty = - (perrinn424Agent.CumulativeWallHitTime - m_PreviousWallHitTime) * velocitySquared * wallContactPenaltyScale;

            var progressReward = Mathf.Clamp(!offCourse ? progressScale * perrinn424Agent.DeltaProgress : 0f, 0f, 10f);
            
            currentReward = progressReward + offCoursePenalty + wallPenalty;
            
            perrinn424Agent.AddReward(rewardScalingFactor * currentReward);
            
            m_PreviousTimeOffCourse = perrinn424Agent.CumulativeTimeOffCourse;
            
            m_PreviousWallHitTime = perrinn424Agent.CumulativeWallHitTime;
        }
    }

    void OnWallHit()
    {
        var velocity = perrinn424Agent.Velocity;
        
        // wall hit penalty
        perrinn424Agent.AddReward(- Mathf.Pow(velocity.magnitude, 2) * wallContactPenaltyScale);
    }
    
}
