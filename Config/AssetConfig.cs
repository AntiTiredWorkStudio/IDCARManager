using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct DataSetting
{
    public string xmlName;
    public List<TrackTarget> targetObjects;
}
[System.Serializable]
public struct TrackTarget
{
    [Header("识别图信息")]
    public string id;
    public float width;
    public float height;
    [Header("当前识别图的AR预制件")]
    public GameObject prefab;
    [Header("当前识别图的AR预制件局部空间变换信息")]
    public Vector3 localOffset;
    public Quaternion localRotation;
    public Vector3 localScale;
}
public class AssetConfig : ScriptableObject
{
    public static List<ImageSize> ReadARTrackXml(string text)
    {
        XmlDocument ARConfig = new XmlDocument();
        ARConfig.LoadXml(text);
        List<ImageSize> result = new List<ImageSize>();
        foreach (XmlNode nd in ARConfig.ChildNodes[1].SelectSingleNode("Tracking").ChildNodes)
        {
            if (nd.Name != "ImageTarget")
            {
                continue;
            }
            string name = (nd as XmlElement).GetAttribute("name");
            string size = (nd as XmlElement).GetAttribute("size");
            string[] sizeArray = size.Split(' ');
            Vector2 sizeValue = new Vector2(float.Parse(sizeArray[0]), float.Parse(sizeArray[1]));
            ImageSize ImageObject = new ImageSize();
            ImageObject.id = name;
            ImageObject.size = sizeValue;
            result.Add(ImageObject);
        }
        return result;
    }
#if UNITY_EDITOR

    //AssetConfig target = AssetDatabase.LoadAssetAtPath(assetPath, typeof(ScriptableObject)) as AssetConfig;
    const  string assetPath = "Assets/AssetConfig.asset";
    static AssetConfig ConfigObject
    {
        get
        {
            return AssetDatabase.LoadAssetAtPath(assetPath, typeof(ScriptableObject)) as AssetConfig;
        }
    }
    
    [MenuItem("ARAsset/MakeARCamera")]
    static void MakeARCamera()
    {
        ARManager arManager = GameObject.FindObjectOfType<ARManager>();
        if(arManager != null)
        {
            Debug.LogWarning("已经存在ARManager");
            Selection.activeObject = arManager.gameObject;
        }
        else
        {
            if (Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<ARManager>();
                Selection.activeObject = Camera.main.gameObject;
                Debug.LogWarning("添加成功");
            }
            else{
                GameObject camObject = GameObject.FindObjectOfType<Camera>().gameObject;
                if(camObject == null)
                {
                    Debug.LogWarning("场景中需添加摄像机");
                    return;
                }
                else
                {
                    camObject.AddComponent<ARManager>();
                    Selection.activeObject = camObject;
                    Debug.LogWarning("添加成功");
                }
            }
        }
    }

    [MenuItem("ARAsset/MakeARResources")]
    static void MakeARResources()
    {
        ARManager armanager = GameObject.FindObjectOfType<ARManager>();
        if (armanager == null) { Debug.LogError("需配置ARManager"); return; }
        string sourcesPath = Application.streamingAssetsPath + "/Vuforia";
        if(armanager.LoadLocalPath == "")
        {
            Selection.activeObject = armanager.gameObject;
            Debug.LogError("DataSet路径为空");
            return;
        }
        string resourcesPath = Application.dataPath + "/Resources/" + armanager.LoadLocalPath;
        List<string> files = new List<string>(from string file in Directory.GetFiles(sourcesPath) where !file.EndsWith(".meta") && (file.EndsWith(".xml") || file.EndsWith(".dat")) select file);
        Dictionary<string, string> targetPath = new Dictionary<string, string>();
        foreach (string file in files)
        {
            string targetFilePath = file.Replace(sourcesPath, "");
            targetFilePath = targetFilePath.Replace(".dat", "_dat.bytes");
            targetFilePath = targetFilePath.Replace(".xml", "_xml.txt");
            targetFilePath = resourcesPath + targetFilePath;
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }
            File.Copy(file, targetFilePath);
            Debug.Log(targetFilePath);
        }
        AssetDatabase.Refresh();
    }

    [MenuItem("ARAsset/MakeAssetConfig")]
    static void AssetInstance()
    {
        if(Selection.activeObject == null || Selection.objects.Length == 0)
        {
            Debug.LogError("请选择Vuforia AR配置xml文件");
        }

        List<TextAsset> rightSelection = new List<TextAsset>();
        foreach (Object selectObject in Selection.objects)
        {
            TextAsset targetText = (selectObject as TextAsset);
            if(targetText == null || targetText.text == "")
            {
                Debug.LogWarning("非法文本文件:" + selectObject.name);
                continue;
            }
            else
            {
                XmlDocument xmlDoc = new XmlDocument();
                try
                {
                    xmlDoc.LoadXml(targetText.text);
                }
                catch
                {
                    Debug.LogWarning("非法Xml格式:" + selectObject.name);
                    continue;
                }
                rightSelection.Add(targetText);
                Debug.Log("选择:" + targetText.name);
            }
        }
        Selection.objects = rightSelection.ToArray();
        if (rightSelection.Count == 0)
        {
            return;
        }

        //string assetPath = "Assets/AssetConfig.asset";
        AssetConfig target = ConfigObject;//AssetDatabase.LoadAssetAtPath(assetPath, typeof(ScriptableObject)) as AssetConfig;
        if (target != null)
        {
            Debug.LogWarning("已存在AssetConfig文件");
            target.DoAssetInstanceAction(rightSelection);

            AssetDatabase.SaveAssets();
        }
        else {
            target = ScriptableObject.CreateInstance<AssetConfig>();
            target.DoAssetInstanceAction(rightSelection);
            AssetDatabase.CreateAsset(target, assetPath);
            Debug.LogWarning("创建新AssetConfig文件");
        }
        AssetDatabase.Refresh();
    }
    static ARAssetEditManager editTarget = null;
    static GameObject EditTarget {
        get
        {
            editTarget = GameObject.FindObjectOfType<ARAssetEditManager>();
            if(editTarget == null)
            {
                GameObject editObject = new GameObject("ARAssets");
                editTarget = editObject.AddComponent<ARAssetEditManager>();
                editTarget.configObject = ConfigObject;
            }
            return editTarget.gameObject;
        }
    }
    [MenuItem("ARAsset/Open ARAssetsEditor")]
    public static void EditARAssetsAdapte()
    {
        if(ConfigObject == null)
        {
            Debug.LogError("Assets/AssetConfig.asset路径下无配置文件");
            return;
        }
        if(EditTarget!= null)
        {
            DestroyImmediate(EditTarget);
        }
        AssetConfig configFile = ConfigObject;

        float heightOffset = 0.0f;
        foreach (DataSetting data in configFile.TrackAssets)
        {
            GameObject dataObject = new GameObject(data.xmlName);
            dataObject.transform.parent = EditTarget.transform;
            dataObject.transform.localPosition = new Vector3(0.0f, 0.0f, heightOffset*10.0f);

            float currentWidth = 0.0f;
            float currentHeight = 0.0f;
            foreach (TrackTarget target in data.targetObjects)
            {
                GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                targetObject.name = target.id;
                targetObject.transform.parent = dataObject.transform;
                targetObject.transform.localScale = new Vector3(target.width, target.width, target.height);
                targetObject.transform.localPosition = new Vector3(currentWidth * 10.0f, 0.0f, 0.0f);
                currentHeight = Mathf.Max(currentHeight, target.height);
                currentWidth += (target.width + target.width*0.1f);
                Material tMat = new Material(Shader.Find("Standard"));
                string sourcesTex = "Assets/Editor/Vuforia/ImageTargetTextures/" + data.xmlName.Replace("_xml","").Replace("_dat","") + "/" + target.id + "_scaled.jpg";
                Debug.Log("sources:"+sourcesTex);
                tMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(sourcesTex));
                tMat.SetFloat("_Glossiness", 0.0f);
                targetObject.GetComponent<Renderer>().sharedMaterial = tMat;

                if(target.prefab != null)
                {
                    GameObject view = Instantiate(target.prefab) as GameObject;
                    view.name = view.name.Replace("(Clone)", "");
                    view.transform.parent = targetObject.transform;
                    view.transform.localPosition = target.localOffset;
                    view.transform.localRotation = target.localRotation;
                    view.transform.localScale = target.localScale;
                }
            }
            Debug.LogWarning(currentHeight);
            heightOffset += (currentHeight + currentHeight*0.1f);
        }
        Selection.activeObject = EditTarget;
    } 
#endif
    public List<DataSetting> TrackAssets;

    /// <summary>
    /// 创建Instance时执行动作
    /// </summary>
    /// <param name="textAssets"></param>
    public void DoAssetInstanceAction(List<TextAsset> textAssets) {
        foreach(TextAsset target in textAssets)
        {
            if (TrackNameExist(target.name))
            {
                UpdateDataSetting(target);
            }
            else
            {
                CreateDataSetting(target);
            }
        }
    }
    /// <summary>
    /// 检测是否存在识别名
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public bool TrackNameExist(string name)
    {
        if(TrackAssets == null) { return false; }
        foreach(DataSetting data in TrackAssets)
        {
            if(data.xmlName == name)
            {
                return true;
            }
        }
        return false;
    }

    public void UpdateDataSetting(TextAsset targetXml)
    {
        List<ImageSize> list = ReadARTrackXml(targetXml.text);

        List<DataSetting> dTarget =new List<DataSetting>(
            (from DataSetting dt in TrackAssets where dt.xmlName == targetXml.name select dt));
        if (dTarget.Count == 0)
        {
            DataSetting tData = new DataSetting();
            tData.targetObjects = new List<TrackTarget>();
            tData.xmlName = targetXml.name;
            //增加ImageTarget
            foreach(ImageSize imageSize in list)
            {
                TrackTarget tTarget = new TrackTarget();
                tTarget.id = imageSize.id;
                tTarget.width = imageSize.size.x;
                tTarget.height = imageSize.size.y;
                tTarget.localScale = new Vector3(1.0f,1.0f,  imageSize.size.y/ imageSize.size.x );
                tData.targetObjects.Add(tTarget);
            }
            TrackAssets.Add(tData);
        }
        else
        {
            DataSetting dataSetting = dTarget[0];
            foreach (ImageSize imageSize in list)
            {
                List<TrackTarget> selectionList = new List<TrackTarget>(
                    from TrackTarget t in dataSetting.targetObjects
                    where t.id == imageSize.id select t);
                if(selectionList.Count == 0)
                {
                    int dataSettingIndex = TrackAssets.IndexOf(dataSetting);
                    TrackTarget newTrack = new TrackTarget();
                    newTrack.id = imageSize.id;
                    newTrack.width = imageSize.size.x;
                    newTrack.height = imageSize.size.y;
                    TrackAssets[dataSettingIndex].targetObjects.Add(newTrack);
                }
            }
        }
    }

    public void CreateDataSetting(TextAsset targetXml)
    {
        List<ImageSize> list = ReadARTrackXml(targetXml.text);
        DataSetting tData = new DataSetting();
        tData.targetObjects = new List<TrackTarget>();
        tData.xmlName = targetXml.name;
        //增加ImageTarget
        foreach (ImageSize imageSize in list)
        {
            TrackTarget tTarget = new TrackTarget();
            tTarget.id = imageSize.id;
            tTarget.width = imageSize.size.x;
            tTarget.height = imageSize.size.y;
            tTarget.localScale = new Vector3(1.0f, 1.0f, imageSize.size.x/ imageSize.size.y);
            tData.targetObjects.Add(tTarget);
        }
        if(TrackAssets == null)
            TrackAssets = new List<DataSetting>();
        TrackAssets.Add(tData);
    }
}
