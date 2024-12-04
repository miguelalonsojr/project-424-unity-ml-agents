using Perrinn424.AutopilotSystem;
using UnityEngine;
using UnityEngine.Serialization;
using VehiclePhysics;
using VehiclePhysics.Timing;

public class AgentPilot : BaseAutopilot
    {

        [Header("References")]
        public RecordedLap recordedLap;

        [SerializeField]
        LapTimer timer;

        [SerializeField]
        PathDrawer pathDrawer;
        
        [SerializeField]
        Perrinn424Agent perrinnAgent;
        
        [Header("Setup")]

        [Tooltip("Sets the agent to be the pilot. Overrides the Auto Pilot Start.")]
        [SerializeField]
        bool agentPilotStart = true;
        
        [SerializeField]
        bool autoPilotStart = false;

        [SerializeField]
        AutopilotStartup startup;
        
        public PositionCorrector lateralCorrector;

        [SerializeField]
        TimeCorrector timeCorrector;

        [FormerlySerializedAs("offset")]
        public float positionOffset = 0.9f;

        AutopilotSearcher m_AutopilotSearcher;
        AutopilotDebugDrawer m_DebugDrawer;
        IPIDInfo PIDInfo => lateralCorrector;

        public Sample ReferenceSample { get; private set; }
        public float ReferenceSpeed { get; private set; }
        public override float PlayingTime => m_PlayingTime;
        float m_PlayingTime;

        public override float DeltaTime => m_DeltaTime;
        float m_DeltaTime;

        public override float Duration => recordedLap.lapTime;
        public override long Timestamp => recordedLap.timestamp;

        public override void OnEnableVehicle()
        {
            m_AutopilotSearcher = new AutopilotSearcher(this, recordedLap);
            lateralCorrector.Init(vehicle.cachedRigidbody);
            timeCorrector.Init(vehicle.cachedRigidbody);
            startup.Init(vehicle);
            m_DebugDrawer = new AutopilotDebugDrawer();
            pathDrawer.recordedLap = recordedLap;

            vehicle.onBeforeUpdateBlocks += UpdateAutopilot;
            UpdateAutopilot();

            if (agentPilotStart)
            {
                autoPilotStart = false;
                SetStatus(true);
            }

            if (autoPilotStart)
            {
                SetStatus(true);
            }
        }

        public override void OnDisableVehicle()
        {
            vehicle.onBeforeUpdateBlocks -= UpdateAutopilot;
            SetStatus(false);
        }

        public void UpdateAutopilot()
        {
            m_AutopilotSearcher.Search(vehicle.transform);
            ReferenceSample = GetInterpolatedNearestSample();
            ReferenceSpeed = ReferenceSample.speed;
            m_PlayingTime = CalculatePlayingTime();
            m_DeltaTime = timer.currentLapTime - PlayingTime;
            pathDrawer.index = m_AutopilotSearcher.StartIndex;

            if (IsOn)
            {
                UpdateAutopilotInOnStatus();
            }
        }

        void UpdateAutopilotInOnStatus()
        {
            if (agentPilotStart)
            {
                WriteInput(currentAgentSample);
            }
            else
            {
                startup.IsStartup(ReferenceSpeed);
                Sample runningSample = ReferenceSample;
                Vector3 targetPosition = m_AutopilotSearcher.ProjectedPosition;

                float yawError = RotationCorrector.YawError(vehicle.transform.rotation, runningSample.rotation);

                if (yawError > 90f)
                {
                    SetStatus(false);
                    return;
                }

                if (IsStartup) //startup block
                {
                    lateralCorrector.Correct(targetPosition);
                    runningSample = startup.Correct(runningSample);
                }
                else //main block
                {
                    lateralCorrector.Correct(targetPosition);
                    float currentTime = timer.currentLapTime;
                    timeCorrector.Correct(CalculatePlayingTime(), currentTime);
                }

                m_DebugDrawer.Set(targetPosition, lateralCorrector.ApplicationPosition, lateralCorrector.Force);
                WriteInput(runningSample);
            }
        }

        float CalculatePlayingTime()
        {
            float sampleIndex = (m_AutopilotSearcher.StartIndex + m_AutopilotSearcher.Ratio);
            float playingTimeBySampleIndex = sampleIndex / recordedLap.frequency;
            float offset = vehicle.speed > 10f ? positionOffset / vehicle.speed : 0f;
            return playingTimeBySampleIndex - offset;
        }

        protected override void SetStatus(bool isOn)
        {
            if (isOn)
            {
                if (!CanOperate())
                {
                    Debug.LogWarning("Autopilot can't operate from these conditions, resetting.");
                }
            }
            else
            {
                vehicle.data.Set(Channel.Custom, Perrinn424Data.EnableProcessedInput, 0);
            }

            base.SetStatus(isOn);
        }

        bool CanOperate()
        {

            // Quaternion pathRotation = recordedLap.samples[m_AutopilotSearcher.StartIndex].rotation;
            // float yawError = RotationCorrector.YawError(vehicle.transform.rotation, pathRotation);
            //
            // if (Mathf.Abs(yawError) > 30f)
            // {
            //     return false;
            // }

            return true;
        }

        Sample GetInterpolatedNearestSample()
        {
            Sample start = recordedLap[m_AutopilotSearcher.StartIndex];
            Sample end = recordedLap[m_AutopilotSearcher.EndIndex];
            float t = m_AutopilotSearcher.Ratio;
            Sample interpolatedSample = Sample.Lerp(start, end, t);

            return interpolatedSample;
        }

        void WriteInput(Sample s)
        {
            vehicle.data.Set(Channel.Custom, Perrinn424Data.EnableProcessedInput, 1);
            vehicle.data.Set(Channel.Custom, Perrinn424Data.InputDrsPosition, (int)(s.drsPosition * 10.0f));
            vehicle.data.Set(Channel.Custom, Perrinn424Data.InputSteerAngle, (int)(s.steeringAngle * 10000.0f));
            vehicle.data.Set(Channel.Custom, Perrinn424Data.InputThrottlePosition, (int)(s.throttle * 100.0f));
            vehicle.data.Set(Channel.Custom, Perrinn424Data.InputBrakePosition, (int)(s.brake * 100.0f));
            vehicle.data.Set(Channel.Custom, Perrinn424Data.InputGear, s.gear); //TODO
        }

        void OnDrawGizmos()
        {
            if(m_DebugDrawer != null)
                m_DebugDrawer.Draw();
        }

        public override float Error => PIDInfo.Error;
        public override float P => PIDInfo.P;
        public override float I => PIDInfo.I;
        public override float D => PIDInfo.D;
        public override float PID => PIDInfo.PID;
        public override float MaxForceP => PIDInfo.MaxForceP;
        public override float MaxForceD => PIDInfo.MaxForceD; //TODO remove MaxForceD
        public override bool IsStartup => startup.isStartUp;

        public Sample currentAgentSample;
    }
