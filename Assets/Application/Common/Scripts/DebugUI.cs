using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

public class DebugUI : MonoBehaviour
{
    private static DebugUI instance;

    public Text misakiNumText;
    public Text yukoNumText;
    public Text fpsText;
    int fpsCount;
    float fpsSum;
    private StringBuilder sb = new StringBuilder();

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        instance = null;
    }

    void Update()
    {
        fpsSum += Time.deltaTime;
        fpsCount++;
        if (fpsSum > 0.5f)
        {
            int fps = (int)(1.0f / (fpsSum / fpsCount));
            sb.Length = 0;
            sb.Append("fps:").Append(fps);
            fpsText.text = sb.ToString();
            fpsSum = 0.0f;
            fpsCount = 0;
        }
    }

    private static void SetMisakiNum(int num)
    {
        if (instance != null )
        {
            instance.UpdateMisakiNum(instance.misakiNumText, "Misaki:", num);
        }
    }
    private static void SetYukoNum(int num)
    {
        if (instance != null)
        {
            instance.UpdateMisakiNum(instance.yukoNumText, "Yuko:", num);
        }
    }

    void UpdateMisakiNum(Text txt,string head,int num)
    {
        if (txt == null) { return; }
        sb.Length = 0;
        sb.Append(head).Append(num);
        txt.text = sb.ToString();
    }

    public static void SetCounter(AppAnimationInfo info, int num)
    {
        if (info.sprites[0].name.StartsWith("Misaki"))
        {
            SetMisakiNum(num);
        }
        else if (info.sprites[0].name.StartsWith("Yuko"))
        {
            SetYukoNum(num);
        }
    }
}
