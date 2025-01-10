using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ML_Agents.Scripts
{
    public class AgentBenchmarkData
    {
        public List<bool> SectorFlag { get; set; } = new();
        public List<bool> LapFlag { get; set; } = new();
        public List<float> SectorTime { get; set; } = new();
        public List<float> Speed { get; set; } = new();
        public List<string> Gear { get; set; } = new();
        public List<float> TotalElecPower { get; set; } = new();
        public List<float> BatterySOC { get; set; } = new();
        public List<float> BatteryCapacity { get; set; } = new();
        public List<float> ThrottlePosition { get; set; } = new();
        public List<float> BrakePosition { get; set; } = new();
        public List<float> SteeringAngle { get; set; } = new();
        public List<float> EngagedGear { get; set; } = new();
        public List<float> FrontPowertrain { get; set; } = new();
        public List<float> RearPowertrain { get; set; } = new();
        public List<float> GroundTrackerFrontRideHeight { get; set; } = new();
        public List<float> GroundTrackerRearRideHeight { get; set; } = new();
        public List<float> GroundTrackerFrontRollAngle { get; set; } = new();
        public List<float> GroundTrackerRearRollAngle { get; set; } = new();

        public List<float> EngineRpm { get; set; } = new();
        public List<float> EngineLoad { get; set; } = new();
        public List<float> EngineTorque { get; set; } = new();
        public List<float> EnginePower { get; set; } = new();
        public List<float> AidedSteer { get; set; } = new();

        public DataTable ToDataTable()
        {
            var dataTable = new DataTable();

            var properties = typeof(AgentBenchmarkData).GetProperties();

            foreach (var prop in properties)
            {
                var listType = prop.PropertyType.GetGenericArguments()[0];
                dataTable.Columns.Add(prop.Name, listType);
            }

            var rowCount = Speed.Count;

            for (var i = 0; i < rowCount; i++)
            {
                var row = dataTable.NewRow();
                foreach (var prop in properties)
                {
                    var list = (IList)prop.GetValue(this);
                    row[prop.Name] = list[i];
                }

                dataTable.Rows.Add(row);
            }

            return dataTable;
        }
    }

    public static class AgentBenchmarkDataUtils
    {
        public static void SaveToCsv(DataTable table, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            for (var i = 0; i < table.Columns.Count; i++)
            {
                writer.Write(table.Columns[i]);
                if (i < table.Columns.Count - 1) writer.Write(",");
            }

            writer.WriteLine();

            foreach (DataRow row in table.Rows)
            {
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    writer.Write(row[i]);
                    if (i < table.Columns.Count - 1) writer.Write(",");
                }

                writer.WriteLine();
            }
        }
    }
}