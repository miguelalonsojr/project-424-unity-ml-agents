using System;
using System.Collections;
using System.Collections.Generic;
using Perrinn424.AutopilotSystem;
using UnityEngine;
using VehiclePhysics;
using VehiclePhysics.InputManagement;

[RequireComponent(typeof(AgentPilot))]
public class AgentPilotInputController : VehicleBehaviour
{
    public BaseAutopilot agentPilot;

    void OnEnable()
    {
        InputManager.instance.runtime.disableForceFeedback = agentPilot.IsOn;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            agentPilot.ToggleStatus();
            InputManager.instance.runtime.disableForceFeedback = agentPilot.IsOn;
        }
    }

    void Reset()
    {
        if (agentPilot == null)
        {
            agentPilot = GetComponent<BaseAutopilot>();
        }
    }
}
