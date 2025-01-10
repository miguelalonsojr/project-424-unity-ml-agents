using ML_Agents.Scripts;
using UnityEngine;
using VehiclePhysics.Timing;
using VehiclePhysics;

namespace Perrinn424
{
    public class AgentBenchmark : VehicleBehaviour
    {
        [SerializeField] LapTimer m_LapTimer;
        [SerializeField] bool m_IsAgentBenchmark = true;
        [SerializeField] int m_BenchmarkLapNum = 1;
        [SerializeField] Rigidbody m_Rigidbody;

        Battery m_battery;
        int m_SectorNumPerLap;
        int m_SectorNum;
        int m_LapNum;

        AgentBenchmarkData m_BenchmarkData = new();

        public override void OnEnableVehicle()
        {
            if (m_LapTimer != null)
            {
                m_SectorNumPerLap = m_LapTimer.sectors;
                m_LapTimer.onSector += OnSector;
                m_LapTimer.onBeginLap += LapBeginEventHandler;
            }
        }

        void OnSector(int sector, float sectorTime)
        {
            var sectorSampleNum = m_BenchmarkData.Speed.Count - m_BenchmarkData.SectorFlag.Count;
            for (var i = 0; i < sectorSampleNum - 1; i++)
            {
                m_BenchmarkData.SectorFlag.Add(false);
                m_BenchmarkData.SectorTime.Add(0);
            }
            m_BenchmarkData.SectorFlag.Add(true);
            m_BenchmarkData.SectorTime.Add(sectorTime);
            m_SectorNum++;
            Debug.Log("Sector: " + sector + " Time: " + sectorTime);
            
            if (m_SectorNum == m_SectorNumPerLap)
            {
                var lapSampleNum = m_BenchmarkData.Speed.Count - m_BenchmarkData.LapFlag.Count;
                for (var i = 0; i < lapSampleNum - 1 ; i++)
                {
                    m_BenchmarkData.LapFlag.Add(false);
                }
                m_BenchmarkData.LapFlag.Add(true);
                m_LapNum++;
            }
            
            // Save benchmark data to csv
            if (m_BenchmarkLapNum == m_LapNum)  
                AgentBenchmarkDataUtils.SaveToCsv(m_BenchmarkData.ToDataTable(), 
                    m_IsAgentBenchmark ? "AgentPilotBenchmarkData.csv":"AutoPilotBenchmarkData.csv");
        }

        public override void OnDisableVehicle()
        {
            if (m_LapTimer != null)
            {
                m_LapTimer.onSector -= OnSector;
            }
        }

        void LapBeginEventHandler()
        {
        }

        public override void FixedUpdateVehicle()
        {
            // FixedUpdateVehicle happens right after the vehicle simulation step, with all internal values updated.
            if (m_battery == null)
            {
                m_battery = vehicle.GetComponentInChildren<Battery>();
            }

            var vehicleData = vehicle.data.Get(Channel.Vehicle);
            var custom = vehicle.data.Get(Channel.Custom);

            // Vehicle data
            var speed = m_Rigidbody.velocity.magnitude;
            m_BenchmarkData.Speed.Add(speed); // Speed data (km/h)
            m_BenchmarkData.EngineRpm.Add(vehicleData[VehiclePhysics.VehicleData.EngineRpm]);
            m_BenchmarkData.EngineTorque.Add(vehicleData[VehiclePhysics.VehicleData.EngineTorque]);
            m_BenchmarkData.EngineLoad.Add(vehicleData[VehiclePhysics.VehicleData.EngineLoad]);
            m_BenchmarkData.EnginePower.Add(vehicleData[VehiclePhysics.VehicleData.EnginePower]);
            m_BenchmarkData.AidedSteer.Add(vehicleData[VehiclePhysics.VehicleData.AidedSteer]);

            // Gear data
            var gearId = vehicleData[VehiclePhysics.VehicleData.GearboxGear];
            var switchingGear = vehicleData[VehiclePhysics.VehicleData.GearboxShifting] != 0;
            var gear = gearId switch
            {
                0 => switchingGear ? " " : "N",
                > 0 => "D",
                -1 => "R",
                _ => "R" + -gearId
            };
            m_BenchmarkData.Gear.Add(gear);

            // Battery data
            if (m_battery != null)
            {
                m_BenchmarkData.TotalElecPower.Add(m_battery.Power);
                m_BenchmarkData.BatterySOC.Add(m_battery.StateOfCharge * 100f);
                m_BenchmarkData.BatteryCapacity.Add(m_battery.NetEnergy);
            }

            // Controller data
            // TODO: confirm the data bus channel get updated before FixedUpdateVehicle
            m_BenchmarkData.ThrottlePosition.Add(custom[Perrinn424Data.ThrottlePosition] / 1000.0f);
            m_BenchmarkData.BrakePosition.Add(custom[Perrinn424Data.BrakePosition] / 1000.0f);
            m_BenchmarkData.SteeringAngle.Add(custom[Perrinn424Data.SteeringWheelAngle] / 1000.0f);
            m_BenchmarkData.EngagedGear.Add(custom[Perrinn424Data.EngagedGear]);
            m_BenchmarkData.FrontPowertrain.Add(custom[Perrinn424Data.FrontDiffFriction] / 1000.0f);
            m_BenchmarkData.RearPowertrain.Add(custom[Perrinn424Data.RearDiffFriction] / 1000.0f);
            m_BenchmarkData.GroundTrackerFrontRideHeight.Add(custom[Perrinn424Data.FrontRideHeight] / 1000.0f);
            m_BenchmarkData.GroundTrackerRearRideHeight.Add(custom[Perrinn424Data.RearRideHeight] / 1000.0f);
            m_BenchmarkData.GroundTrackerFrontRollAngle.Add(custom[Perrinn424Data.FrontRollAngle] / 1000.0f);
            m_BenchmarkData.GroundTrackerRearRollAngle.Add(custom[Perrinn424Data.RearRollAngle] / 1000.0f);
        }
    }
}