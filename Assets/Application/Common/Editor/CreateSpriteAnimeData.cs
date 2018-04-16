using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CreateSpriteAnimeData : EditorWindow {
    
    private const string PathFilter = "Assets";

    private string targetFile;

    [MenuItem("Tools/CreateSpriteAnimeData")]
    public static void Create()
    {
        CreateSpriteAnimeData.GetWindow<CreateSpriteAnimeData>();
	}

    void OnGUI()
    {
        EditorGUILayout.LabelField("Spriteデータベース作成君");
        EditorGUILayout.LabelField("");
        targetFile = EditorGUILayout.TextField("作成するSpriteデータベース指定", targetFile);
        if (GUILayout.Button("作成"))
        {
            CreateData(targetFile);
            AssetDatabase.Refresh();
        }
    }

    private static void CreateData( string nameHead){
        string path = "Assets/Application/Common/Datas/" + nameHead + ".asset";
        var instance = AssetDatabase.LoadAssetAtPath<AppAnimationInfo>( path );
        bool isExists = (instance != null);
        if (!isExists)
        {
            instance = Editor.CreateInstance<AppAnimationInfo>();
        }

        instance.sprites = GetTargetSprites(nameHead);
        if (!isExists)
        {
            AssetDatabase.CreateAsset(instance, path );
        }else{
            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();
        }
    }


    private static Sprite[] GetTargetSprites( string nameHead)
    {
        List<Sprite> sprites = new List<Sprite>();
        var guids = AssetDatabase.FindAssets("t:Sprite");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if ( !string.IsNullOrEmpty(PathFilter) && !path.StartsWith(PathFilter))
            {
                continue;
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null && sprite.name.StartsWith(nameHead))
            {
                sprites.Add(sprite);
            }
        }
        sprites.Sort( (a,b)=>{
            return a.name.CompareTo(b.name);
        });
        return sprites.ToArray();
    }

}
