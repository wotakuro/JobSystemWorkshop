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
public class Work2JobCharaManager : MonoBehaviour
{

    /// <summary>
    /// キャラクターの更新処理
    /// </summary>
    private struct CharacterUpdateJob : IJobParallelFor
    {
        //キャラクター位置
        public NativeArray<Vector3> characterPosition;
        // 描画用のMatrix
        [WriteOnly]
        public NativeArray<Matrix4x4> characterMatrices;
        // 描画用のパラメーター
        [WriteOnly]
        public NativeArray<Vector4> characterDrawParameter;

        // Animation用の描画領域
        [ReadOnly]
        public NativeArray<Vector4> animationVectorInfo;

        // キャラクターの位置
        public Vector3 characterVelocity;
        // Cameraの位置
        public Vector3 cameraPosition;
        // Animationの長さ
        public int animationLength;
        // 時間処理用
        public float realtimeSinceStartup;
        public float deltaTime;

        /// <summary>
        /// 実行用の関数
        /// </summary>
        /// <param name="index"> index </param>
        public void Execute(int index)
        {
            // 移動処理
            characterPosition[index] = characterPosition[index] + characterVelocity * deltaTime;
            // はみ出し処理対応
            if (characterPosition[index].z < -InitPosZParam)
            {
                characterPosition[index] = new Vector3(characterPosition[index].x, characterPosition[index].y, InitPosZParam);
            }
            else if (characterPosition[index].z > InitPosZParam)
            {
                characterPosition[index] = new Vector3(characterPosition[index].x, characterPosition[index].y, -InitPosZParam);
            }
            if (characterPosition[index].x < -InitPosXParam)
            {
                characterPosition[index] = new Vector3(InitPosXParam, characterPosition[index].y, characterPosition[index].z);
            }
            else if (characterPosition[index].x > InitPosXParam)
            {
                characterPosition[index] = new Vector3(-InitPosXParam, characterPosition[index].y, characterPosition[index].z);
            }
            // マトリックス計算
            characterMatrices[index] = CreateMatrix(characterPosition[index], cameraPosition);
            // 向きを計算
            Vector3 forwardFromCamera = GetVectorFromCamera(cameraPosition, characterPosition[index], characterVelocity);
            int direction = AppAnimationInfo.GetDirection(forwardFromCamera);//<-カメラと、キャラクターの向きを考慮してどの向きを向くかを決定します
            int frameIdx = direction * animationLength;
            int rectIndex = ((int)(index * 0.3f + realtimeSinceStartup * 25.0f)) % animationLength + (direction * animationLength);
            characterDrawParameter[index] = animationVectorInfo[rectIndex];
        }
    }

    // ランダム出現位置に関するぱらえーた
    private const float InitPosXParam = 22.5f;
    private const float InitPosZParam = 15.0f;

    //GPUインスタンシングで一括でかく数
    private const int InstanceDrawNum = 500;
    // ランニング用アニメーションの情報
    public AppAnimationInfo animationInfo;
    // 描画用のメッシュ
    public Mesh drawMesh;
    // 描画用のマテリアル
    public Material drawCharaMaterial;
    // 影描画用のマテリアル
    public Material drawShadowMaterial;

    // キャラクター数
    public int characterNum = 1800;
    // 速度
    public Vector3 characterVelocity = new Vector3(0.0f, 0.0f, 1.0f);
    public Rect playerSpawnRect = new Rect(-14, -14, 14, 14);

    // 描画に使用するマトリックス
    private NativeArray<Matrix4x4> characterMatrices;
    // 各キャラクターの移動速度
    private NativeArray<Vector3> characterPosition;
    // 各キャラクターの描画用する矩形領域
    private NativeArray<Vector4> characterDrawParameter;
    // Animationの矩形情報
    private NativeArray<Vector4> animationVectorInfo;

    // GPU インスタンスで一気に書くときのバッファー
    private Matrix4x4[] instancedBufferForMatrics = new Matrix4x4[InstanceDrawNum];
    // GPU インスタンスで一気に書くときのバッファー
    private Vector4[] instancedBufferForRects = new Vector4[InstanceDrawNum];
    // GPU インスタンスで一気に書くときのバッファー
    private Matrix4x4[] matrixBufferForShadow = new Matrix4x4[InstanceDrawNum];

    // 実際に描画する方法のマテリアル
    private Material actualMaterial;
    // zprepass用のマテリアル
    private Material zprepassMaterial;
    // material property
    private MaterialPropertyBlock materialBlock;

    // ZPrepass用のコマンド
    private CommandBuffer zPrepassCommandBuffer;

    // 実際の描画コマンド
    private CommandBuffer actualCommandBuffer;

    // 影用の描画コマンド
    private CommandBuffer shadowCommandBuffer;

    // フレーム数カウント
    private int frameIndex = 0;

    // Jobのハンドル
    private JobHandle jobHandle;


    /// <summary>
    /// Start関数
    /// </summary>
    void Start()
    {
        // Debugger登録
        DebugUI.SetCounter(animationInfo, characterNum);
        // animation の情報初期化
        animationInfo.Initialize();
        animationVectorInfo = new NativeArray<Vector4>(animationInfo.Length, Allocator.Persistent);
        for (int i = 0; i < animationInfo.Length; ++i)
        {
            var rect = animationInfo.GetUvRect(i);
            animationVectorInfo[i] = new Vector4(rect.x, rect.y, rect.width, rect.height);
        }
        // キャラクター用のバッファー
        characterPosition = new NativeArray<Vector3>(characterNum, Allocator.Persistent);
        characterDrawParameter = new NativeArray<Vector4>(characterNum,Allocator.Persistent);
        characterMatrices = new NativeArray<Matrix4x4>(characterNum, Allocator.Persistent);
        // キャラクターを適当な位置に配置します
        for (int i = 0; i < characterNum; ++i)
        {
            characterPosition[i] = new Vector3(Random.RandomRange(playerSpawnRect.x, playerSpawnRect.x + playerSpawnRect.width), 0.5f, Random.RandomRange(playerSpawnRect.y, playerSpawnRect.y + playerSpawnRect.height));
        }

        // CommandBuffer関連の作成
        zPrepassCommandBuffer = new CommandBuffer();
        actualCommandBuffer = new CommandBuffer();
        shadowCommandBuffer = new CommandBuffer();

        // 描画マテリアル
        actualMaterial = new Material(drawCharaMaterial);
        actualMaterial.mainTexture = animationInfo.texture;
        materialBlock = new MaterialPropertyBlock();
        //影用のMatrixを事前に入れられるところを入れておく
        for (int i = 0; i < InstanceDrawNum; ++i)
        {
            this.matrixBufferForShadow[i] = Matrix4x4.identity;
            // Xのスケール調整
            this.matrixBufferForShadow[i].m00 = 0.4f;
            // Y軸をz軸に置換
            this.matrixBufferForShadow[i].m11 = 0.0f;
            this.matrixBufferForShadow[i].m21 = 0.4f;
            // Y座標値をセット
            this.matrixBufferForShadow[i].m13 = 0.1f;
        }
    }

    /// <summary>
    /// 更新処理
    /// </summary>
    void Update()
    {
        // 前のフレームで出した結果を待ちます
        jobHandle.Complete();
        characterVelocity.y = 0.0f;
        if (frameIndex == 2)
        {
            AddCommandBuffer();
        }
        ++frameIndex;

        // 描画周り
        this.DrawCommandsCreate();
        // 影の描画処理
        this.DrawShadows();


        // Jobを作成して発行します。
        CharacterUpdateJob job = new CharacterUpdateJob(){
            characterPosition = characterPosition,
            characterMatrices = characterMatrices,
            characterDrawParameter = characterDrawParameter,
            animationVectorInfo = animationVectorInfo,
            characterVelocity = characterVelocity,
            cameraPosition = Camera.main.transform.position,
            animationLength = animationInfo.animationLength,
            realtimeSinceStartup = Time.realtimeSinceStartup,
            deltaTime = Time.deltaTime
        };
        jobHandle = job.Schedule(characterNum, 10);
        JobHandle.ScheduleBatchedJobs();
    }

    /// <summary>
    /// 実際の描画周り処理
    /// </summary>
    private void DrawCommandsCreate()
    {
        // キャラをパラパラ動かします

        // 実際の描画コマンド
        zPrepassCommandBuffer.Clear();
        actualCommandBuffer.Clear();

        int startIdx = 0;
        int addNum = characterNum / InstanceDrawNum + 1;
        for (int i = 0; i < addNum; ++ i )
        {
            int num = AddToCommandBuffer(startIdx);
            startIdx += num;
        }
    }
    /// <summary>
    /// 描画コマンドをバッファーに突っ込みます
    /// </summary>
    /// <param name="startIdx"></param>
    private int AddToCommandBuffer(int startIdx)
    {
        int drawNum = Mathf.Min( InstanceDrawNum , characterNum - startIdx);

        UnsafeFastCopy.Copy(characterMatrices, instancedBufferForMatrics, startIdx, 0, drawNum);
        UnsafeFastCopy.Copy(characterDrawParameter, instancedBufferForRects, startIdx, 0, drawNum);

        if (drawNum == 0) { return 0; }

        materialBlock.SetVectorArray(ShaderNameHash.RectValue, instancedBufferForRects);
        zPrepassCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, this.actualMaterial, 0, instancedBufferForMatrics, drawNum, materialBlock);
        actualCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, this.actualMaterial, 1, instancedBufferForMatrics, drawNum, materialBlock);
        return drawNum;
    }

    /// <summary>
    /// 影の描画を行います
    /// </summary>
    private void DrawShadows()
    {
        shadowCommandBuffer.Clear();
        int drawNum = 0;
        for (int i = 0; i < characterNum; ++i)
        {

            this.matrixBufferForShadow[drawNum].m03 = characterPosition[i].x;
            this.matrixBufferForShadow[drawNum].m23 = characterPosition[i].z;
            ++drawNum;
            if (drawNum >= InstanceDrawNum)
            {
                this.shadowCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, drawShadowMaterial, 0, matrixBufferForShadow, drawNum);
                drawNum = 0;
            }
        }
        if (drawNum > 0)
        {
            this.shadowCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, drawShadowMaterial, 0, matrixBufferForShadow, drawNum);
        }
    }


    /// <summary>
    /// 破棄時の処理
    /// </summary>
    void OnDestroy()
    {
        // Jobをここで終了しておかないと正しく解放できません
        jobHandle.Complete();
        if (MyScriptableRenderPipelineInstance.Instance != null)
        {
            MyScriptableRenderPipelineInstance.Instance.zPrepassCommandBuffers.Remove(zPrepassCommandBuffer);
            MyScriptableRenderPipelineInstance.Instance.actualCommandBuffers.Remove(actualCommandBuffer);
        }
        else if (Camera.main != null)
        {
            Camera.main.RemoveAllCommandBuffers();
        }
        this.actualCommandBuffer.Dispose();
        this.zPrepassCommandBuffer.Dispose();
        this.shadowCommandBuffer.Dispose();

        // 各種NativeContainerの解放
        animationVectorInfo.Dispose();
        characterPosition.Dispose();
        characterDrawParameter.Dispose();
        characterMatrices.Dispose();
    }

    /// <summary>
    /// マトリックスを直接計算します
    /// </summary>
    private static Matrix4x4 CreateMatrix(Vector3 position, Vector3 cameraPos)
    {
        var diff = position - cameraPos;
        diff.Normalize();

        Matrix4x4 matrix = Matrix4x4.identity;
        // 向きセット
        matrix.m00 = diff.z;
        matrix.m02 = -diff.x;

        matrix.m20 = -diff.x;
        matrix.m22 = diff.z;

        // 位置セット
        matrix.m03 = position.x;
        matrix.m13 = position.y;
        matrix.m23 = position.z;
        return matrix;
    }

    /// <summary>
    /// カメラを考慮して向きを決定します
    /// </summary>
    private static Vector3 GetVectorFromCamera(Vector3 cameraPos, Vector3 charaPos, Vector3 charaForward)
    {
        Vector3 diff = charaPos - cameraPos;
        Vector3 fromCameraForward = new Vector3(
            diff.z * charaForward.x - diff.x  * charaForward.z,
            0.0f,
            diff.x * charaForward.x + diff.z  * charaForward.z );

        return fromCameraForward;
    }


    /// <summary>
    /// コマンドバッファーが実行されるように追加します
    /// </summary>
    private void AddCommandBuffer()
    {
        if (MyScriptableRenderPipelineInstance.Instance != null)
        {
            MyScriptableRenderPipelineInstance.Instance.zPrepassCommandBuffers.Add(zPrepassCommandBuffer);
            MyScriptableRenderPipelineInstance.Instance.actualCommandBuffers.Add(actualCommandBuffer);
            MyScriptableRenderPipelineInstance.Instance.shadowCommandBuffers.Add(shadowCommandBuffer);
        }
        else
        {
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, zPrepassCommandBuffer);
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, actualCommandBuffer);
            Camera.main.AddCommandBuffer(CameraEvent.AfterForwardAlpha, shadowCommandBuffer);
        }
    }
}
