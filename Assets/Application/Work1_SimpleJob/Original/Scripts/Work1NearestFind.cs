using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// native container関連
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
//job関連
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;


/// <summary>
/// 複数の配列の中から最も近いVector3を探します
/// </summary>
public class Work1NearestFind : MonoBehaviour
{
    // 掛かった時間の秒数を表示するテキストです
    public UnityEngine.UI.Text text;

    // 配列の要素数
    public int arrayNum = 10000;
    // ソースとなるポジション
    public Vector3[] sourcePositions = new Vector3[10];

    // 探す対象の座標一覧
    private NativeArray<Vector3> targets;
    // 文字列用のバッファ
    private System.Text.StringBuilder sb = new System.Text.StringBuilder(128);

    /// <summary>
    /// 初期化時に配列を初期化しておきます
    /// </summary>
    void Start()
    {
        // Vector3の配列を NativeArrayとして確保します。
        // この配列は、このオブジェクトが存在する限りはいてほしいので、 Allocator.Persistentを指定します
        targets = new NativeArray<Vector3>(this.arrayNum, Allocator.Persistent);
        for (int i = 0; i < this.arrayNum; ++i)
        {
            targets[i] = new Vector3(Random.Range(-100.0f, 100.0f), Random.Range(-100.0f, 100.0f), Random.Range(-100.0f, 100.0f));
        }
    }

    /// <summary>
    /// Native Arrayは Disposeしないと、メモリリークが発生します
    /// </summary>
    void OnDestroy()
    {
        // [試しに]試しにコメントアウトしてみましょう
        // プレイが終了したタイミングでエラーメッセージが出るはずです。
        // このエラーメッセージはメモリリークしていることを教えてくれています
        targets.Dispose();
    }


    /// <summary>
    /// 一番近いポジションを取得してきて、結果を表示する部分
    /// </summary>
    public void Calculate()
    {
        float start = Time.realtimeSinceStartup;
        this.CalculateCore();
        float end = Time.realtimeSinceStartup;
        sb.Length = 0;
        sb.Append("実行時間").Append((end - start) * 1000.0f).Append("m秒\n").
            Append("要素数").Append(arrayNum).Append("を").Append(sourcePositions.Length).Append("回 実行しました");
        text.text = sb.ToString();
    }

    /// <summary>
    /// 計算のコア部分です。
    /// [課題] 今は 全てシングルコアでしていますが、これを並行処理できるようにしてみましょう
    /// </summary>
    private void CalculateCore()
    {
        for (int i = 0; i < sourcePositions.Length; ++i)
        {
            // これを一つのJobにして並行処理できるようにしましょう
            var result = FindNearestPosition(sourcePositions[i], targets);
            Debug.Log( sourcePositions[i] + "に一番近いのは" + result ); // <- Jobの中でもDebug.Logは呼び出して書けます
        }
    }

    /// <summary>
    /// 最も近いポジションを探して返します
    /// </summary>
    public static Vector3 FindNearestPosition(Vector3 position, NativeArray<Vector3> target)
    {
        float nearestDistance = float.MaxValue;
        Vector3 nearestPosition = new Vector3();
        for (int i = 0; i < target.Length; ++i)
        {
            var distance = (target[i] - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestPosition = target[i];
                nearestDistance = distance;
            }
        }
        return nearestPosition;
    }
}
