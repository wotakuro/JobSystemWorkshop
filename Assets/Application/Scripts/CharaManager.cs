﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


public class CharaManager : MonoBehaviour
{
    public GameObject prefab;
    public AnimationInfo running;

    public int unityChanNum = 100;

    private const float InitPosXParam = 20.0f;
    private const float InitPosZParam = 20.0f;


    private Transform[] characterTransforms;
    /// <summary>
    /// 絵の変更等をするための部分
    /// </summary>
    private BoardRenderer[] boardRenderers;
    private NativeArray<Vector3> velocities;
    private NativeArray<Rect> drawParameter;


    private CommandBuffer commandBuffer;

    // Use this for initialization
    void Start()
    {
        boardRenderers = new BoardRenderer[unityChanNum];
        characterTransforms = new Transform[unityChanNum];
        velocities = new NativeArray<Vector3>(unityChanNum, Allocator.Persistent);
        drawParameter = new NativeArray<Rect>(unityChanNum, Allocator.Persistent);

        running.Initialize();
        for (int i = 0; i < unityChanNum; ++i)
        {
            var gmo = GameObject.Instantiate(prefab, new Vector3(Random.RandomRange(-InitPosXParam, InitPosXParam), 0.5f, Random.RandomRange(-InitPosZParam, InitPosZParam)), Quaternion.identity);
            characterTransforms[i] = gmo.transform;
            boardRenderers[i] = gmo.GetComponent<BoardRenderer>();
            boardRenderers[i].SetTexture( running.sprites[0].texture );
            int idx = i % running.sprites.Length;
            boardRenderers[i].SetRect( running.GetUvRect( idx ) );
        }

        for (int i = 0; i < unityChanNum; ++i)
        {
            velocities[i] = new Vector3(Random.RandomRange(-1.0f, 1.0f), 0.0f, Random.RandomRange(-1.0f, 1.0f));
            velocities[i] = velocities[i].normalized;
        }
    }
    void OnDestroy()
    {
        velocities.Dispose();
        drawParameter.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        cameraPosition.y = 0.5f;

        for (int i = 0; i < unityChanNum; ++i)
        {
            if (Physics.Raycast(new Ray(characterTransforms[i].position, velocities[i]), Time.deltaTime * 3.0f))
            {
                velocities[i] = -velocities[i];
            }
        }

        for (int i = 0; i < unityChanNum; ++i)
        {
            // デバッグ用コード
            if (unityChanNum == 1)
            {
                velocities[i] = new Vector3(0, 0, 1);
                characterTransforms[i].position = Vector3.up;
            }
            else
            {
                characterTransforms[i].position = characterTransforms[i].position + velocities[i] * Time.deltaTime;
            }

            characterTransforms[i].rotation = Quaternion.LookRotation(characterTransforms[i].position - cameraPosition);
        }


        //
        int animationLength = 15;
        for (int i = 0; i < unityChanNum; ++i)
        {
            var cameraDir = characterTransforms[i].position - Camera.main.transform.position;
            cameraDir.y = 0.0f;
            Quaternion cameraRotate = Quaternion.FromToRotation(cameraDir, Vector3.forward);

            int direction = GetDirection(cameraRotate * velocities[i]);
            int idx = ((int)(i * 0.3f + Time.realtimeSinceStartup * 20.0f) ) % animationLength + (direction * animationLength);
            this.drawParameter[i] = running.GetUvRect(idx);
        }


        for (int i = 0; i < unityChanNum; ++i)
        {
            boardRenderers[i].SetRect(drawParameter[i]);
        }

    }

    private static int GetDirection(Vector3 dir)
    {
        float param1 = 0.84f;
        float param2 = 0.4f;

        dir.Normalize();
        if (dir.z > param1) { 
            return 4;
        }else if( dir.z > param2 ){
            if (dir.x > 0.0f) { return 3; }
            else { return 5; }
        }
        else if (dir.z > -param2)
        {
            if (dir.x > 0.0f) { return 2; }
            else { return 6; }
        }
        else if (dir.z > -param1)
        {
            if (dir.x > 0.0f) { return 1; }
            else { return 7; }
        }
        else
        {
            return 0;
        }
    }

}
