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


// ワーク4．キャラクターを管理するマネージャ
public class Work4CharaManager : MonoBehaviour
{
    // キャラクターのプレハブ
    public GameObject prefab;
    // アニメーションの情報
    public AppAnimationInfo animationInfo;
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
        velocities = new NativeArray<Vector3>(characterNum, Allocator.Persistent);
        drawParameter = new NativeArray<Rect>(characterNum, Allocator.Persistent);
        animationRectInfo = new NativeArray<Rect>(animationInfo.Length, Allocator.Persistent);
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
    }
    /// <summary>
    /// それぞれの NativeContainerを破棄します
    /// </summary>
    void OnDestroy()
    {
        animationRectInfo.Dispose();
        velocities.Dispose();
        drawParameter.Dispose();
    }

    /// <summary>
    /// 更新処理
    /// </summary>
    void Update()
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        cameraPosition.y = 0.5f;

        // 全キャラクター分 進行方向にRayを飛ばします。
        // 何かにぶつかったら180°進行方向を変えます
        // [課題]ここを RaycastCommandを使って並行して出来るように
        for (int i = 0; i < characterNum; ++i)
        {
            if (Physics.Raycast(new Ray(characterTransforms[i].position, velocities[i]), Time.deltaTime * 3.0f))
            {
                velocities[i] = -velocities[i];
            }
        }
        // 全キャラクターの移動処理を行います
        // [課題]ここもうまく計算処理を並行して出来るように
        for (int i = 0; i < characterNum; ++i)
        {
            characterTransforms[i].position = characterTransforms[i].position + velocities[i] * Time.deltaTime;
            characterTransforms[i].rotation = Quaternion.LookRotation(characterTransforms[i].position - cameraPosition); // <- カメラを常に向くようにします
        }

        //キャラクターのアニメーション更新を行います
        // [課題]ここもうまくJobに…
        int animationLength = animationInfo.animationLength ;
        float realtimeSinceStartup = Time.realtimeSinceStartup;
        for (int i = 0; i < characterNum; ++i)
        {
            // プレイヤーの向いてる向きに応じて、絵を切り替えます
            var cameraDir = characterTransforms[i].position - Camera.main.transform.position;
            cameraDir.y = 0.0f;
            Quaternion cameraRotate = Quaternion.FromToRotation(cameraDir, Vector3.forward);
            int direction = AppAnimationInfo.GetDirection(cameraRotate * velocities[i]);//<-カメラと、キャラクターの向きを考慮してどの向きを向くかを決定します

            int rectIndex = ((int)(i * 0.3f + realtimeSinceStartup * 25.0f)) % animationLength + (direction * animationLength);
            this.drawParameter[i] = animationRectInfo[rectIndex];
        }

        // 最後にTexture内の描画領域をセットします(これはMainThreadじゃないと出来ません)
        for (int i = 0; i < characterNum; ++i)
        {
            boardRenderers[i].SetRect(drawParameter[i]);
        }
    }

}
