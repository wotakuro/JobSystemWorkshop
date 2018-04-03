using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


public class Work4CharaManager : MonoBehaviour
{
    public GameObject prefab;
    public AnimationInfo running;
    public Material drawMaterial;

    public int unityChanNum = 100;

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

    private CommandBuffer commandBuffer;

    // Use this for initialization
    void Start()
    {
        running.Initialize();

        boardRenderers = new BoardRenderer[unityChanNum];
        characterTransforms = new Transform[unityChanNum];
        velocities = new NativeArray<Vector3>(unityChanNum, Allocator.Persistent);
        drawParameter = new NativeArray<Rect>(unityChanNum, Allocator.Persistent);
        animationRectInfo = new NativeArray<Rect>(running.Length, Allocator.Persistent);
        for (int i = 0; i < running.Length; ++i)
        {
            animationRectInfo[i] = running.GetUvRect(i);
        }

        var material = new Material(drawMaterial);

        material.mainTexture = running.texture;
        for (int i = 0; i < unityChanNum; ++i)
        {
            var gmo = GameObject.Instantiate(prefab, new Vector3(Random.RandomRange(-InitPosXParam, InitPosXParam), 0.5f, Random.RandomRange(-InitPosZParam, InitPosZParam)), Quaternion.identity);
            characterTransforms[i] = gmo.transform;
            boardRenderers[i] = gmo.GetComponent<BoardRenderer>();
            boardRenderers[i].SetMaterial(material );
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
        animationRectInfo.Dispose();
        velocities.Dispose();
        drawParameter.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        cameraPosition.y = 0.5f;

        // 全キャラクター分 進行方向にRayを飛ばします。
        // 何かにぶつかったら180°進行方向を変えます
        for (int i = 0; i < unityChanNum; ++i)
        {
            if (Physics.Raycast(new Ray(characterTransforms[i].position, velocities[i]), Time.deltaTime * 3.0f))
            {
                velocities[i] = -velocities[i];
            }
        }
        // 全キャラクターの移動処理を行います
        for (int i = 0; i < unityChanNum; ++i)
        {
            characterTransforms[i].position = characterTransforms[i].position + velocities[i] * Time.deltaTime;
            characterTransforms[i].rotation = Quaternion.LookRotation(characterTransforms[i].position - cameraPosition); // <- カメラを常に向くようにします
        }

        //キャラクターのアニメーション更新を行います
        int animationLength = running.animationLength ;
        for (int i = 0; i < unityChanNum; ++i)
        {
            // プレイヤーの向いてる向きに応じて、絵を切り替えます
            var cameraDir = characterTransforms[i].position - Camera.main.transform.position;
            cameraDir.y = 0.0f;
            Quaternion cameraRotate = Quaternion.FromToRotation(cameraDir, Vector3.forward);
            int direction = AnimationInfo.GetDirection(cameraRotate * velocities[i]);//<-カメラと、キャラクターの向きを考慮してどの向きを向くかを決定します

            int rectIndex = ((int)(i * 0.3f + Time.realtimeSinceStartup * 25.0f)) % animationLength + (direction * animationLength);
            this.drawParameter[i] = animationRectInfo[rectIndex];
        }

        // 最後にTexture内の描画領域をセットします(これはMainThreadじゃないと出来ません)
        for (int i = 0; i < unityChanNum; ++i)
        {
            boardRenderers[i].SetRect(drawParameter[i]);
        }
    }

}
