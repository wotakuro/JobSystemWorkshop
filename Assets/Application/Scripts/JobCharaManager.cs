using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Jobs;
using Unity.Jobs;

/// <summary>
/// Job対応版CharacterManager
/// </summary>
public class JobCharaManager : MonoBehaviour
{
    public GameObject prefab;
    public AnimationInfo running;
    public Material drawMaterial;

    public int characterNum = 1500;

    private const float InitPosXParam = 10.0f;
    private const float InitPosZParam = 10.0f;


    private Transform[] characterTransforms;
    /// <summary>
    /// 絵の変更等をするための部分
    /// </summary>
    private BoardRenderer[] boardRenderers;
    private NativeArray<Vector3> velocities;
    private NativeArray<Rect> drawParameter;

    private NativeArray<Rect> animationRectInfo;


    private JobHandle nextFrameSyncHandle;


    private TransformAccessArray transformAccessArray;
    private NativeArray<RaycastHit> rayCastResults;
    private NativeArray<RaycastCommand> rayCastCommmands;

    
    /// <summary>
    /// Rayを一括計算するJob
    /// </summary>
    private struct CreateRaycastJob : IJobParallelForTransform
    {
        [WriteOnly]
        public NativeArray<RaycastCommand> rayCastCommands;
        [ReadOnly]
        public NativeArray<Vector3> velocities;
        [ReadOnly]
        public float rayLength;

        public void Execute(int index, TransformAccess transform)
        {
            rayCastCommands[index] = new RaycastCommand(transform.position, velocities[index],rayLength);
        }
    }

    /// <summary>
    /// キャラクターを動かすためのJob
    /// </summary>
    private struct CharacterMoveJob : IJobParallelForTransform
    {
        public NativeArray<Vector3> velocities;
        public NativeArray<Rect> drawParameter;
        [ReadOnly]
        public NativeArray<RaycastHit> rayCastResults;
        [ReadOnly]
        public Vector3 cameraPosition;
        [ReadOnly]
        public float deltaTime;
        [ReadOnly]
        public float realtimeSinceStartup;
        [ReadOnly]
        public int animationLength;
        [ReadOnly]
        public NativeArray<Rect> animationRectInfo;

        public void Execute(int index, TransformAccess transform)
        {
            if (rayCastResults[index].normal.sqrMagnitude > float.Epsilon)
            {
                velocities[index] = -velocities[index];
            }
            transform.position = transform.position + velocities[index] * deltaTime;
            transform.rotation = Quaternion.LookRotation(transform.position - cameraPosition);

            var cameraDir = transform.position - cameraPosition;
            cameraDir.y = 0.0f;
            Quaternion cameraRotate = Quaternion.FromToRotation(cameraDir, Vector3.forward);

            int direction = AnimationInfo.GetDirection(cameraRotate * velocities[index]);
            int rectIndex = ((int)(index * 0.3f + realtimeSinceStartup * 25.0f)) % animationLength + (direction * animationLength);
            this.drawParameter[index] = animationRectInfo[rectIndex];
        }
    }

    // Use this for initialization
    void Start()
    {
        running.Initialize();
        boardRenderers = new BoardRenderer[characterNum];
        characterTransforms = new Transform[characterNum];
        velocities = new NativeArray<Vector3>(characterNum, Allocator.Persistent);
        drawParameter = new NativeArray<Rect>(characterNum, Allocator.Persistent);
        animationRectInfo = new NativeArray<Rect>(running.Length, Allocator.Persistent);
        rayCastResults = new NativeArray<RaycastHit>(characterNum, Allocator.Persistent);
        rayCastCommmands = new NativeArray<RaycastCommand>(characterNum, Allocator.Persistent);
        for (int i = 0; i < running.Length; ++i)
        {
            animationRectInfo[i] = running.GetUvRect(i);
        }
        var material = new Material(drawMaterial);
        material.mainTexture = running.texture;
        for (int i = 0; i < characterNum; ++i)
        {
            var gmo = GameObject.Instantiate(prefab, new Vector3(Random.RandomRange(-InitPosXParam, InitPosXParam), 0.5f, Random.RandomRange(-InitPosZParam, InitPosZParam)), Quaternion.identity);
            characterTransforms[i] = gmo.transform;
            boardRenderers[i] = gmo.GetComponent<BoardRenderer>();
            boardRenderers[i].SetMaterial(material );
            int idx = i % running.sprites.Length;
            boardRenderers[i].SetRect( running.GetUvRect( idx ) );
        }

        for (int i = 0; i < characterNum; ++i)
        {
            velocities[i] = new Vector3(Random.RandomRange(-1.0f, 1.0f), 0.0f, Random.RandomRange(-1.0f, 1.0f));
            velocities[i] = velocities[i].normalized;
        }
        transformAccessArray = new TransformAccessArray(characterTransforms);
    }
    void OnDestroy()
    {
        transformAccessArray.Dispose();
        animationRectInfo.Dispose();
        velocities.Dispose();
        drawParameter.Dispose();
        rayCastResults.Dispose();
        rayCastCommmands.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        cameraPosition.y = 0.0f;

        nextFrameSyncHandle.Complete();
        // RayCastHitの作成
        float rayLength = Time.deltaTime * 3.0f;
        var createRaycastCommandJob = new CreateRaycastJob()
        {
            rayLength = rayLength,
            rayCastCommands = this.rayCastCommmands,
            velocities = this.velocities
        };

        // キャラクターの移動Job
        var characterJob = new CharacterMoveJob()
        {
            velocities = this.velocities,
            drawParameter = this.drawParameter,
            rayCastResults = rayCastResults,
            cameraPosition = cameraPosition,
            deltaTime = Time.deltaTime,
            realtimeSinceStartup = Time.realtimeSinceStartup,
            animationLength = running.animationLength,
            animationRectInfo = this.animationRectInfo
        };


        // raycast のJob化
        JobHandle rayCastJobHandle = RaycastCommand.ScheduleBatch(rayCastCommmands, rayCastResults, 1 );
        JobHandle.ScheduleBatchedJobs();

        // Rectの指定
        for (int i = 0; i < characterNum; ++i)
        {
            boardRenderers[i].SetRect(drawParameter[i]);
        }
        var moveJobHandl = characterJob.Schedule(transformAccessArray, rayCastJobHandle);
        nextFrameSyncHandle = createRaycastCommandJob.Schedule(this.transformAccessArray, moveJobHandl);
        JobHandle.ScheduleBatchedJobs();


    }


}
