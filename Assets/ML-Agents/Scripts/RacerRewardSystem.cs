using Unity.MLAgents;
using UnityEngine;

public class RacerRewardSystem : MonoBehaviour
{
    [SerializeField]
    RacerAgent racerAgent;

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
    }

    void AddStepReward(int stepCount)
    {
        var offCourse = racerAgent.CheckOffCourse();
        if (stepCount % racerAgent.DecisionPeriod == 0 && !offCourse)
        {
            var rewardScalingFactor = (float)racerAgent.DecisionPeriod / racerAgent.MaxStep;
            
            racerAgent.AddReward(rewardScalingFactor * racerAgent.Progress);

            var steeringRate = racerAgent.CurrentActions[0] - racerAgent.PreviousActions[0];

            var velocitySquared = Mathf.Pow(racerAgent.Velocity.magnitude, 2);
            
            racerAgent.AddReward(- (racerAgent.CumulativeTimeOffCourse - m_PreviousTimeOffCourse) * velocitySquared * offCoursePenaltyScale);
            
            racerAgent.AddReward( - (racerAgent.CumulativeWallHitTime - m_PreviousWallHitTime) * velocitySquared * wallContactPenaltyScale);
            
            racerAgent.AddReward(-Mathf.Pow(steeringRate, 2) * steeringPenaltyScale * rewardScalingFactor);

            m_PreviousTimeOffCourse = racerAgent.CumulativeTimeOffCourse;
            
            m_PreviousWallHitTime = racerAgent.CumulativeWallHitTime;

            // Debug.Log(-Mathf.Pow(steeringRate, 2) * steeringPenaltyScale * rewardScalingFactor);
        }

        // if (offCourse)
        // {
        //     racerAgent.EndEpisode();
        // }
    }

    public void OnWallHit()
    {
        var velocity = racerAgent.Velocity;
        racerAgent.AddReward(- Mathf.Pow(velocity.magnitude, 2) * wallContactPenaltyScale);
    }
}
