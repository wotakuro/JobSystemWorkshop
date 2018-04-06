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
public class Work4JobCharaManager : MonoBehaviour
{
    // キャラクターのプレハブ
    public GameObject prefab;
    // アニメーションの情報
    public AnimationInfo animationInfo;
    // 描画用のマテリアル
    public Material drawMaterial;

    // キャラクター数
    public int characterNum = 1000;

    // ランダム出現位置に関するぱらえーた
    private const float InitPosXParam = 10.0f;
    private const float InitPosZParam = 10.0f;


    // 動かす対象キャラのTransformリスト
    private Transform[] characterTransforms;
    // 各キャラクターの移動速度
    private NativeArray<Vector3> velocities;
    // 各キャラクターの描画用する矩形領域
    private NativeArray<Rect> drawParameter;

    // 絵の変更等をするための部分
    private BoardRenderer[] boardRenderers;

    // Animationの矩形情報
    private NativeArray<Rect> animationRectInfo;

    // フレームの頭で処理の同期をするようです
    private JobHandle nextFrameSyncHandle;

    // jobで動かす対象のTransformのリスト
    private TransformAccessArray transformAccessArray;
    // raycastの結果保存用
    private NativeArray<RaycastHit> rayCastResults;
    // raycastのコマンド
    private NativeArray<RaycastCommand> rayCastCommmands;

    
    /// <summary>
    /// RayCastCommandの生成を並行して行うためのJob
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

    /// <summary>
    /// 初期化処理
    /// </summary>
    void Start()
    {
        // animation の情報初期化
        animationInfo.Initialize();
        // それぞれのバッファーを初期化/作成
        boardRenderers = new BoardRenderer[characterNum];
        characterTransforms = new Transform[characterNum];
        velocities = new NativeArray<Vector3>(characterNum, Allocator.Persistent);
        drawParameter = new NativeArray<Rect>(characterNum, Allocator.Persistent);
        animationRectInfo = new NativeArray<Rect>(animationInfo.Length, Allocator.Persistent);
        rayCastResults = new NativeArray<RaycastHit>(characterNum, Allocator.Persistent);
        rayCastCommmands = new NativeArray<RaycastCommand>(characterNum, Allocator.Persistent);
        for (int i = 0; i < animationInfo.Length; ++i)
        {
            animationRectInfo[i] = animationInfo.GetUvRect(i);
        }
        var material = new Material(drawMaterial);
        material.mainTexture = animationInfo.texture;
        for (int i = 0; i < characterNum; ++i)
        {
            var gmo = GameObject.Instantiate(prefab, new Vector3(Random.RandomRange(-InitPosXParam, InitPosXParam), 0.5f, Random.RandomRange(-InitPosZParam, InitPosZParam)), Quaternion.identity);
            characterTransforms[i] = gmo.transform;
            boardRenderers[i] = gmo.GetComponent<BoardRenderer>();
            boardRenderers[i].SetMaterial(material );
            int idx = i % animationInfo.sprites.Length;
            boardRenderers[i].SetRect( animationInfo.GetUvRect( idx ) );
        }

        for (int i = 0; i < characterNum; ++i)
        {
            velocities[i] = new Vector3(Random.RandomRange(-1.0f, 1.0f), 0.0f, Random.RandomRange(-1.0f, 1.0f));
            velocities[i] = velocities[i].normalized;
        }
        transformAccessArray = new TransformAccessArray(characterTransforms);
    }


    /// <summary>
    /// それぞれの NativeContainerを破棄します
    /// </summary>
    void OnDestroy()
    {
        this.nextFrameSyncHandle.Complete(); // <- ジョブが処理中のものは解放できないので、ここでJobの完了をさせます
        transformAccessArray.Dispose();
        animationRectInfo.Dispose();
        velocities.Dispose();
        drawParameter.Dispose();
        rayCastResults.Dispose();
        rayCastCommmands.Dispose();
    }

    /// <summary>
    /// 更新処理
    /// </summary>
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
            animationLength = animationInfo.animationLength,
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
