using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ShaderNameHash  {

    public static int MainTex;
    public static int RectValue;
    public static int ExpectedRect;

    [RuntimeInitializeOnLoadMethod]
    public static void Initialize()
    {
        MainTex = Shader.PropertyToID("_MainTex");
        RectValue = Shader.PropertyToID("_RectValue");
        ExpectedRect = Shader.PropertyToID("_ExpectedRect");
    }

}
