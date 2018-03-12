using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationInfo : ScriptableObject {
    public Sprite[] sprites;

    public const int AnglePatternNum = 8;

    private Rect[] preCalculateData;

    public Rect GetUvRect(int idx)
    {
        return preCalculateData[idx];
    }

    public Texture texture
    {
        get
        {
            return sprites[0].texture;
        }
    }

    public int Length
    {
        get { return preCalculateData.Length; }
    }


    public void Initialize()
    {
        if (sprites == null || sprites.Length == 0) { return; }
        preCalculateData = new Rect[sprites.Length];
        float textureWidth = texture.width;
        float textureHeight = texture.height;
        for (int i = 0; i < sprites.Length; ++i)
        {
            var originRect = sprites[i].textureRect;

            preCalculateData[i] = new Rect(originRect.x / textureWidth, 
                originRect.y / textureHeight, 
                originRect.width / textureWidth, 
                originRect.height / textureHeight);
        }
    }
}
