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
public class Work2CharaManager : MonoBehaviour
{

    // ランダム出現位置に関するぱらえーた
    private const float InitPosXParam = 22.5f;
    private const float InitPosZParam = 15.0f;

    //GPUインスタンシングで一括でかく数
    private const int InstanceDrawNum = 500;
    // ランニング用アニメーションの情報
    public AnimationInfo animationInfo;
    // 描画用のメッシュ
    public Mesh drawMesh;
    // 描画用のマテリアル
    public Material drawMaterial;

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

    // 実際に描画する方法のマテリアル
    private Material actualMaterial;
    // zprepass用のマテリアル
    private Material zprepassMaterial;

    // ZPrepass用のコマンド
    private CommandBuffer zPrepassCommandBuffer;

    // 実際の描画コマンド
    private CommandBuffer actualCommandBuffer;

    // フレーム数カウント
    private int frameIndex = 0;

    /// <summary>
    /// Start関数
    /// </summary>
    void Start()
    {
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

        // 描画マテリアル
        actualMaterial = new Material(drawMaterial);
        actualMaterial.mainTexture = animationInfo.texture;
    }

    /// <summary>
    /// 更新処理
    /// </summary>
    void Update()
    {
        characterVelocity.y = 0.0f;
        if (frameIndex == 2)
        {
            AddCommandBuffer();
        }
        ++frameIndex;
        Vector3 cameraPosition = Camera.main.transform.position;
        int animationLength = animationInfo.animationLength;
        float realtimeSinceStartup = Time.realtimeSinceStartup;
        float deltaTime = Time.deltaTime;

        // ここを IJobParallelForを利用して並行処理にします
        for (int i = 0; i < characterNum;++i )
        {
            // 移動処理
            characterPosition[i] = characterPosition[i] + characterVelocity * deltaTime;
            // はみ出し処理対応
            if (characterPosition[i].z < -InitPosZParam)
            {
                characterPosition[i] = new Vector3(characterPosition[i].x, characterPosition[i].y, InitPosZParam);
            }
            else if (characterPosition[i].z >InitPosZParam)
            {
                characterPosition[i] = new Vector3(characterPosition[i].x, characterPosition[i].y, -InitPosZParam);
            }
            if (characterPosition[i].x < -InitPosXParam)
            {
                characterPosition[i] = new Vector3(InitPosXParam, characterPosition[i].y, characterPosition[i].z);
            }
            else if (characterPosition[i].x > InitPosXParam)
            {
                characterPosition[i] = new Vector3(-InitPosXParam, characterPosition[i].y, characterPosition[i].z);
            }  
            // マトリックス計算
            characterMatrices[i] = CreateMatrix( characterPosition[i], cameraPosition );
            // 向きを計算
            Vector3 forwardFromCamera = GetVectorFromCamera(cameraPosition ,characterPosition[i],characterVelocity);
            int direction = AnimationInfo.GetDirection(forwardFromCamera);//<-カメラと、キャラクターの向きを考慮してどの向きを向くかを決定します
            int frameIdx = direction * animationLength;
            int rectIndex = ((int)(i * 0.3f + realtimeSinceStartup * 25.0f)) % animationLength + (direction * animationLength);
            characterDrawParameter[i] = animationVectorInfo[rectIndex];
        }
        // 描画周り
        this.DrawCommandsCreate();
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
        int drawNum = 0;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < InstanceDrawNum; ++i)
        {
            if (startIdx + i >= characterNum)
            {
                break;
            }
            instancedBufferForMatrics[i] = this.characterMatrices[startIdx + i];
            instancedBufferForRects[i] = this.characterDrawParameter[startIdx + i];
            ++drawNum;
        }
        if (drawNum == 0) { return 0; }
        block.SetVectorArray(ShaderNameHash.RectValue, instancedBufferForRects);
        zPrepassCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, this.actualMaterial, 0, instancedBufferForMatrics, drawNum, block);
        actualCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, this.actualMaterial, 1, instancedBufferForMatrics, drawNum, block);
        return drawNum;
    }


    /// <summary>
    /// 破棄時の処理
    /// </summary>
    void OnDestroy()
    {
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
        }
        else
        {
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, zPrepassCommandBuffer);
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, actualCommandBuffer);
        }
    }
}
