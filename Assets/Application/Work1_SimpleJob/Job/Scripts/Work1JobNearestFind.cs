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
public class Work1JobNearestFind : MonoBehaviour
{
    // 掛かった時間の秒数を表示するテキストです
    public UnityEngine.UI.Text text;

    // 配列の要素数
    public int arrayNum = 10000;
    // ソースとなるポジション
    public Vector3[] sourcePositions = new Vector3[10];
    //　結果をいれます
    private Vector3[] results;

    // 探す対象の座標一覧
    private NativeArray<Vector3> targets;
    // 文字列用のバッファ
    private System.Text.StringBuilder sb = new System.Text.StringBuilder(128);


    // Jobを構造体で定義します
    struct NearestFindJob : IJob
    {
        // Jobを実行するのに必要なものを変数で定義します
        [ReadOnly]
        public Vector3 sourcePosition;

        // ジョブを実行した結果を保持します。
        // 要素が一つだけなのですが、 NativeArray経由しないと 結果を戻せません
        // ※Vector3 resultBuffer;でやるとうまくいきません
        [WriteOnly]
        public NativeArray<Vector3> resultBuffer;

        [ReadOnly]
        public NativeArray<Vector3> targets;
        // こっちはうまくいきません
        // これは Executeが呼び出されるのは、オリジナルからコピーしたインスタンス上で実行されるからです。
        [WriteOnly]
        private Vector3 result;

        // 実行します
        public void Execute()
        {
            result = Work1JobNearestFind.FindNearestPosition(sourcePosition, targets);
            resultBuffer[0] = result;
        }
    }

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
        // 結果格納先を初期化しておきます
        results = new Vector3[sourcePositions.Length];
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
        sb.Append("実行時間").Append( (end - start) * 1000.0f).Append("m秒\n").
            Append("要素数").Append(arrayNum).Append("を").Append(sourcePositions.Length).Append("回 実行しました");
        text.text = sb.ToString();
        //結果をデバッグログで
        sb.Length = 0;
        for (int i = 0; i < sourcePositions.Length; ++i)
        {
            sb.Append(i).Append(":").Append(sourcePositions[i]).Append("に一番近いのは").Append(results[i]).Append("\n");
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 計算のコア部分です。
    /// [課題] 今は 全てシングルコアでしていますが、これを並行処理できるようにしてみましょう
    /// </summary>
    private void CalculateCore()
    {
        JobHandle[] jobHandles = new JobHandle[sourcePositions.Length];
        var jobs = new NearestFindJob[sourcePositions.Length];

        for (int i = 0; i < sourcePositions.Length; ++i)
        {
            jobs[i] = new NearestFindJob()
            {
                resultBuffer = new NativeArray<Vector3>(1, Allocator.Temp),
                sourcePosition = sourcePositions[i],
                targets = targets,
            };
            jobHandles[i] = jobs[i].Schedule();
        }
        // [重要]Scheduleしたジョブをただちに開始するように促します
        JobHandle.ScheduleBatchedJobs();

        // Jobが終わるのをまち結果を取り出します
        for (int i = 0; i < jobHandles.Length; ++i)
        {
            jobHandles[i].Complete();
            this.results[i] = jobs[i].resultBuffer[0];
            jobs[i].resultBuffer.Dispose(); // <- 書き出しようのバッファーをクリアします
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
