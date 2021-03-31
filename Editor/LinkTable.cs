
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System;
using OnionCollections.DataEditor;


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


    //
    public string GetTableAssetPath()
    {
        return AssetDatabase.GetAssetPath(this);
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


    //

    public void ParseDataFromTSV(string tsvText)
    {
        const char splitLine = '\n';
        const char splitChar = '\t';


        colNames = new List<string>();
        ids = new List<string>();
        data = new List<Col>();


        var lines = tsvText.Split(splitLine).ToArray();

        colNames = lines[0].Split(splitChar).Skip(1).Select(_ => _.Trim()).ToList();  //略過id的Col

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

    [NodeAction("Import TSV")]
    public void ImportDataFromTSV()
    {
        string path = EditorUtility.OpenFilePanel("Select Table(TSV)", "", "tsv");

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

            //EditorUtility.DisplayDialog("LinkTable", "DONE!", "OK");
        }
    }

    [NodeAction("Copy Path")]
    public void CopyPath()
    {
        string path = GetTableAssetPath();
        EditorGUIUtility.systemCopyBuffer = path;
        Debug.Log($"Copy Path : {path}");
    }

    //

    [Obsolete]
    public void OpenUrl()
    {
        Application.OpenURL(linkSheetUrl);
    }

    [Obsolete]
    public void DownloadAndImportTSV()
    {
        DownloadTSV();
        ImportDataFromTSV();
    }

    [Obsolete]
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

    float unitIdCellWidth = 30F;
    float unitCellWidth = 80F;
    float unitCellHeight = 21F;

    const int vPadding = 3;
    const int hPadding = 5;

    LinkTable _LinkTable = null;
    LinkTable LinkTable
    {
        get
        {
            if (_LinkTable == null)
                _LinkTable = target as LinkTable;

            return _LinkTable;
        }
    }

    Texture2D headerBackground = null;
    Texture2D splitLineColor = null;
    Texture2D skipSplitLineColor = null;


    void OnEnable()
    {
        headerBackground = MakeTex(new Color(0.5F, 0.5F, 0.5F, 0.1F));
        splitLineColor = MakeTex(new Color(0.5F, 0.5F, 0.5F, 0.2F));
        skipSplitLineColor = MakeTex(new Color(0.5F, 0.5F, 0.5F, 0.05F));

        Texture2D MakeTex(Color color)
        {
            Color[] pix = new[] { color };
            Texture2D result = new Texture2D(1, 1);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }

    public override void OnInspectorGUI()
    {
        GUILayout.Space(10);

        string lastImportTime = string.IsNullOrEmpty(LinkTable.lastImportTime) ? "Never Import" : $"{LinkTable.lastImportTime:yyyy/MM/dd hh:mm:ss}";

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            GUI.color = new Color(1, 1, 1, 0.5F);
            GUILayout.Label($"Last Import Time : {lastImportTime}");
            GUI.color = new Color(1, 1, 1, 1);

            GUILayout.FlexibleSpace();
        }

        GUILayout.Space(40);

        using (new GUILayout.HorizontalScope())
        {
            float h = 26;

            var c = EditorGUIUtility.IconContent("d_Import");
            c.text = "  Import  ";

            if (GUILayout.Button(c, GUILayout.Width(90), GUILayout.Height(h)))
            {
                ShowImportMenu();
            }

            GUILayout.FlexibleSpace();

            string path = LinkTable.GetTableAssetPath();

            GUI.color = new Color(1, 1, 1, 0.5F);
            GUILayout.Label(path, GUILayout.Height(h));
            GUI.color = new Color(1, 1, 1, 1);

            GUILayout.Space(5);

            if (GUILayout.Button("Copy", GUILayout.Width(90), GUILayout.Height(h)))
            {
                LinkTable.CopyPath();
            }
        }

        GUILayout.Space(30);

        DrawTable();

        GUILayout.Space(30);
        
        isShow = EditorGUILayout.Foldout(isShow, $"Origin Data ({LinkTable.ids.Count})");

        if (isShow)
        {
            base.OnInspectorGUI();
        }



        serializedObject.ApplyModifiedProperties();

    }


    GUIStyle headerStyle;
    GUIStyle cellStyle;
    GUIStyle hLineStyle;
    GUIStyle vLineStyle;
    GUIStyle hSkipLineStyle;
    const int viewRowLines = 10;
    void DrawTable()
    {
        InitStyles();

        if (LinkTable.ids.Count == 0 && LinkTable.colNames.Count == 0)
        {
            float h = 26;

            GUILayout.Label("This table is empty.", GUILayout.Height(h));
            GUILayout.Space(5);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Please ", GUILayout.Width(45), GUILayout.Height(h));


                var c = EditorGUIUtility.IconContent("d_Import");
                c.text = " Import  ";

                if (GUILayout.Button(c, GUILayout.Width(90), GUILayout.Height(h)))
                {
                    ShowImportMenu();
                }

                GUILayout.Label(" data.", GUILayout.Height(h));
            }
        }
        else
        {
            //Table View
            try
            {
                int rowLines = Mathf.Min(LinkTable.ids.Count, viewRowLines);
                DrawTableHeader();
                for (int i = 0; i < rowLines; i++)
                {
                    DrawTableRow(i);
                }

                if (LinkTable.ids.Count > viewRowLines)
                {
                    DrawSkipSplitLine();
                    DrawTableRow(LinkTable.ids.Count - 1);
                }
            }
            catch
            {
                GUILayout.Space(10);
                GUILayout.Label("Table has some error...");
            }
        }

        void InitStyles()
        {
            headerStyle = new GUIStyle("Label")
            {
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(hPadding, hPadding, vPadding, vPadding),
            };
            headerStyle.normal.background = headerBackground;

            cellStyle = new GUIStyle("Label")
            {
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(hPadding, hPadding, vPadding, vPadding),
            };

            hLineStyle = new GUIStyle("Label")
            {
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(hPadding, hPadding, 0, 0),
            };
            hLineStyle.normal.background = splitLineColor;

            vLineStyle = new GUIStyle("Label")
            {
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, vPadding, vPadding),
            };
            vLineStyle.normal.background = splitLineColor;

            hSkipLineStyle = new GUIStyle("Label")
            {
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
            };
            hSkipLineStyle.normal.background = skipSplitLineColor;
            hSkipLineStyle.normal.textColor = new Color(0.5F, 0.5F, 0.5F, 0.5F);
            hSkipLineStyle.alignment = TextAnchor.MiddleCenter;
        }
        
        void DrawTableHeader()
        {
            float h = unitCellHeight;
            using (new GUILayout.HorizontalScope())
            {
                DrawHorizontalSplitLine();
            }

            using (new GUILayout.HorizontalScope())
            {
                DrawVerticalSplitLine(h);
                GUILayout.Label("", headerStyle, GUILayout.Width(unitIdCellWidth));    //ID
                for (int i = 0; i < LinkTable.colNames.Count; i++)
                {
                    DrawVerticalSplitLine(h);
                    GUILayout.Label(LinkTable.colNames[i], headerStyle, GUILayout.Width(unitCellWidth));
                }
                DrawVerticalSplitLine(h);
            }

            using (new GUILayout.HorizontalScope())
            {
                DrawHorizontalSplitLine();
            }

        }

        void DrawTableRow(int rowIndex)
        {
            float h = unitCellHeight;
            using (new GUILayout.HorizontalScope())
            {
                DrawVerticalSplitLine(h);
                GUILayout.Label(LinkTable.ids[rowIndex], cellStyle, GUILayout.Width(unitIdCellWidth));    //ID
                for (int i = 0; i < LinkTable.colNames.Count; i++)
                {
                    DrawVerticalSplitLine(h);
                    GUILayout.Label(LinkTable.data[i].items[rowIndex], cellStyle, GUILayout.Width(unitCellWidth));
                }
                DrawVerticalSplitLine(h);
            }

            using (new GUILayout.HorizontalScope())
            {
                DrawHorizontalSplitLine();
            }
        }

        void DrawVerticalSplitLine(float h)
        {
            GUILayout.Label("", vLineStyle, GUILayout.Width(1), GUILayout.Height(h));
        }

        void DrawHorizontalSplitLine()
        {
            GUILayout.Label("", hLineStyle, GUILayout.Height(1), GUILayout.Width(GetTableWidth()));
        }

        void DrawSkipSplitLine()
        {
            float h = unitCellHeight / 2;

            using (new GUILayout.HorizontalScope())
            {
                DrawVerticalSplitLine(h);
                GUILayout.Label("", hSkipLineStyle, GUILayout.Width(unitIdCellWidth), GUILayout.Height(h));    //ID
                for (int i = 0; i < LinkTable.colNames.Count; i++)
                {
                    DrawVerticalSplitLine(h);
                    GUILayout.Label("", hSkipLineStyle, GUILayout.Width(unitCellWidth), GUILayout.Height(h));
                }
                DrawVerticalSplitLine(h);
            }

            using (new GUILayout.HorizontalScope())
            {
                DrawHorizontalSplitLine();
            }
        }

        float GetTableWidth()
        {
            return unitIdCellWidth + 1 + (LinkTable.colNames.Count * (unitCellWidth + 1)) + 1;
        }

    }
       

    void ShowImportMenu()
    {
        Event current = Event.current;

        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("From TSV"), false, () =>
        {
            LinkTable.ImportDataFromTSV();
        });
        menu.ShowAsContext();

        current.Use();
    }


}