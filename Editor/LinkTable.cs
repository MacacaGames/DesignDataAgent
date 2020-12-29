
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System;


[CreateAssetMenu(menuName = "Design Data Agent/Link Table", fileName = "LinkTable")]
public class LinkTable : ScriptableObject
{
    [HideInInspector]
    public string linkSheetUrl;

    [Multiline(3)]
    [HideInInspector]
    public string description;

    [HideInInspector]
    public string lastImportTime;

    //
    public void OpenUrl()
    {
        Application.OpenURL(linkSheetUrl);
    }

    //
    public void GetTableAssetPath()
    {
        Debug.Log(AssetDatabase.GetAssetPath(this));
    }


    public List<string> colNames = new List<string>();

    public List<string> ids = new List<string>();

    public List<Col> data = new List<Col>();
    
    [Serializable]
    public struct Col
    {
        public List<string> items;
    }
    
    public string Get(string colName, string id)
    {
        int colIndex = colNames.IndexOf(colName);
        int idIndex = ids.IndexOf(id);
        
        if (colIndex < 0)
        {
            throw new System.Exception($"LinkTable({name})沒有名為 {colName} 的Col");
        }

        if (idIndex < 0)
        {
            throw new System.Exception($"LinkTable({name})沒有名為 {id} 的Id");
        }

        return data[colIndex].items[idIndex];
    }


    public void ParseDataFromTSV(string tsvText)
    {
        const char splitLine = '\n';
        const char splitChar = '\t';


        colNames = new List<string>();
        ids = new List<string>();
        data = new List<Col>();


        var lines = tsvText.Split(splitLine).ToArray();
        
        colNames = lines[0].Split(splitChar).Skip(1).Select(_=>_.Trim()).ToList();  //略過id的Col
        
        for (int i = 1; i < lines.Length - 1; i++)
        {
            string[] splitItems = lines[i].Split(splitChar);

            ids.Add(splitItems[0].Trim());

            for (int j = 0; j < colNames.Count; j++)
            {
                if (i == 1)
                {
                    data.Add(new Col
                    {
                        items = new List<string>()
                    });
                }
                data[j].items.Add(splitItems[j + 1].Trim());
            }
        }

    }

    //
    public void ImportDataFromTSV()
    {
        string path = EditorUtility.OpenFilePanel("Select Table", "", "tsv");

        if (string.IsNullOrEmpty(path) == false)
        {
            StringBuilder sb = new StringBuilder();
            using (StreamReader sr = new StreamReader(path))
            {
                while (sr.Peek() >= 0)
                {
                    sb.AppendLine(sr.ReadLine());
                }
            }

            ParseDataFromTSV(sb.ToString());
            EditorUtility.SetDirty(this);

            DateTime now = DateTime.Now;
            lastImportTime = $"{now:yyyy/MM/dd hh:mm:ss}";

            EditorUtility.DisplayDialog("LinkTable", "DONE!", "OK");
        }
    }

    //
    public void DownloadAndImportTSV()
    {
        DownloadTSV();
        ImportDataFromTSV();
    }

    public void DownloadTSV()
    {
        string downloadLink = linkSheetUrl.Replace("edit#gid=", "export?format=tsv&gid=");
        Application.OpenURL(downloadLink);
    }
}

[CustomEditor(typeof(LinkTable))]
public class LinkTableEditor: Editor
{
    bool isShow = false;

    public override void OnInspectorGUI()
    {
        LinkTable table = (target as LinkTable);

        GUILayout.Space(10);

        string lastImportTime = string.IsNullOrEmpty(table.lastImportTime) ? "Never Import": $"{table.lastImportTime:yyyy/MM/dd hh:mm:ss}";

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            GUI.color = new Color(1, 1, 1, 0.5F);
            GUILayout.Label($"Last Import Time : {lastImportTime}");
            GUI.color = new Color(1, 1, 1, 1);

            GUILayout.FlexibleSpace();
        }

        GUILayout.Space(30);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Import TSV", GUILayout.Width(150)))
            {
                table.ImportDataFromTSV();
            }

            GUILayout.Space(30);

            if (GUILayout.Button("Download & Import", GUILayout.Width(150)))
            {
                table.DownloadAndImportTSV();
            }

            GUILayout.FlexibleSpace();
        }

        GUILayout.Space(40);


        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("URL", GUILayout.Width(90)))
            {
                table.OpenUrl();
            }

            GUILayout.Space(5);

            var urlSP = serializedObject.FindProperty("linkSheetUrl");

            EditorGUILayout.PropertyField(urlSP, new GUIContent(""));
        }

        GUILayout.Space(10);

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Get Path", GUILayout.Width(90)))
            {
                table.GetTableAssetPath();
            }

            GUILayout.Space(5);

            GUI.color = new Color(1, 1, 1, 0.5F);
            GUILayout.TextField(AssetDatabase.GetAssetPath(target));
            GUI.color = new Color(1, 1, 1, 1);
        }

        GUILayout.Space(10);

        var desSP = serializedObject.FindProperty("description");
        EditorGUILayout.PropertyField(desSP, new GUIContent(""));

        GUILayout.Space(30);
        
        isShow = EditorGUILayout.Foldout(isShow, $"Origin ({table.ids.Count})");

        if(isShow)
        {
            base.OnInspectorGUI();
        }

        serializedObject.ApplyModifiedProperties();
    }
}