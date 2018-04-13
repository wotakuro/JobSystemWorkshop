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
    // ランニング用アニメーションの情報
    public AnimationInfo runningAnimationInfo;
    // 攻撃アニメーションの情報
    public AnimationInfo attackingAnimationInfo;
    // 攻撃アニメーションの情報
    public AnimationInfo goDownAnimationInfo;

    // 描画用のメッシュ
    public Mesh drawMesh;
    // 描画用のマテリアル
    public Material drawMaterial;

    // キャラクター数
    public int characterNum = 2000;

    //GPUインスタンシングで一括でかく数
    private const int InstanceDrawNum = 500;

    // GPU インスタンスで一気に書くときのバッファー
    private Matrix4x4[] instancedMatrics = new Matrix4x4[InstanceDrawNum];
    private Vector4[] instancedRect = new Vector4[InstanceDrawNum];

    // 実際に描画する方法のマテリアル
    private Material actualMaterial;

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
        runningAnimationInfo.Initialize();
        attackingAnimationInfo.Initialize();
        goDownAnimationInfo.Initialize();
        // それぞれのバッファーを初期化/作成
        zPrepassCommandBuffer = new CommandBuffer();
        actualCommandBuffer = new CommandBuffer();


        // 描画マテリアル
        actualMaterial = new Material(drawMaterial);
        actualMaterial.mainTexture = runningAnimationInfo.texture;
    }

    /// <summary>
    /// 更新処理
    /// </summary>
    void Update()
    {
        if (frameIndex == 2)
        {
            AddCommandBuffer();
        }
        ++frameIndex;
        for (int i = 0; i < instancedMatrics.Length;++i )
        {
            instancedMatrics[i] = Matrix4x4.Translate(Vector3.left * i);
            instancedRect[i] = new Vector4(0, 0.01f * i, 0, 0);
        }
        // キャラをパラパラ動かします
//        actualMaterial.SetPass(0);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetVectorArray(ShaderNameHash.RectValue, instancedRect);

        zPrepassCommandBuffer.Clear();
        actualMaterial.SetPass(0);
        zPrepassCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, this.actualMaterial, 0, instancedMatrics, InstanceDrawNum, block);

        actualMaterial.SetPass(1);
        actualCommandBuffer.Clear();
        actualCommandBuffer.DrawMeshInstanced(this.drawMesh, 0, this.actualMaterial, 0, instancedMatrics, InstanceDrawNum, block);
    }

    /// <summary>
    /// 破棄時の処理
    /// </summary>
    void OnDestroy()
    {

        if (MyScriptableRenderPipelineInstance.Instance != null)
        {
            MyScriptableRenderPipelineInstance.Instance.zPrepassCommandBuffers = null;
            MyScriptableRenderPipelineInstance.Instance.actualCommandBuffers = null;
        }
        else if (Camera.main != null)
        {
            Camera.main.RemoveAllCommandBuffers();
        }

     //   this.actualCommandBuffer.Dispose();
     //   this.zPrepassCommandBuffer.Dispose();
    }

    private void AddCommandBuffer()
    {
        if (MyScriptableRenderPipelineInstance.Instance != null)
        {
            MyScriptableRenderPipelineInstance.Instance.zPrepassCommandBuffers = zPrepassCommandBuffer;
            MyScriptableRenderPipelineInstance.Instance.actualCommandBuffers = actualCommandBuffer;
        }
        else
        {
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, zPrepassCommandBuffer);
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, actualCommandBuffer);
        }
    }
}
