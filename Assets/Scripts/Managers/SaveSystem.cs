using System;
using System.IO;
using UnityEngine;

// 저장 파일 입출력. 저장 슬롯은 하나이며 JSON으로 기록합니다.
// 무엇을 저장할지는 GameManager.CaptureSave / ApplySave가 정합니다.
public static class SaveSystem
{
    const string FILE_NAME = "save.json";

    public static string SavePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    public static bool HasSave => File.Exists(SavePath);

    public static bool Save(SaveData data)
    {
        if (data == null) return false;
        try
        {
            // 쓰는 도중 꺼져도 기존 저장이 깨지지 않도록 임시 파일에 쓴 뒤 바꿔치기
            string tempPath = SavePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(tempPath, SavePath);
            Debug.Log($"💾 [저장] {SavePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ [저장 실패] {e.Message}");
            return false;
        }
    }

    public static bool TryLoad(out SaveData data)
    {
        data = null;
        if (!HasSave) return false;
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            return data != null;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ [불러오기 실패] {e.Message}");
            return false;
        }
    }

    public static void Delete()
    {
        try
        {
            if (HasSave) File.Delete(SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ [저장 삭제 실패] {e.Message}");
        }
    }
}
