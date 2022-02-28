﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostPlayer : MonoBehaviour
{

    private Material material;
    public Color color;
    private void OnEnable()
    {
        material = new Material(Shader.Find("Standard"));
        material.SetFloat("_Mode", 3f);

        //https://forum.unity.com/threads/standard-material-shader-ignoring-setfloat-property-_mode.344557/
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        material.color = color;

        foreach (var renderer in this.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.sharedMaterial = material;
        }
    }

    private void OnDisable()
    {
        Destroy(material);
    }
}
