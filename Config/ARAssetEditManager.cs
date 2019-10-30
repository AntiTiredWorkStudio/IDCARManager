using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public class ARAssetEditManager : MonoBehaviour
{
    public AssetConfig configObject;

    public void ConfirmChanges()
    {
        if(configObject == null)
        {
            Debug.LogError("未填写配置文件");
            return;
        }
        foreach (DataSetting datas in configObject.TrackAssets)
        {
            int colums = configObject.TrackAssets.IndexOf(datas);
            for (int i = 0;i<datas.targetObjects.Count;i++)
            {
                int row = datas.targetObjects.IndexOf(datas.targetObjects[i]);
                GameObject SelectionTarget = GameObject.Find(datas.targetObjects[i].id);
                if(SelectionTarget == null)
                {
                    continue;
                }
                if (SelectionTarget.transform.childCount == 0)
                {
                    continue;
                }
                GameObject prefab = SelectionTarget.transform.GetChild(0).gameObject;
                TrackTarget tTarget = configObject.TrackAssets[colums].targetObjects[row];
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                //Debug.LogWarning(assetPath);
                if (assetPath != "")
                {
                    tTarget.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                }
                else
                {
                    if(!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                        AssetDatabase.CreateFolder("Assets", "Prefabs");
                    bool result = true;
                    PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/" + datas.targetObjects[i].id + "_ARPrefab.prefab",out result);
                    if(result)
                    tTarget.prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + datas.targetObjects[i].id + "_ARPrefab.prefab");
                }
                //Debug.LogWarning(SelectionTarget.transform.GetChild(0).gameObject);

                tTarget.localOffset = prefab.transform.localPosition;
                tTarget.localRotation= prefab.transform.localRotation;
                tTarget.localScale = prefab.transform.localScale;
                configObject.TrackAssets[colums].targetObjects[row] = tTarget;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.LogWarning("保存成功");
        AssetDatabase.Refresh();
        GiveUpChanges();
        AssetConfig.EditARAssetsAdapte();
        Selection.activeObject = configObject;
    }
    public void GiveUpChanges()
    {
        DestroyImmediate(gameObject);
    }


    [CustomEditor(typeof(ARAssetEditManager))]
    public class ARAssetEditorBoard : Editor
    {
        public override void OnInspectorGUI()
        {
            //获取脚本对象
            ARAssetEditManager editManager = target as ARAssetEditManager;
            EditorGUILayout.HelpBox("配置文件自动从路径Assets/AssetConfig.asset读取", MessageType.Info, true);
            editManager.configObject = EditorGUILayout.ObjectField("ConfigFile",editManager.configObject,typeof(AssetConfig),false) as AssetConfig;
            
            if(editManager.configObject == null) { return; }

            foreach(DataSetting datas in editManager.configObject.TrackAssets)
            {
                GUIStyle titleStyle = new GUIStyle();
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = (int)(1.2f* titleStyle.fontSize);
                titleStyle.normal.textColor = Color.green;



                EditorGUILayout.SelectableLabel(datas.xmlName+":", titleStyle);
                EditorGUILayout.BeginVertical();
                int seek = 0;
                foreach (TrackTarget target in datas.targetObjects)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(++seek+".",GUILayout.Width((seek.ToString() + ".").Length*10) );
                    EditorGUILayout.SelectableLabel(target.id, GUILayout.Width(Mathf.Max(150, target.id.Length*10)));

                    GameObject SelectionTarget = GameObject.Find(target.id);

                    //选择识别图
                    string btnImageName = "选择" + target.id;
                    if (GUILayout.Button(btnImageName, GUILayout.Width(40 + target.id.Length * 10)))
                    {
                        Selection.activeObject = SelectionTarget;
                    }

                    if (SelectionTarget != null && SelectionTarget.transform.childCount != 0) {
                        GameObject prefab = SelectionTarget.transform.GetChild(0).gameObject;

                        //选择预制体
                        string btnPrefabName = "选择" + prefab.name;
                        if (GUILayout.Button(btnPrefabName, GUILayout.Width(40+ prefab.name.Length * 10)))
                        {
                            Selection.activeObject = prefab;
                        }
                        string btnCenterPivotName = "中心对齐";
                        if (GUILayout.Button(btnCenterPivotName, GUILayout.Width(btnCenterPivotName.Length * 20)))
                        {
                            prefab.transform.localPosition = Vector3.zero;
                        }
                        string btnScaleName = "重置缩放";
                        if (GUILayout.Button(btnScaleName, GUILayout.Width(btnScaleName.Length * 20)))
                        {
                            prefab.transform.localScale = target.localScale;
                        }
                        string btnClearPrefab = "清除预制体";
                        if (GUILayout.Button(btnClearPrefab, GUILayout.Width(btnClearPrefab.Length * 20)))
                        {
                            DestroyImmediate(prefab);
                        }
                    } else
                    {
                        EditorGUILayout.LabelField("预制体设置:",GUILayout.Width(80));
                        GameObject prefabClone = null;
                        prefabClone = EditorGUILayout.ObjectField("", prefabClone, typeof(GameObject), false,GUILayout.Width(250)) as GameObject;
                        if(prefabClone != null)
                        {
                            GameObject tempObject = Instantiate(prefabClone, SelectionTarget.transform.position, new Quaternion());
                            tempObject.name = tempObject.name.Replace("(Clone)", "");
                            tempObject.transform.parent = SelectionTarget.transform;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("确认修改"))
            {
                editManager.ConfirmChanges();
                //                Debug.LogWarning("确认修改");
            }
            if (GUILayout.Button("放弃修改"))
            {
                editManager.GiveUpChanges();
                //                Debug.LogWarning("确认修改");
            }
            GUILayout.EndHorizontal();
        }
    }
}

#endif


