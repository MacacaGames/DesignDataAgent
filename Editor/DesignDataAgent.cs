
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Text;
using LitJson;
using System.Reflection;

namespace OnionCollections.DesignDataAgent
{
    using CommandParam = Dictionary<string, string>;

    internal class DesignDataAgent
    {
        /// <summary>Current version.</summary>
        public const int lastestVersion = 1;

        /// <summary>The minnest support version.</summary>
        public const int minSupportVersion = 0;


        internal Table targetTable { get; private set; }
        DesignDataAgentMethodManager methodManager { get; set; }

        UnityEngine.Object root;


        #region Caches

        Dictionary<string, UnityEngine.Object> rootCache;
        public Dictionary<string, LinkTable> linkTableCache;
        public Dictionary<(string colName, string id), string> cellValueCache;

        #endregion

        Dictionary<string, double> actionSpendTime = new Dictionary<string, double>();

        public DesignDataAgent(Table table)
        {
            targetTable = table;
            methodManager = new DesignDataAgentMethodManager(this);

            //匯入基礎Method
            ImportBasicMethod();

            //匯入擴充Method
            ImportExtensionMethod();


            void ImportBasicMethod()
            {
                new DesignDataAgentBasicMethods().Import(this, methodManager);
            }

            void ImportExtensionMethod()
            {
                const string key = "import";
                JsonData tableDefine = targetTable.tableDefine;

                if (tableDefine.Keys.Contains(key) == false)
                    return;

                List<string> importKitNames = null;
                if (targetTable.tableVersion >= 1)
                {
                    importKitNames = new List<string>();
                    for (int i = 0; i < tableDefine[key].Count; i++)
                    {
                        importKitNames.Add((string)tableDefine[key][i]);
                    }
                }
                else if (targetTable.tableVersion >= 0)
                {
                    importKitNames = new List<string>(((string)tableDefine[key]).Split(';'));
                }

                if (importKitNames != null && importKitNames.Count > 0)
                {
                    Debug.Log("============ Extensions ============");
                }

                foreach (var el in importKitNames)
                {
                    Type type = Type.GetType(el.Trim());
                    if (type != null)
                    {
                        Assembly assembly = type.Assembly;
                        var target = assembly.CreateInstance(el);

                        if (target is IDesignDataAgentMethods methods)
                        {
                            Debug.Log($"Import {methods}");
                            methods.Import(this, methodManager);
                        }
                        else
                        {
                            throw new Exception("Import 的 target 非 IDesignDataAgentMethods");
                        }
                    }
                    else
                    {
                        throw new Exception($"找不到 Import 的項目 {el.Trim()}");
                    }
                }
            }

        }

        public int logDepth = 0;
        public IEnumerator Execute()
        {
            Debug.Log("============= Prepare =============");

            actionSpendTime = new Dictionary<string, double>();
            globalParams = new Dictionary<string, string>();
            rootCache = new Dictionary<string, UnityEngine.Object>();
            linkTableCache = new Dictionary<string, LinkTable>();
            cellValueCache = new Dictionary<(string colName, string id), string>();

            logDepth = 0;

            //建置Command
            List<CellInfo> commands = CreateCellInfosByTable(targetTable);

            string rootPath = "null";

            if (targetTable.tableVersion >= 1)
            {
                rootPath = targetTable.GetDefineValue("defaultRoot");
            }
            else if (targetTable.tableVersion >= 0)
            {
                rootPath = targetTable.GetDefineValue("rootPath");
            }

            if (rootPath != "null")
            {
                SetRoot(rootPath);

                if (root == null)
                {
                    //throw new Exception($"取得Root失敗：{rootPath}");
                }
            }
            else
            {
                root = null;
            }

            string rootName = (root == null) ? "null" : root.name;
            Debug.Log($"Root = {rootName}");

            Debug.Log($"Command Count = {commands.Count}");

            yield return null;

            Debug.Log("============= Execute =============");

            executeProgress = 0;


            //開始執行
            for (int i = 0; i < commands.Count; i++)
            {
                executeProgress = (i + 1F) / commands.Count;

                ExecuteCellInfo(commands[i]);
                yield return null;
            }

            AssetDatabase.SaveAssets();
            currentExecutingCommand = null;

            Debug.Log("============ Dashboard ============");

            var spendTimes = actionSpendTime
                .OrderByDescending(item => item.Value)
                .Select(item => $"{item.Key} :\t{item.Value:0.###}s");

            Debug.Log(string.Join("\n", spendTimes));

            List<CellInfo> CreateCellInfosByTable(Table table)
            {
                List<CellInfo> cellInfoList = new List<CellInfo>();

                int cellInfoId = 1;

                cellInfoList.AddRange(CreateHookCellInfos(CellInfo.CellType.OnStart, "onStart"));

                foreach (var row in table.rows)
                {
                    cellInfoList.AddRange(CreateRowCellInfos(row));
                }

                cellInfoList.AddRange(CreateHookCellInfos(CellInfo.CellType.OnEnd, "onEnd"));

                return cellInfoList;


                IEnumerable<CellInfo> CreateHookCellInfos(CellInfo.CellType cellType, string hookMethodName)
                {
                    //略過[0]的tableHeadDefine
                    for (int i = 1; i < table.colDefines.Count; i++)
                    {
                        //不可執行則跳出
                        if (IsExecutable(i) == false)
                            continue;

                        Dictionary<string, List<Dictionary<string, List<CommandParam>>>> define = table.colDefines[i];

                        if (define == null)
                            continue;

                        if (define.ContainsKey(hookMethodName) == false)
                            continue;

                        yield return new CellInfo
                        {
                            id = cellInfoId,
                            tableLocation = (row: -1, col: i),  //tableLocation.row 會是 -1

                            methods = define[hookMethodName],
                            colDefines = define,
                            type = cellType,
                            value = "",                         //value 一律是空字串
                        };
                    }
                }

                IEnumerable<CellInfo> CreateRowCellInfos(Table.Row row)
                {
                    const string methodName = "methods";

                    //略過[0]的tableHeadDefine
                    for (int i = 1; i < table.colDefines.Count; i++)
                    {
                        //不可執行則跳出
                        if (IsExecutable(i) == false)
                            continue;

                        Dictionary<string, List<Dictionary<string, List<CommandParam>>>> define = table.colDefines[i];

                        if (define == null)
                            continue;

                        if (define.ContainsKey(methodName) == false)
                            continue;

                        yield return new CellInfo
                        {
                            id = cellInfoId,
                            tableLocation = (row: row.index + table.dataBeginRowIndex, col: i),

                            methods = define[methodName],
                            colDefines = define,
                            type = CellInfo.CellType.Cell,
                            value = row.data[i],
                        };

                        cellInfoId++;
                    }
                }

                bool IsExecutable(int index)
                {
                    //取得對應col的Executable，沒有的話則為true
                    bool colExecutable = true;
                    if (index < table.colExecutables.Count)
                    {
                        colExecutable = table.colExecutables[index];
                    }
                    return colExecutable;
                }

            }

        }

        public float executeProgress { get; private set; }
        public CellInfo? currentExecutingCommand { get; private set; }
        public void ExecuteCellInfo(CellInfo cellInfo)
        {
            currentExecutingCommand = cellInfo;

            foreach (var method in cellInfo.methods)
            {
                SetParam("value", cellInfo.value);

                //沒有if就幫填
                if (method.ContainsKey("if") == false)
                {
                    method.Add("if", new List<CommandParam>());
                }

                //如果是一般Cell
                if (cellInfo.type == CellInfo.CellType.Cell)
                {
                    //if裡面沒有IsEmpty就幫填NotEmpty
                    if (method["if"].Any(item => item["name"] == "IsEmpty" || item["name"] == "NotEmpty") == false)
                    {
                        method["if"].Add(new CommandParam { ["name"] = "NotEmpty" });
                    }
                }

                //Condition 
                bool conditionResult = method["if"]
                    .All(chain => ExecuteCommand(cellInfo, chain).conditionResult);

                if (conditionResult == true)
                {
                    //Do
                    foreach (var chain in method["do"])
                    {
                        ExecuteCommand(cellInfo, chain);
                    }
                }

                ClearParam("value");
            }
        }

        CommandExecuteResult ExecuteCommand(CellInfo cellInfo, CommandParam commnadParam)
        {
            //取得Command名稱
            string doMethodName = FillParam(cellInfo, commnadParam["name"]);

            if (actionSpendTime.ContainsKey(doMethodName) == false)
            {
                actionSpendTime.Add(doMethodName, 0F);
            }

            commnadParam = commnadParam.ToDictionary(
                item => item.Key,
                item => FillParam(cellInfo, item.Value));


            //嘗試取得對應的執行方法並執行
            if (methodManager.TryGetParseMethod(doMethodName, out var v))
            {
                double startTime = EditorApplication.timeSinceStartup;
                CommandExecuteResult result = v.Invoke(cellInfo, commnadParam);
                actionSpendTime[doMethodName] += EditorApplication.timeSinceStartup - startTime;

                if (result.hideInLog == false)
                {
                    Debug.Log($"{logTab(logDepth)}{GetCellId(cellInfo)}\t{result.resultDescription}");
                }

                return result;
            }

            throw new Exception($"{GetCellId(cellInfo)}\tCan not find method : {doMethodName}");

            string logTab(int depth)
            {
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < depth; i++)
                {
                    sb.Append('\t');
                }

                return sb.ToString();
            }
        }

        public string FillParam(CellInfo cellInfo, string paramValue)
        {
            //最多跑N次，途中只要全解析完就跳出
            for (int times = 0; times < 128; times++)
            {
                string innerestParam = GetInnerestParam(paramValue, 0);

                if (innerestParam == null)  //再也找不到可解析參數時跳出
                    return paramValue;

                //Table parameter
                {
                    if (targetTable.tableVersion >= 1)
                    {
                        const string tableParamHead = "{data.";
                        if (Utilities.IsStartWith(innerestParam, tableParamHead))
                        {
                            string defineName = innerestParam.Substring(tableParamHead.Length, innerestParam.IndexOf('}') - tableParamHead.Length);
                            paramValue = paramValue.Replace(innerestParam, targetTable.GetDefineValue($"data.{defineName}"));
                            continue;
                        }
                    }
                    else if (targetTable.tableVersion >= 0)
                    {
                        const string tableParamHead = "{table.";
                        if (Utilities.IsStartWith(innerestParam, tableParamHead))
                        {
                            string defineName = innerestParam.Substring(tableParamHead.Length, innerestParam.IndexOf('}') - tableParamHead.Length);
                            paramValue = paramValue.Replace(innerestParam, targetTable.GetDefineValue(defineName));
                            continue;
                        }
                    }
                }


                //Param parameter
                {
                    const string paramHead = "{param.";
                    if (Utilities.IsStartWith(innerestParam, paramHead))
                    {
                        string defineName = innerestParam.Substring(paramHead.Length, innerestParam.IndexOf('}') - paramHead.Length);
                        if (globalParams.TryGetValue(defineName, out string result))
                        {
                            paramValue = paramValue.Replace(innerestParam, result);
                            continue;
                        }
                    }
                }

                //linkTable parameter
                {
                    const string linkTableParamHead = "{linkTable[";
                    if (Utilities.IsStartWith(innerestParam, linkTableParamHead))
                    {
                        //把值拆成path、col、id，送去GetLinkTableValue拿值
                        string[] vparse = innerestParam.Substring(linkTableParamHead.Length, innerestParam.Length - linkTableParamHead.Length - 1 - "]".Length)
                            .Split(new string[] { "][" }, StringSplitOptions.None);

                        string tableValue = methodManager.GetLinkTableValue(vparse[0], vparse[1], vparse[2]); //[0]=path, [1]=colName, [2]=id

                        paramValue = paramValue.Replace(innerestParam, tableValue);
                        continue;
                    }
                }


                //func parameter
                {
                    const string funcParamHead = "{func.";
                    if (Utilities.IsStartWith(innerestParam, funcParamHead))
                    {
                        string funcName = innerestParam.Substring(funcParamHead.Length, innerestParam.IndexOf("(") - funcParamHead.Length);
                        string[] funcParams = innerestParam.Substring(innerestParam.IndexOf("(") + 1, innerestParam.LastIndexOf(")") - innerestParam.IndexOf("(") - 1).Split(',');

                        if (methodManager.TryGetParamFunc(funcName, out var func))
                        {
                            string resultValue = func(funcParams);
                            paramValue = paramValue.Replace(innerestParam, resultValue);
                            continue;
                        }
                        else
                        {
                            throw new Exception($"沒有名為 {funcName} 的ParamFunc。");
                        }
                    }
                }


                //Colume
                {
                    const string colNumberHead = "{#";
                    if (Utilities.IsStartWith(innerestParam, colNumberHead))
                    {
                        string colName = innerestParam.Substring(colNumberHead.Length, innerestParam.IndexOf('}') - colNumberHead.Length);
                        paramValue = paramValue.Replace(innerestParam, (int.Parse(GetColValue(colName)) - 1).ToString());
                        continue;
                    }
                }
                {
                    const string colHead = "{";
                    if (Utilities.IsStartWith(innerestParam, colHead))
                    {
                        string colName = innerestParam.Substring(colHead.Length, innerestParam.IndexOf('}') - colHead.Length);
                        paramValue = paramValue.Replace(innerestParam, GetColValue(colName));
                        continue;
                    }
                }


            }

            return paramValue;


            string GetInnerestParam(string paramV, int startFindIndex)
            {
                int endIndex = paramV.IndexOf('}', startFindIndex);
                if (endIndex >= 0)
                {
                    int startIndex = paramV.Substring(0, endIndex).LastIndexOf('{');
                    if (startIndex >= 0)
                    {
                        return paramV.Substring(startIndex, endIndex - startIndex + 1);
                    }
                    throw new Exception("在填入參數時發現參數括號不成對。");
                }
                return null;
            }

            string GetColValue(string paramV)
            {
                if (cellInfo.type != CellInfo.CellType.Cell)
                    throw new Exception("在 CellInfo.CellType 不為 Cell 的 CellInfo 中試圖取得 Col 資訊");

                Table.Row row = targetTable.rows[cellInfo.tableLocation.row - targetTable.dataBeginRowIndex];

                int indexOfFormat = paramV.IndexOf(':');
                string colName = null;

                //有格式化
                if (indexOfFormat > 0)
                {
                    string formatType = paramV.Substring(indexOfFormat + 1);
                    colName = paramV.Substring(0, indexOfFormat);

                    Table.Row rowPointer = row;

                    switch (formatType)
                    {
                        case "nearestTop":

                            while (true)
                            {
                                if (rowPointer == null)
                                    throw new Exception("尋找 NearestTop 時發生錯誤，沒有符合的結果。");

                                if (string.IsNullOrEmpty(rowPointer[colName]) == false)
                                    break;

                                rowPointer = rowPointer.GetPreviousRow();
                            }

                            return rowPointer[colName];


                        case "nearestBottom":

                            while (true)
                            {
                                if (rowPointer == null)
                                    throw new Exception("尋找 NearestBottom 時發生錯誤，沒有符合的結果。");

                                if (string.IsNullOrEmpty(rowPointer[colName]) == false)
                                    break;

                                rowPointer = rowPointer.GetNextRow();
                            }

                            return rowPointer[colName];


                        default:

                            throw new Exception($"使用了不存在的格式化字串：{paramV}");
                    }


                }

                //無格式化
                colName = paramV;
                return row[colName];
            }

        }

        public void SetRoot(string rootPath)
        {
            if (rootCache.TryGetValue(rootPath, out UnityEngine.Object rt) == false)
            {
                rt = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rootPath);
                rootCache.Add(rootPath, rt);
            }

            root = rt;
        }

        public IEnumerable<string> GetPathWithCommandAndParam(CommandParam param, string paramPathName)
        {
            string paramPath = param[paramPathName];
            return CalcPath(paramPath);

            IEnumerable<string> CalcPath(string path)
            {
                return path
                    .Replace("[", ".[")
                    .Split('.')
                    .Where(_ => string.IsNullOrEmpty(_) == false);
            }
        }

        public SerializedNode GetNodeByPath(IEnumerable<string> path)
        {
            SerializedNode currentNode = new SerializedNode(root);

            string currentPath = $"Root({root.name})";
            try
            {
                foreach (string pathChip in path)
                {
                    currentPath += " ▸ " + pathChip;

                    //索引子[??]
                    if (pathChip[0] == '[')
                    {
                        string innerIndexer = Utilities.CutString(pathChip, 1, 1);
                        currentNode = ParseIndexer(innerIndexer);
                    }

                    //Component
                    else if (Utilities.IsStartWith(pathChip, "component<") && Utilities.IsEndWith(pathChip, ">"))
                    {
                        string compName = Utilities.CutString(pathChip, 10, 1);
                        currentNode = currentNode.GetComponent(compName);
                    }

                    //屬性
                    else
                    {
                        currentNode = currentNode.Enter(pathChip);
                    }


                }

                return currentNode;
            }
            catch (Exception e)
            {
                throw new Exception($"{GetCellId(currentExecutingCommand.Value)} 在執行尋找Path目標時發生錯誤：{currentPath}\n錯誤資訊：{e.Message}");
            }



            SerializedNode ParseIndexer(string innerIndexer)
            {
                char[] indexSet = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '+' };
                char[] numberSet = { '#' };
                char[] idSet = { '\'', '"' };

                int? index = null;
                string id = null;

                if (Utilities.IsStartWith(innerIndexer, numberSet))         //list[#1]
                    index = int.Parse(Utilities.CutString(innerIndexer, 1, 0)) - 1;
                else if (Utilities.IsStartWith(innerIndexer, indexSet))     //list[0]
                    index = int.Parse(innerIndexer);
                else if (Utilities.IsPinchWith(innerIndexer, idSet))        //list['id']
                    id = Utilities.CutString(innerIndexer, 1, 1);
                else if (innerIndexer == "new")                             //list[new]
                    index = currentNode.ArrayLength;
                else if (innerIndexer == "last")                            //list[last]
                    index = currentNode.ArrayLength - 1;

                //[index]
                if (index.HasValue)
                {
                    return currentNode.EnterIndex(index.Value);
                }

                //[id]
                if (id != null)
                {
                    return currentNode.EnterId(id);
                }

                throw new Exception($"{GetCellId(currentExecutingCommand.Value)} 無法解析索引子內容：{innerIndexer}");
            }

        }

        public string GetCellId(CellInfo cellInfo)
        {
            int rowIndex = cellInfo.tableLocation.row;
            int colIndex = cellInfo.tableLocation.col;

            if (rowIndex < 0)
            {
                return $"[{Table.GetTicket(colIndex + 1)}_]";
            }

            Table.Row row = targetTable.rows[rowIndex - targetTable.dataBeginRowIndex];

            const string idName = "ID";
            if (row.table.colNames.Contains(idName))
            {
                return $"[{Table.GetTicket(colIndex + 1)}{row[idName]}]";
            }
            else
            {
                return $"[{Table.GetTicket(colIndex + 1)}{rowIndex + 1}?]";
            }
        }

        #region GlobalParam

        Dictionary<string, string> globalParams;

        public void SetParam(string paramName, string paramValue)
        {
            if (globalParams.ContainsKey(paramName) == false)
            {
                globalParams.Add(paramName, paramValue);
            }
            else
            {
                globalParams[paramName] = paramValue;
            }
        }

        public void ClearParam(string paramName)
        {
            globalParams.Remove(paramName);
        }

        #endregion


        //

        public class SerializedNode
        {
            public enum SerializeType { Object, Property, }
            public SerializeType type { get; private set; }

            readonly SerializedProperty sp;
            readonly SerializedObject so;
            public SerializedNode(SerializedProperty p) { sp = p; type = SerializeType.Property; }
            public SerializedNode(SerializedObject o) { so = o; type = SerializeType.Object; }
            public SerializedNode(UnityEngine.Object o) { so = new SerializedObject(o); type = SerializeType.Object; }

            public SerializedProperty GetProperty()
            {
                return sp;
            }
            public UnityEngine.Object GetObject()
            {
                if (type == SerializeType.Object)
                {
                    return so.targetObject;
                }
                else if (type == SerializeType.Property && sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    return sp.objectReferenceValue;
                }

                throw new Exception("SerializedNode 無法取得 Object");
            }

            public bool IsObject()
            {
                return (type == SerializeType.Object) || (type == SerializeType.Property && sp.propertyType == SerializedPropertyType.ObjectReference);
            }

            public SerializedNode Enter(string n)
            {
                if (type == SerializeType.Property && sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var node = new SerializedNode(sp.objectReferenceValue);
                    return node.Enter(n);
                }

                SerializedProperty newSp = null;

                switch (type)
                {
                    case SerializeType.Object:

                        newSp = so.FindProperty(n);
                        break;

                    case SerializeType.Property:
                        newSp = sp.FindPropertyRelative(n);
                        break;

                    default:
                        throw new Exception("SerializedNode的型別未知");
                }

                return TransObjRefToObj(newSp);
            }

            public SerializedNode EnterIndex(int index)
            {
                while (index >= sp.arraySize)
                {
                    sp.InsertArrayElementAtIndex(sp.arraySize);
                }

                return TransObjRefToObj(sp.GetArrayElementAtIndex(index));
            }

            public SerializedNode EnterId(string id)
            {
                UnityEngine.Object n = sp != null ? sp.objectReferenceValue : so.targetObject;

                if (n is IEnumerable<IQueryableData> q)
                {
                    return new SerializedNode(q.QueryByID<IQueryableData>(id) as UnityEngine.Object);
                }

                throw new Exception($"物件並非IQueryableData，無法查詢id。");
            }

            public bool IsArray()
            {
                return type == SerializeType.Property && sp.isArray;
            }

            public int ArrayLength
            {
                get
                {
                    if (IsArray() == true)
                        return sp.arraySize;
                    else
                        throw new Exception("無法取得非陣列的SerializedNode長度");
                }
            }

            public SerializedNode GetComponent(string compName)
            {
                UnityEngine.Object o = null;
                if (type == SerializeType.Object)
                {
                    o = (so.targetObject as GameObject).GetComponent(compName);
                }
                else if (sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    o = (sp.objectReferenceValue as GameObject).GetComponent(compName);
                }

                if (o == null)
                {
                    throw new Exception($"此 SerializedNode 無法取得Component({compName})。");
                }

                return new SerializedNode(o);
            }

            SerializedNode TransObjRefToObj(SerializedProperty newSp)
            {
                return new SerializedNode(newSp);
            }
        }

        public struct CellInfo
        {
            public int id;
            public (int row, int col) tableLocation;

            public List<Dictionary<string, List<CommandParam>>> methods;
            public Dictionary<string, List<Dictionary<string, List<CommandParam>>>> colDefines;

            public CellType type;

            public string value;

            public enum CellType
            {
                Cell = 0,
                OnStart = 1,
                OnEnd = 2,
            }
        }

        public struct CommandExecuteResult
        {
            public string resultDescription;
            public bool conditionResult;
            public bool hideInLog;
        }

    }
}