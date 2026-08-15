using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TeleportGatePrefabTool
{
    private const string SpriteSheetPath = "Assets/Image/teleport_door_sheet.png";
    private const string PrefabFolder = "Assets/Prefabs/Prop";
    private const string PrefabPath = PrefabFolder + "/TeleportGate.prefab";

    [MenuItem("Tools/Top Down Multi/Create Teleport Gate Prefab")]
    public static void CreateTeleportGatePrefab()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteSheetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();

        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("Teleport Gate", "Không tìm thấy sprite trong teleport_door_sheet.png.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Prop");
        }

        GameObject gate = new GameObject("TeleportGate");
        SpriteRenderer renderer = gate.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites[0];
        renderer.sortingLayerName = "Prop";
        renderer.sortingOrder = 30;

        BoxCollider2D collider = gate.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.45f, 2.15f);
        collider.offset = new Vector2(0f, 0.25f);

        TeleportGate teleportGate = gate.AddComponent<TeleportGate>();
        SerializedObject serializedObject = new SerializedObject(teleportGate);
        SerializedProperty frames = serializedObject.FindProperty("frames");
        frames.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        serializedObject.FindProperty("frameInterval").floatValue = 0.18f;
        serializedObject.FindProperty("teleportDelayAfterOpen").floatValue = 0.12f;
        serializedObject.FindProperty("playReverseOnArrival").boolValue = true;
        serializedObject.FindProperty("playerArrivalOffset").vector3Value = Vector3.down * 1.25f;
        serializedObject.FindProperty("reuseCooldown").floatValue = 1.5f;
        serializedObject.FindProperty("showLoadingScreen").boolValue = true;
        serializedObject.FindProperty("loadingBeforeMapSwitch").floatValue = 0.85f;
        serializedObject.FindProperty("loadingAfterMapSwitch").floatValue = 0.35f;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(gate, PrefabPath);
        Object.DestroyImmediate(gate);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorUtility.DisplayDialog("Teleport Gate", "Đã tạo prefab:\n" + PrefabPath, "OK");
    }
}
