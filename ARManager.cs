using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Vuforia;
using System.Xml;
using System.Linq;
[System.Serializable]
public struct PathConfig { public string datPath; public string xmlPath; }
[System.Serializable]
public struct ImageSize { public string id; public Vector2 size; }
[System.Serializable]
public class ARDataSet
{

    public string id;
    public TextAsset dat;
    public TextAsset xml;
    public PathConfig diskPath;
    public DataSet VuforiaDataSet;
    public List<ImageSize> ImageSizeList;
    public ARDataSet SetDatAsset(TextAsset Dat)
    {
        dat = Dat;
        return this;
    }
    public ARDataSet SetXmlAsset(TextAsset Xml)
    {
        xml = Xml;
        return this;
    }

    public ARDataSet SingleLoadFromResources(string resourcesPath,string aid)
    {
        id = aid;
        dat = Resources.Load<TextAsset>(resourcesPath + "/" + aid + "_dat");
        xml = Resources.Load<TextAsset>(resourcesPath + "/" + aid + "_xml");

        ImageSizeList = new List<ImageSize>();
        XmlDocument ARConfig = new XmlDocument();
        ARConfig.LoadXml(xml.text);
       foreach (XmlNode nd in ARConfig.ChildNodes[1].SelectSingleNode("Tracking").ChildNodes)
        {
            if(nd.Name != "ImageTarget")
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
            ImageSizeList.Add(ImageObject);
        }

        return this;
    }

    public ARDataSet OpenInDisk(string path)
    {
        diskPath = new PathConfig();
        diskPath.datPath = path + "/" + dat.name.Replace("_dat", ".dat");
        diskPath.xmlPath = path + "/" + xml.name.Replace("_xml", ".xml");
        if (File.Exists(diskPath.datPath)) { File.Delete(diskPath.datPath); }
        if (File.Exists(diskPath.xmlPath)) { File.Delete(diskPath.xmlPath); }
        File.WriteAllBytes(diskPath.datPath, dat.bytes);
        File.WriteAllText(diskPath.xmlPath, xml.text);
        return this;
    }

    public ARDataSet CreateVuforiaDataSet(ObjectTracker targetTracker)
    {
        targetTracker.Stop();
        VuforiaDataSet = targetTracker.CreateDataSet();
        VuforiaDataSet.Load(diskPath.xmlPath, VuforiaUnity.StorageType.STORAGE_ABSOLUTE);
        targetTracker.ActivateDataSet(VuforiaDataSet);
        targetTracker.Start();
        return this;
    }
}

[RequireComponent(typeof(Camera))]
public class ARManager : MonoBehaviour
{


    [Header("Resources中的地址,留空则不从此处加载")]
    public string LoadLocalPath = "DataSet";
    [Header("AR资源配置文件")]
    public AssetConfig ARAssetsConfig;
    public List<ARDataSet> ARDataSets;


    Camera selfCam = null;
    public Camera ARCam
    {
        get{if(selfCam == null) { selfCam = GetComponent<Camera>(); }return selfCam; }
    }
    ObjectTracker oTracker;

    // Start is called before the first frame update
    void Start()
    {
        //InitVideoRecord();
        //VuforiaRuntime.Instance.InitPlatform(this);
       VuforiaRuntime.Instance.InitVuforia();
        ARCam.gameObject.AddComponent<VuforiaBehaviour>();
        ARCam.clearFlags = CameraClearFlags.SolidColor;
        oTracker = TrackerManager.Instance.InitTracker<ObjectTracker>();
        CameraDevice.Instance.Init();
        CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
        oTracker.Stop();
        if (LoadLocalPath != "")
        {
            LoadFromResources(LoadLocalPath);
            DuplicateToDisk(Application.persistentDataPath);
        }
        oTracker.Start();
        InitImageTarget();
        CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
    }

    public void LoadFromResources(string localPath)
    {
        Dictionary<string, ARDataSet> DataSetList = new Dictionary<string, ARDataSet>();
        TextAsset[] ARDatas = Resources.LoadAll<TextAsset>(localPath);
        ARDataSets = new List<ARDataSet>();
        foreach (TextAsset tAsset in ARDatas)
        {
            string key = tAsset.name.Replace("_xml", "").Replace("_dat", "");
            if (!DataSetList.ContainsKey(key))
            {
                ARDataSet data = new ARDataSet();
                DataSetList.Add(key,
                data.SingleLoadFromResources(localPath, key)
                );
            }
        }
        ARDataSets = new List<ARDataSet>(DataSetList.Values);
    }

    public void DuplicateToDisk(string targetPath)
    {
        Debug.LogWarning(targetPath);
        foreach(ARDataSet dataset in ARDataSets)
        {
            dataset.OpenInDisk(targetPath).CreateVuforiaDataSet(oTracker);
        }
    }

    void InitImageTarget()
    {
        List<ImageSize> sizeList = new List<ImageSize>();
        Dictionary<string, ImageSize> sizeHash = new Dictionary<string, ImageSize>();
        foreach(ARDataSet data in ARDataSets)
        {
            sizeList.AddRange(data.ImageSizeList);
        }
        foreach (ImageSize imageSize in sizeList)
        {
            sizeHash.Add(imageSize.id, imageSize);
        }

        
        foreach (DataSetTrackableBehaviour target in FindObjectsOfType<DataSetTrackableBehaviour>())
        {
            if(!sizeHash.ContainsKey(target.TrackableName)) 
            {
                Debug.LogWarning("不包含:" + target.TrackableName);
                continue;
            }
            ARDataSet arset = new List<ARDataSet> (from ARDataSet tdset in ARDataSets
                              where tdset.ImageSizeList.Contains(sizeHash[target.TrackableName])
                              select tdset)[0];

            target.gameObject.name = target.TrackableName;


            DataSetting tData = new List<DataSetting>(from DataSetting sData in ARAssetsConfig.TrackAssets where sData.xmlName.StartsWith(arset.id) select sData)[0];
            TrackTarget tTrack = new List<TrackTarget>(from TrackTarget sTrack in tData.targetObjects where sTrack.id == target.TrackableName select sTrack)[0];

            if (tTrack.prefab == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Plane); //Instantiate(AREffect); 
                cube.GetComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("PlaneMaterial");
                cube.transform.parent = target.transform;
                cube.transform.localPosition = Vector3.zero;
                cube.transform.rotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, 0.0f));
                cube.transform.localScale = 0.1f * (new Vector3(1.0f, 1.0f, 1.0f * (sizeHash[target.TrackableName].size.y / sizeHash[target.TrackableName].size.x)));
                target.gameObject.AddComponent<DefaultTrackableEventHandler>();
            }
            else
            {
                GameObject prefab =Instantiate(tTrack.prefab); //Instantiate(AREffect); 
                prefab.transform.parent = target.transform;
                prefab.transform.localPosition = tTrack.localOffset*0.1f;
                prefab.transform.rotation =   Quaternion.Euler(new Vector3(180.0f,0.0f,180.0f)+ tTrack.localRotation.eulerAngles);
                prefab.transform.localScale = 0.1f *(new Vector3(tTrack.localScale.x, tTrack.localScale.y, tTrack.localScale.x));
                target.gameObject.AddComponent<DefaultTrackableEventHandler>();
            }
        }
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
        }
    }
}
