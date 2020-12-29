#define TABLE_PARSE_DEBUG

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Text;
using LitJson;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OnionCollections.DesignDataAgent
{
    internal class TableParseEditorWindow : EditorWindow, IHasCustomMenu
    {
        const string AssetsPath = "Assets/DesignDataAgent";
        public static string path
        {
            get
            {
                //改成 UPM 後要針對不同的匯入方式處理路徑
                if (Directory.Exists(Application.dataPath + AssetsPath.Replace("Assets", "")))
                {
                    return AssetsPath;
                }
                return "Packages/com.macacagames.designdataagent/";
            }
        }


        [MenuItem("Window/Table Parse Window")]
        public static void ShowWindow()
        {
            TableParseEditorWindow wnd = GetWindow<TableParseEditorWindow>();
            wnd.titleContent = new GUIContent("TableParseEditorWindow");
        }


        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Set Unexecute"), false, SetUnexecute);

            void SetUnexecute()
            {
                isExecuting = false;


                TableParseEditorWindow wnd = GetWindow<TableParseEditorWindow>();

                wnd.UnbindUpdate();

                SetActive(wnd.btnBreakProgress, false);
                SetActive(wnd.btnExecute, true);
            }
        }


        public class TableMissionItem
        {
            public string title;
            public float progress;

            public int currentGroupIndex = -1;
            public int GroupCount = -1;

            public string path;

            public Label label;
            public ProgressBar bar;
            public VisualElement container;
        }


        Button btnBreakProgress;
        ProgressBar bar;
        Button btnExecute;


        bool hideLogs = false;
        public void OnEnable()
        {
            missionList = new List<TableMissionItem>();


            //


            VisualElement root = rootVisualElement;

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{path}/Editor/EditorWindow/TableParseEditorWindow.uxml");
            VisualElement labelFromUXML = visualTree.CloneTree();
            root.Add(labelFromUXML);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{path}/Editor/EditorWindow/TableParseEditorWindow.uss");

            root.Children().First().style.flexGrow = 1F;

            root.Q<Button>("SelectFile").clickable.clicked += SelectFile;
            root.Q<Button>("BreakProgress").clickable.clicked += BreakProgress;
            root.Q<Button>("ResetMission").clickable.clicked += ResetMission;


            root.Q<Toggle>("ToggleHideLog").RegisterValueChangedCallback(b =>
            {
                hideLogs = b.newValue;
            });

            btnExecute = root.Q<Button>("Execute");
            btnExecute.clickable.clicked += Execute;

            btnBreakProgress = root.Q<Button>("BreakProgress");
            bar = root.Q<ProgressBar>("progress");

            btnExecute.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            btnBreakProgress.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            FreshMissionList();

        }

        static bool isBindUpdate = false;
        void BindUpdate()
        {
            if (isBindUpdate == false)
            {
                currentUpdateIEnumerator = UpdateIEnumerator();
                EditorApplication.update += OnUpdate;
                isBindUpdate = true;
            }
        }

        void UnbindUpdate()
        {
            EditorApplication.update -= OnUpdate;
            isBindUpdate = false;
        }

        void OnUpdate()
        {
            if (currentUpdateIEnumerator.MoveNext() == false)
            {
                UnbindUpdate();
            }
        }

        const float frameWaitingTimeMax = 1F / 30F;

        int currentMissionIndex = -1;
        bool isStop = false;
        IEnumerator currentUpdateIEnumerator;
        IEnumerator UpdateIEnumerator()
        {
            isStop = false;
            SetIndex(0);
            isExecuting = true;

            missionList = missionList.Where(item => item.progress < 1F).ToList();
            FreshMissionList();

            yield return null;

            Debug.Log("============== Table ==============");

            //

            while (currentMissionIndex < missionList.Count)
            {

                double t = EditorApplication.timeSinceStartup;

                TableMissionItem currentMissionItem = missionList[currentMissionIndex];

                PinFile(currentMissionItem.path);

                //建Table
                DesignDataAgent designDataAgent = null;


                List<Table> tables;
                using (StreamReader sr = new StreamReader(currentMissionItem.path))
                {
                    tables = Table.CreateTables(sr, Table.ParseType.tsv);
                }

                for (int i = 0; i < tables.Count; i++)
                {
                    bool result = true;

                    currentMissionItem.currentGroupIndex = i;
                    currentMissionItem.GroupCount = tables.Count;
                    currentMissionItem.progress = 0F;

                    Table table = tables[i];

                    if (tables.Count > 1 && i < tables.Count)
                    {
                        Debug.Log($"============ Group ({i + 1}/{tables.Count}) ============");
                    }

                    designDataAgent = new DesignDataAgent(table);

                    if (table.tableVersion == DesignDataAgent.lastestVersion)
                    {
                        //最新版本
                    }
                    else if (table.tableVersion >= DesignDataAgent.minSupportVersion)
                    {
                        EditorUtility.DisplayDialog("表格版本過舊", $"表格 {table.GetDefineValue("title")} 版本過舊，\n將嘗試進行相容性執行。\n\n請盡速將此表格升級版本。", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("表格版本無法執行", $"表格 {table.GetDefineValue("title")} 版本過舊，\n且無法進行相容性執行。\n\n請盡速將此表格升級版本。", "OK");
                        isExecuting = false;
                        //SystemSounds.Exclamation.Play();
                        Debug.Log("=============== End ===============");
                        Debug.LogError("表格版本過舊無法執行");
                        yield break;
                    }


                    currentExecuteIEnumerator = designDataAgent.Execute();

                    btnBreakProgress.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    btnExecute.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

                    double tStart = EditorApplication.timeSinceStartup;
                    while (result == true)
                    {
                        if (isStop == true)
                            yield break;

                        currentMissionItem.progress = designDataAgent.executeProgress;

                        UpdateTableMissionItem(currentMissionItem);

#if (TABLE_PARSE_DEBUG)
                        result = currentExecuteIEnumerator.MoveNext();
#else
                try
                {
                    result = currentExecuteIEnumerator.MoveNext();
                }
                catch (System.Exception e)
                {
                    //失敗時直接結束
                    result = false;
                    throw e;
                }
#endif
                        //如果操作間隔 > 1frame最長時間，則跳下一個
                        if (EditorApplication.timeSinceStartup - tStart > frameWaitingTimeMax)
                        {
                            tStart = EditorApplication.timeSinceStartup;
                            yield return null;
                        }
                    }

                }

                SetIndex(currentMissionIndex + 1);

                Debug.Log($"Spent {EditorApplication.timeSinceStartup - t:#.0}s");

            }

            isExecuting = false;

            //

            if (IsAllMissionExecuteFinished() == true)
            {
                Debug.Log("=============== End ===============");
                //SystemSounds.Beep.Play();
            }
            else
            {
                Debug.LogError("Error");
                //SystemSounds.Exclamation.Play();
            }

            UnbindUpdate();

            SetActive(btnBreakProgress, false);
            SetActive(btnExecute, true);





        }

        bool IsAllMissionExecuteFinished()
        {
            return missionList.All(item => item.progress >= 1F);
        }

        void BreakProgress()
        {
            isStop = true;
            isExecuting = false;
            currentMissionIndex = -1;
            btnBreakProgress.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            btnExecute.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
        }

        void ResetMission()
        {
            if (isExecuting == true)
            {
                Debug.LogError("執行期間不能按！");
                return;
            }

            missionList.Clear();
            FreshMissionList();
            PinFile("");

        }

        private void OnDestroy()
        {
            UnbindUpdate();
        }

        static bool isExecuting = false;
        List<TableMissionItem> missionList;
        public void SelectFile()
        {
            if (isExecuting == true)
            {
                Debug.LogError("執行期間不能按！");
                return;
            }

            string path = EditorUtility.OpenFilePanel("Select Table", "", "tsv");

            if (string.IsNullOrEmpty(path) == false)
            {
                if (missionList.Count == 0)
                {
                    PinFile(path);
                }

                string TableHeaderLine = "";
                using (StreamReader sr = new StreamReader(path))
                {
                    TableHeaderLine = sr.ReadLine();
                }



                missionList.Add(new TableMissionItem
                {
                    path = path,
                    title = $"{Table.GetTableTitle(TableHeaderLine, Table.ParseType.tsv)}",
                    progress = 0F,
                });

                FreshMissionList();
            }
        }

        void PinFile(string path)
        {
            if (string.IsNullOrEmpty(path) == false)
            {
                rootVisualElement.Q<Label>("FilePath").text = path;

                string s = "";
                using (StreamReader sr = File.OpenText(path))
                {
                    s = sr.ReadLine();
                    s = s.Substring(0, s.IndexOf("\t"));
                }

                var define = JsonMapper.ToObject(s);

                rootVisualElement.Q<Label>("TableTitle").text = (string)define["title"];
            }
            else
            {
                rootVisualElement.Q<Label>("FilePath").text = "--";
                rootVisualElement.Q<Label>("TableTitle").text = "--";
            }
        }

        void SetIndex(int index)
        {
            currentMissionIndex = index;
        }

        void FreshMissionList()
        {
            VisualElement list = rootVisualElement.Q("PathList");

            rootVisualElement.Add(rootVisualElement.Q("InfoContainer"));

            while (list.childCount > 0)
            {
                list.RemoveAt(0);
            }

            foreach (var item in missionList)
            {
                list.Add(GetTableMissionItemVE(item));
                item.label.text = item.title;
                UpdateTableMissionItem(item);
            }

            //SetActive(rootVisualElement.Q<Button>("ResetMission"), missionList.Count > 0);
            SetActive(rootVisualElement.Q("PathListEmpty"), missionList.Count == 0);
            btnExecute.SetEnabled(missionList.Count > 0);
        }

        static void SetActive(VisualElement ve, bool active)
        {
            ve.style.display = active ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        void UpdateTableMissionItem(TableMissionItem item)
        {
            item.bar.value = item.progress * 100F;

            item.bar.title = item.progress.ToString("##0.0%");

            if (item.GroupCount <= 1)
            {
                item.label.text = item.title;
            }
            else
            {
                item.label.text = $"{item.title} ({item.currentGroupIndex + 1}/{item.GroupCount})";
            }

            item.bar.style.opacity = new StyleFloat(IsDone() == false ? 1F : 0.5F);

            bool IsDone()
            {
                return item.currentGroupIndex == item.GroupCount - 1 && item.progress >= 1F;
            }
        }

        IEnumerator currentExecuteIEnumerator;
        void Execute()
        {
            BindUpdate();

            //StartUpdate...
        }

        VisualElement GetTableMissionItemVE(TableMissionItem data)
        {
            VisualElement ve = new VisualElement();
            ve.AddToClassList("progressItem");

            Label label = new Label("-");
            label.AddToClassList("label");

            ProgressBar progressBar = new ProgressBar()
            {
                title = "0%"
            };
            progressBar.AddToClassList("progress");

            var p = progressBar.Q(className: "unity-progress-bar__progress");
            VisualElement barVe = new VisualElement();
            barVe.AddToClassList("progressBar");
            p.Add(barVe);

            Button btn = new Button(() => RemoveMission(data))
            {
                text = "-"
            };
            btn.AddToClassList("btn");

            data.label = label;
            data.bar = progressBar;
            data.container = ve;

            ve.Add(label);
            ve.Add(progressBar);
            ve.Add(btn);

            return ve;

            void RemoveMission(TableMissionItem d)
            {
                missionList.Remove(d);
                FreshMissionList();
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            isExecuting = false;
        }

    }
}