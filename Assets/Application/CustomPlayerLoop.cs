using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.LowLevel;
using UnityEngine.Experimental.PlayerLoop;


// プレイヤーループのカスタマイズ
// 参考URL https://www.patreon.com/posts/unity-2018-1-16336053
public class CustomPlayerLoop {

    [RuntimeInitializeOnLoadMethod]
    static  void Init()
    {
        var loopSystem = GenerateCustomLoop();
        PlayerLoop.SetPlayerLoop( loopSystem );
    }

    private static PlayerLoopSystem GenerateCustomLoop()
    {
        // Note: this also resets the loop to its defalt state first.
        var playerLoop = PlayerLoop.GetDefaultPlayerLoop();

        for (int i = 0; i < playerLoop.subSystemList.Length; ++ i  )
        {
            var subSystem = playerLoop.subSystemList[i];
            // FixedUpdateの中身消します
            if (subSystem.type == typeof(FixedUpdate))
            {
                subSystem.subSystemList = new PlayerLoopSystem[0];
            }
            // Preupdateも大体削る
            if (subSystem.type == typeof(PreUpdate))
            {
                Debug.Log(subSystem.subSystemList[0].type);
                subSystem.subSystemList = new PlayerLoopSystem[1];
                subSystem.subSystemList[0] = new PlayerLoopSystem();
                subSystem.subSystemList[0].type = typeof( UnityEngine.Experimental.PlayerLoop.PreUpdate.CheckTexFieldInput );
            }
            // 構造体なので上書きしないとセットされないです
            playerLoop.subSystemList[i] = subSystem;
        }
        return playerLoop;
    }
}
