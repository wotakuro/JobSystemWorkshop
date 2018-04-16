using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// native container関連
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
//job関連
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;


// ワーク3．キャラクターを管理するマネージャ
public class Work3JobCharaManager : MonoBehaviour
{
    // キャラクターのプレハブ
    public GameObject prefab;
    // アニメーションの情報
    public AppAnimationInfo animationInfo;
    // 描画用のマテリアル
    public Material drawMaterial;

    // キャラクター数
    public int characterNum = 2000;

    // ランダム出現位置に関するぱらえーた
    private const float InitPosXParam = 15.0f;
    private const float InitPosZParam = 15.0f;

    // 動かす対象キャラのTransformリスト
    private Transform[] characterTransforms;

    // 絵の変更等をするための部分
    private BoardRenderer[] boardRenderers;

    // jobで動かす対象のTransformのリスト
    private TransformAccessArray transformAccessArray;

    // フレームの頭で処理の同期をするようです
    private JobHandle nextFrameSyncHandle;

    
    /// <summary>
    /// キャラクターを動かすためのJob
    /// </summary>
    private struct CharacterMoveJob : IJobParallelForTransform
    {
        public float deltaTime;
        public float realtimeSinceStartup;
        public Vector3 cameraPosition;
        public void Execute(int index, TransformAccess transform)
        {
            Vector3 delta = new Vector3(Mathf.Sin(realtimeSinceStartup + index) * 0.8f, 0.0f, -1);

            transform.position = transform.position + delta * deltaTime;
            if (transform.position.z < -15.0f)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, 15.0f);
            }
            transform.rotation = Quaternion.LookRotation(transform.position - cameraPosition); // <- カメラを常に向くようにします
        }
    }

    /// <summary>
    /// Start関数
    /// </summary>
    void Start()
    {
        // animation の情報初期化
        animationInfo.Initialize();
        // それぞれのバッファーを初期化/作成
        boardRenderers = new BoardRenderer[characterNum];
        characterTransforms = new Transform[characterNum];

        var material = new Material(drawMaterial);
        material.mainTexture = animationInfo.texture;
        for (int i = 0; i < characterNum; ++i)
        {
            var gmo = GameObject.Instantiate(prefab, new Vector3(Random.RandomRange(-InitPosXParam, InitPosXParam), 0.5f, Random.RandomRange(-InitPosZParam, InitPosZParam)), Quaternion.identity);
            // 今回のサンプルではColliderは不要なので削除
            GameObject.Destroy( gmo.GetComponent<Collider>() );
            characterTransforms[i] = gmo.transform;
            boardRenderers[i] = gmo.GetComponent<BoardRenderer>();
            boardRenderers[i].SetMaterial(material );
            int idx = i % animationInfo.sprites.Length;
            boardRenderers[i].SetRect( animationInfo.GetUvRect( 0 ) );
        }
        // 
        transformAccessArray = new TransformAccessArray(characterTransforms);
    }
        /// <summary>
    /// それぞれの NativeContainerを破棄します
    /// </summary>
    void OnDestroy()
    {
        this.nextFrameSyncHandle.Complete(); // <- ジョブが処理中のものは解放できないので、ここでJobの完了をさせます
        transformAccessArray.Dispose();
    }

    /// <summary>
    /// 更新処理
    /// </summary>
    void Update()
    {
        // ここで発行したジョブの同期をします
        this.nextFrameSyncHandle.Complete();

        // キャラをパラパラ動かします
        // ここは MainThreadで
        UnityEngine.Profiling.Profiler.BeginSample("SetAllCharaRect");
        int animationLength = animationInfo.animationLength;
        for (int i = 0; i < characterNum; ++i)
        {
            int rectIndex = ((int)(i * 0.3f + Time.realtimeSinceStartup * 25.0f)) % animationLength;
            boardRenderers[i].SetRect(animationInfo.GetUvRect(rectIndex));
        }
        UnityEngine.Profiling.Profiler.EndSample();

        // 全キャラクターの移動処理を行います
        Vector3 cameraPosition = Camera.main.transform.position;
        cameraPosition.y = 0.5f;
        var characterMoveJob = new CharacterMoveJob()
        {
            deltaTime = Time.deltaTime,
            realtimeSinceStartup = Time.realtimeSinceStartup,
            cameraPosition = cameraPosition
        };
        nextFrameSyncHandle = characterMoveJob.Schedule(transformAccessArray);
        JobHandle.ScheduleBatchedJobs();
    }

}
