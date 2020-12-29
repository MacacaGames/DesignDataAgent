
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;

namespace OnionCollections.DesignDataAgent
{
    using CommandParam = Dictionary<string, string>;

    internal class DesignDataAgentBasicMethods : IDesignDataAgentMethods
    {
        DesignDataAgent designDataAgent;
        public void Import(DesignDataAgent designDataAgent, DesignDataAgentMethodManager designDataAgentMethodManager)
        {
            this.designDataAgent = designDataAgent;

            designDataAgentMethodManager.RegisterMethod(new Dictionary<string, Func<DesignDataAgent.CellInfo, CommandParam, DesignDataAgent.CommandExecuteResult>>
            {
                //DO

                ["int"] = SetInt,
                ["float"] = SetFloat,
                ["bool"] = SetBool,
                ["string"] = SetString,
                ["object"] = SetObject,
                ["Vector2"] = SetVector2,
                ["Sprite"] = SetSprite,
                ["enum"] = SetEnumByName,

                ["Method"] = Method,
                ["ForMethod"] = ForMethod,

                ["CreateAsset"] = CreateAsset,
                ["SaveAssets"] = SaveAssets,
                ["Reset"] = ResetPropertyValue,
                ["ClearArray"] = ClearArray,
                ["Print"] = PrintText,
                ["Invoke"] = InvokeFunction,
                ["SetRoot"] = SetRoot,

                //IF

                ["NotEmpty"] = NotEmpty,
                ["IsEmpty"] = IsEmpty,
                ["IsEmptyString"] = IsEmptyString,
                ["IsNotEmptyString"] = IsNotEmptyString,
                ["IsFirst"] = IsFirst,
                ["IsLast"] = IsLast,
                ["IsExist"] = IsExist,
                ["IsNotExist"] = IsNotExist,
                ["IsTrue"] = IsTrue,
                ["IsFalse"] = IsFalse,
                ["IsEqual"] = IsEqual,
                ["IsNotEqual"] = IsNotEqual,
            },
            logRegister: false);

            designDataAgentMethodManager.RegisterParamFunc(new Dictionary<string, Func<string[], string>>
            {
                ["Calc"] = Calc,
                ["GetCellValue"] = GetCellValue,
                ["ToInt"] = ToInt,
            },
            logRegister: false);
        }

        #region BasicParamFunctions

        string Calc(string[] param)
        {
            //func.Calc([exp])

            string expression = param[0];
            string result = Arithmetic.Calculation(expression);
            return result;
        }

        string GetCellValue(string[] param)
        {
            //func.GetCellValue([colName],[id])

            string colName = param[0];
            string id = param[1];

            var key = (colName, id);
            if (designDataAgent.cellValueCache.TryGetValue(key, out string result) == false)
            {
                Table.Row targetRow = designDataAgent.targetTable.rows.First(r => r[Table.idColName] == id);
                result = targetRow[colName];
                designDataAgent.cellValueCache.Add(key, result);
            }

            return result;
        }

        string ToInt(string[] param)
        {
            //func.ToInt([float],[mode])

            string fValue = param[0];

            string mode = param[1];

            float f = float.Parse(fValue);

            switch (mode)
            {
                case "Ceiling":
                    return Mathf.CeilToInt(f).ToString();
                case "Floor":
                    return Mathf.FloorToInt(f).ToString();
                case "Round":
                    return Mathf.RoundToInt(f).ToString();
                default:
                    return fValue;
            }
        }

        #endregion


        #region BasicParseMethods

        enum ValueType
        {
            Int = 0,
            Float = 1,
            String = 2,
            Boolean = 3,
            Enum = 4,
            Object = 5,
            Vector2 = 6,
        }

        const string emptyString = "(Empty)";

        DesignDataAgent.CommandExecuteResult SetValue(DesignDataAgent.CellInfo cellInfo, CommandParam param, object value, ValueType valueType)
        {
            IEnumerable<string> path = designDataAgent.GetPathWithCommandAndParam(param, "target");
            SerializedProperty p = designDataAgent.GetNodeByPath(path).GetProperty();

            if (p == null)
                throw new Exception($"{designDataAgent.GetCellId(cellInfo)}找到的Path目標為null。Path={param["target"]}");

            string resultDescription = null;

            try
            {
                switch (valueType)
                {
                    case ValueType.Int:
                        p.intValue = (int)value;

                        resultDescription = $"{Utilities.GetPathString(path)} = {value}";
                        break;

                    case ValueType.Float:
                        p.floatValue = (float)value;

                        resultDescription = $"{Utilities.GetPathString(path)} = {value}F";
                        break;

                    case ValueType.String:
                        p.stringValue = value as string;

                        resultDescription = $"{Utilities.GetPathString(path)} = \"{value}\"";
                        break;

                    case ValueType.Boolean:
                        bool resultBool = (bool)value;
                        p.boolValue = resultBool;

                        resultDescription = $"{Utilities.GetPathString(path)} = {resultBool.ToString().ToLower()}";
                        break;

                    case ValueType.Vector2:
                        Vector2 resultVector2 = (Vector2)value;
                        p.vector2Value = resultVector2;

                        resultDescription = $"{Utilities.GetPathString(path)} = Vector2({resultVector2.x},{resultVector2.y})";
                        break;

                    case ValueType.Enum:
                        p.intValue = (int)value;

                        resultDescription = $"{Utilities.GetPathString(path)} = {param["enumName"].Replace('+', '.')}.{value}";
                        break;

                    case ValueType.Object:
                        UnityEngine.Object resultObject = value as UnityEngine.Object;
                        p.objectReferenceValue = resultObject;

                        resultDescription = $"{Utilities.GetPathString(path)} = {resultObject.name}";
                        break;

                    default:
                        throw new Exception($"{designDataAgent.GetCellId(designDataAgent.currentExecutingCommand.Value)} 執行設定值時，沒有對應的型別可執行：{p} - {value.ToString()}({value.GetType().Name})。");

                }

                //紀錄
                p.serializedObject.ApplyModifiedProperties();
            }
            catch (Exception e)
            {
                throw new Exception($"{designDataAgent.GetCellId(designDataAgent.currentExecutingCommand.Value)} 執行設定值時發生錯誤：{e.Message}。");
            }

            return new DesignDataAgent.CommandExecuteResult
            {
                resultDescription = resultDescription
            };
        }

        DesignDataAgent.CommandExecuteResult SetInt(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][賦予值]
            //target: [目標路徑]
            //parseMode: [Ceiling | Floor | Round | ""]

            param = SetDefaultValueParam(cellInfo, param);
            param = SetDefaultParam(param, "parseMode", "");


            return SetValue(cellInfo, param, GetIntValue(), ValueType.Int);


            int GetIntValue()
            {
                string value = param["value"];

                switch (param["parseMode"])
                {
                    case "Ceiling":
                        return Mathf.CeilToInt(float.Parse(value));

                    case "Floor":
                        return Mathf.FloorToInt(float.Parse(value));

                    case "Round":
                        return Mathf.RoundToInt(float.Parse(value));

                    default:
                        return int.Parse(value);
                }
            }

        }

        DesignDataAgent.CommandExecuteResult SetBool(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][賦予值]
            //target: [目標路徑]

            param = SetDefaultValueParam(cellInfo, param);
            return SetValue(cellInfo, param, bool.Parse(param["value"]), ValueType.Boolean);
        }

        DesignDataAgent.CommandExecuteResult SetFloat(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][賦予值]
            //target: [目標路徑]

            param = SetDefaultValueParam(cellInfo, param);
            return SetValue(cellInfo, param, float.Parse(param["value"]), ValueType.Float);
        }

        DesignDataAgent.CommandExecuteResult SetString(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][賦予值]
            //target: [目標路徑]

            param = SetDefaultValueParam(cellInfo, param);

            string s = param["value"].Trim();
            if (Utilities.IsPinchWith(s, new[] { '"' }) ||
                Utilities.IsPinchWith(s, new[] { '\'' }))
            {
                s = Utilities.CutString(s, 1, 1);
            }
            else if (s == emptyString)
            {
                s = string.Empty;
            }

            return SetValue(cellInfo, param, s, ValueType.String);
        }

        DesignDataAgent.CommandExecuteResult SetVector2(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][賦予值]
            //target: [目標路徑]
            //[splitChar]: [分割字元](預設,)

            char spChar = ',';
            param = SetDefaultValueParam(cellInfo, param);
            param = SetDefaultParam(param, "splitChar", spChar.ToString());

            spChar = param["splitChar"][0];

            string[] v = param["value"].Split(spChar).Select(item => item.Trim()).ToArray();

            float x = float.Parse(v.First());
            float y = float.Parse(v.Last());    //取最後一個，若只填A而非A,B時，xy都會是A

            Vector2 v2 = new Vector2(x, y);
            return SetValue(cellInfo, param, v2, ValueType.Vector2);
        }

        DesignDataAgent.CommandExecuteResult SetEnum<T>(DesignDataAgent.CellInfo cellInfo, CommandParam param) where T : Enum
        {
            return SetValue(cellInfo, param, Enum.Parse(typeof(T), cellInfo.value), ValueType.Enum);
        }

        DesignDataAgent.CommandExecuteResult SetEnumByName(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][賦予值]
            //target: [目標路徑]
            //enumName: [Enum名稱]

            param = SetDefaultValueParam(cellInfo, param);
            return SetValue(cellInfo, param, Enum.Parse(Type.GetType(param["enumName"]), param["value"]), ValueType.Enum);
        }

        DesignDataAgent.CommandExecuteResult SetObject(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][賦予值]
            //target: [目標路徑]

            AssetDatabase.SaveAssets();

            UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(param["value"]);

            return SetValue(cellInfo, param, target, ValueType.Object);
            
        }

        DesignDataAgent.CommandExecuteResult SetSprite(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //asset: [圖片目錄路徑]
            //target: [目標路徑]

            AssetDatabase.SaveAssets();

            if (designDataAgent.targetTable.tableVersion >= 1)
            {

            }
            else if (designDataAgent.targetTable.tableVersion >= 0)
            {
                throw new Exception("Sprite 的參數 assetPath已過時，請改為asset");
            }

            UnityEngine.Object[] targets = AssetDatabase.LoadAllAssetsAtPath(param["asset"]);
            Sprite target = targets.Single(item => item is Sprite) as Sprite;

            IEnumerable<string> path = designDataAgent.GetPathWithCommandAndParam(param, "target");
            SerializedProperty p = designDataAgent.GetNodeByPath(path).GetProperty();

            try
            {
                if (target is UnityEngine.Object)
                {
                    p.objectReferenceValue = target;
                }
                else
                {
                    throw new Exception($"{designDataAgent.GetCellId(designDataAgent.currentExecutingCommand.Value)} 執行設定值時，沒有對應的型別可執行：{p} - {target.ToString()}。");
                }

                //紀錄
                p.serializedObject.ApplyModifiedProperties();

            }
            catch (Exception e)
            {
                throw new Exception($"{designDataAgent.GetCellId(designDataAgent.currentExecutingCommand.Value)} 執行設定值時發生錯誤：{e.Message}。");
            }

            return new DesignDataAgent.CommandExecuteResult
            {
                resultDescription = $"{Utilities.GetPathString(path)} = {target.name}"
            };
        }

        DesignDataAgent.CommandExecuteResult ResetPropertyValue(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //none param

            IEnumerable<string> path = designDataAgent.GetPathWithCommandAndParam(param, "target");
            SerializedProperty p = designDataAgent.GetNodeByPath(path).GetProperty();

            foreach (var el in GetVisibleChildren(p))
            {
                if (el.isArray)
                {
                    el.ClearArray();
                }
                else
                {
                    SetDefaultValue(el);
                }
            }

            p.serializedObject.ApplyModifiedProperties();

            return new DesignDataAgent.CommandExecuteResult
            {
                resultDescription = $"Reset : {Utilities.GetPathString(path)}"
            };

            void SetDefaultValue(SerializedProperty spForSetDefault)
            {
                switch (spForSetDefault.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        spForSetDefault.intValue = default;
                        break;
                    case SerializedPropertyType.Float:
                        spForSetDefault.floatValue = default;
                        break;
                    case SerializedPropertyType.String:
                        spForSetDefault.stringValue = default;
                        break;
                    case SerializedPropertyType.Boolean:
                        spForSetDefault.boolValue = default;
                        break;
                    case SerializedPropertyType.Enum:
                        spForSetDefault.intValue = default;
                        break;
                    case SerializedPropertyType.Vector2:
                        spForSetDefault.vector2Value = default;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        spForSetDefault.objectReferenceValue = default;
                        break;
                    case SerializedPropertyType.ArraySize:
                        //Do nothing
                        break;
                    default:
                        Debug.LogError($"{designDataAgent.GetCellId(designDataAgent.currentExecutingCommand.Value)} 執行設定預設值時，沒有對應的型別可執行：{spForSetDefault.displayName} ({spForSetDefault.propertyType})。");
                        //throw new Exception($"{GetCommandId(currentExecutingCommand.Value)} 執行設定預設值時，沒有對應的型別可執行：{spForSetDefault.displayName} ({spForSetDefault.propertyType})。");
                        break;
                }
            }

            IEnumerable<SerializedProperty> GetVisibleChildren(SerializedProperty serializedProperty)
            {
                SerializedProperty currentProperty = serializedProperty.Copy();
                SerializedProperty nextSiblingProperty = serializedProperty.Copy();
                {
                    nextSiblingProperty.NextVisible(false);
                }

                if (currentProperty.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                            break;

                        yield return currentProperty;
                    }
                    while (currentProperty.NextVisible(false));
                }
            }
        }

        DesignDataAgent.CommandExecuteResult ClearArray(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //none param

            IEnumerable<string> path = designDataAgent.GetPathWithCommandAndParam(param, "target");
            SerializedProperty p = designDataAgent.GetNodeByPath(path).GetProperty();

            bool isArray = p.isArray;

            if (isArray == true)
            {
                p.ClearArray();
                p.arraySize = 0;
                p.serializedObject.ApplyModifiedProperties();
            }

            return new DesignDataAgent.CommandExecuteResult
            {
                resultDescription = $"ClearArray : {Utilities.GetPathString(path)}"
            };
        }

        DesignDataAgent.CommandExecuteResult PrintText(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][印出值]

            param = SetDefaultValueParam(cellInfo, param);
            Debug.Log(param["value"]);
            return new DesignDataAgent.CommandExecuteResult
            {
                hideInLog = true
            };
        }

        DesignDataAgent.CommandExecuteResult CreateAsset(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //assetPath: [asset目錄]
            //assetName: [asset名稱](不含.asset)
            //assetType: [assetType]

            string assetName = param["assetName"];
            string assetPath = param["assetPath"];
            string assetType = param["assetType"];


            var instance = ScriptableObject.CreateInstance(assetType);

            if (instance != null)
            {
                string targetPath = $"{Path.Combine(assetPath, assetName)}.asset";
                AssetDatabase.CreateAsset(instance, targetPath);

                AssetDatabase.SaveAssets();

                return new DesignDataAgent.CommandExecuteResult
                {
                    resultDescription = $"Create Asset : {targetPath}"
                };
            }

            return new DesignDataAgent.CommandExecuteResult
            {
                resultDescription = $"Create Asset Fail : AssetName={assetName}, AssetPath={assetPath}, AssetType={assetType}"
            };

        }

        DesignDataAgent.CommandExecuteResult SaveAssets(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //none param

            AssetDatabase.SaveAssets();

            return new DesignDataAgent.CommandExecuteResult
            {
                hideInLog = true,
            };

        }

        DesignDataAgent.CommandExecuteResult Method(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //methodName: [MethodName]
            //[rootPath]: [RootPath]

            //提供{param.rootPath}可使用rootPath

            const string rootPathKeyName = "rootPath";

            param = SetDefaultValueParam(cellInfo, param);
            param = SetDefaultParam(param, rootPathKeyName, "");

            designDataAgent.SetParam(rootPathKeyName, param["rootPath"]);

            string templateDefineName = param["methodName"];

            designDataAgent.ExecuteCellInfo(new DesignDataAgent.CellInfo
            {
                id = cellInfo.id,
                tableLocation = cellInfo.tableLocation,
                methods = cellInfo.colDefines[templateDefineName],   //選擇其他指定名稱的define做為執行的method
                value = param["value"],
            });

            designDataAgent.ClearParam(rootPathKeyName);

            return new DesignDataAgent.CommandExecuteResult
            {
                hideInLog = true,
            };

        }

        DesignDataAgent.CommandExecuteResult ForMethod(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //methodName: [MethodName]
            //rangeString: [範圍] 1.單範圍指定：「1」2.範圍指定：「1-10」 3.集合指定：「[1,3,5,7]」
            //[indexName]: [index名稱](預設index)
            //[rootPath]: [RootPath] 

            //提供{param.rootPath}可使用rootPath
            //提供{param.index}、{param.index:00}、{param.index:000}可使用index

            param = SetDefaultValueParam(cellInfo, param);

            param = SetDefaultParam(param, "indexName", "index");
            param = SetDefaultParam(param, "rootPath", "");

            string indexKeyName = param["indexName"];

            IEnumerable<int> elements = GetIndexSet(param["rangeString"]);

            Debug.Log($"{designDataAgent.GetCellId(cellInfo)}\tFor {indexKeyName} = {param["rangeString"]}");
            designDataAgent.logDepth++;

            //

            foreach (int index in GetIndexSet(param["rangeString"]))
            {
                SetIndexParam(indexKeyName, index);

                //執行
                Method(cellInfo, new CommandParam
                {
                    ["methodName"] = designDataAgent.FillParam(cellInfo, param["methodName"]),     //此時填入Param
                    ["rootPath"] = param["rootPath"],
                });
            }

            //

            designDataAgent.logDepth--;
            Debug.Log($"{designDataAgent.GetCellId(cellInfo)}\tEnd {indexKeyName}");

            ClearIndexParam(indexKeyName);

            return new DesignDataAgent.CommandExecuteResult
            {
                hideInLog = true,
            };



            IEnumerable<int> GetIndexSet(string rangeString)
            {
                if (Utilities.IsStartWith(rangeString, "[") && Utilities.IsEndWith(rangeString, "]"))
                {
                    foreach (var el in rangeString
                        .Substring(1, rangeString.Length - 2)
                        .Split(',')
                        .Select(_ => int.Parse(_.Trim())))
                        yield return el;
                }
                else if (rangeString.Contains('-'))
                {
                    IEnumerable<int> range = rangeString.Split('-').Select(_ => int.Parse(_.Trim()));

                    int startIndex = range.ElementAt(0);
                    int endIndex = range.ElementAt(1);
                    int indexStep = (endIndex - startIndex > 0) ? 1 : -1;

                    int currentIndex = startIndex;
                    while ((indexStep > 0) ? currentIndex <= endIndex : currentIndex >= endIndex)
                    {
                        yield return currentIndex;
                        currentIndex += indexStep;
                    }
                }
                else
                {
                    yield return int.Parse(rangeString);
                }
            }

            void SetIndexParam(string _indexKeyName, int i)
            {
                designDataAgent.SetParam(_indexKeyName, $"{i}");
                designDataAgent.SetParam($"{_indexKeyName}:00", $"{i:00}");
                designDataAgent.SetParam($"{_indexKeyName}:000", $"{i:000}");
            }

            void ClearIndexParam(string _indexKeyName)
            {
                designDataAgent.ClearParam(_indexKeyName);
                designDataAgent.ClearParam($"{_indexKeyName}:00");
                designDataAgent.ClearParam($"{_indexKeyName}:000");
            }


        }

        DesignDataAgent.CommandExecuteResult InvokeFunction(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //target: [目標路徑]
            //functionName: [函式名稱]
            //param01: [參數01]
            //param..: [參數..]

            string functionName = param["functionName"];

            IEnumerable<string> path = designDataAgent.GetPathWithCommandAndParam(param, "target");
            UnityEngine.Object p = designDataAgent.GetNodeByPath(path).GetObject();

            MethodInfo methodInfo = p.GetType().GetMethod(functionName);

            ParameterInfo[] parameters = methodInfo.GetParameters();

            //把param["param01"]-param["param??"]當作參數丟進去，目前參數型別只支援string[]
            object[] paramsArray = parameters.Select((item, i) => param[$"param{(i + 1):00}"]).ToArray();

            methodInfo.Invoke(p, paramsArray);

            return new DesignDataAgent.CommandExecuteResult
            {
                resultDescription = $"{Utilities.GetPathString(path)} Invoke Function : {functionName}({string.Join(",", paramsArray)})"
            };

        }

        DesignDataAgent.CommandExecuteResult SetRoot(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //asset: [asset目錄] 

            if (designDataAgent.targetTable.tableVersion >= 1)
            {

            }
            else if (designDataAgent.targetTable.tableVersion >= 0)
            {
                throw new Exception("SetRoot 的參數 assetPath已過時，請改為asset");
            }

            param = SetDefaultValueParam(cellInfo, param);

            string assetPath = param["asset"];

            designDataAgent.SetRoot(assetPath);

            return new DesignDataAgent.CommandExecuteResult
            {
                resultDescription = $"SetRoot = {assetPath}"
            };
        }

        #endregion


        #region BasicConditions

        DesignDataAgent.CommandExecuteResult IsEmpty(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value	[格值][判斷值]

            var result = NotEmpty(cellInfo, param);

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = result.conditionResult == false,
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult NotEmpty(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value	[格值][判斷值]

            param = SetDefaultValueParam(cellInfo, param);

            bool result = string.IsNullOrEmpty(param["value"]) == false;

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = result,
                hideInLog = true,
            };

        }

        DesignDataAgent.CommandExecuteResult IsEmptyString(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value	[格值][判斷值]

            param = SetDefaultValueParam(cellInfo, param);
            string value = param["value"].Trim();

            string[] compare = new[] {
            "''",
            "\"\"",
            emptyString,
            ""
        };
            bool result = compare.Any(c => c == value);

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = result,
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult IsNotEmptyString(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value	[格值][判斷值]

            var result = IsEmptyString(cellInfo, param);

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = result.conditionResult == false,
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult IsFirst(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //colName01: [欄位名稱01]
            //colName..: ...[複數]

            int r = cellInfo.tableLocation.row - designDataAgent.targetTable.dataBeginRowIndex;
            string targetValue = GetCompareKey(param, r);

            for (int i = 0; i < r; i++)
            {
                string cellValue = GetCompareKey(param, i);

                if (targetValue == cellValue)
                {
                    return new DesignDataAgent.CommandExecuteResult
                    {
                        conditionResult = false,
                        resultDescription = $"{targetValue} 並非第一個出現的元素，不符合條件",
                        hideInLog = true,
                    };
                }
            }

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = true,
                resultDescription = $"{targetValue} 為第一個出現的元素，符合條件並執行",
                hideInLog = true,
            };

            string GetCompareKey(CommandParam colNames, int row)
            {
                List<string> s = new List<string>();
                for (int i = 0; i < 10; i++)
                {
                    if (colNames.TryGetValue($"colName{i:00}", out string v) == true)
                    {
                        s.Add(v);
                    }
                }
                return string.Join(";", s.Select(colName => designDataAgent.targetTable.rows[row][colName]));
            }
        }

        DesignDataAgent.CommandExecuteResult IsLast(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //colName01: [欄位名稱01]
            //colName..: ...[複數]

            int r = cellInfo.tableLocation.row - designDataAgent.targetTable.dataBeginRowIndex;
            string targetValue = GetCompareKey(param, r);

            for (int i = r + 1; i < designDataAgent.targetTable.rows.Count; i++)
            {
                string cellValue = GetCompareKey(param, i);

                if (targetValue == cellValue)
                {
                    return new DesignDataAgent.CommandExecuteResult
                    {
                        conditionResult = false,
                        resultDescription = $"{targetValue} 並非最後一個出現的元素，不符合條件",
                        hideInLog = true,
                    };
                }
            }

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = true,
                resultDescription = $"{targetValue} 為最後一個出現的元素，符合條件並執行",
                hideInLog = true,
            };

            string GetCompareKey(CommandParam colNames, int row)
            {
                List<string> s = new List<string>();
                for (int i = 0; i < 10; i++)
                {
                    if (colNames.TryGetValue($"colName{i:00}", out string v) == true)
                    {
                        s.Add(v);
                    }
                }
                return string.Join(";", s.Select(colName => designDataAgent.targetTable.rows[row][colName]));
            }
        }

        DesignDataAgent.CommandExecuteResult IsExist(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //assetPath: [asset目錄]
            //assetName: [asset名稱](不含.asset)

            bool result;

            string path = $"{Path.Combine(param["assetPath"], param["assetName"])}.asset";
            var o = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
            result = o != null;


            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = result,
                resultDescription = $"{path} isExist = {result}",
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult IsNotExist(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //assetPath: [asset目錄]
            //assetName: [asset名稱](不含.asset)

            var result = IsExist(cellInfo, param);
            string path = $"{Path.Combine(param["assetPath"], param["assetName"])}.asset";

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = (result.conditionResult == false),
                resultDescription = $"{path} isNotExist = {(result.conditionResult == false)}",
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult IsTrue(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][判斷值]

            param = SetDefaultValueParam(cellInfo, param);

            string v = param["value"];

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = bool.Parse(v),
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult IsFalse(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][判斷值]

            var result = IsTrue(cellInfo, param);

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = result.conditionResult == false,
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult IsEqual(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][判斷值]
            //compareValue: [比對值]
            //[splitChar]: [分割字元](預設|)

            param = SetDefaultParam(param, "splitChar", "|");

            char splitChar = param["splitChar"][0];
            IEnumerable<string> compareValue = param["compareValue"]
                .Split(splitChar)
                .Select(item => designDataAgent.FillParam(cellInfo, item));

            param = SetDefaultValueParam(cellInfo, param);
            string v = param["value"];

            bool result = compareValue.Any(s => s == v);

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = result,
                hideInLog = true,
            };
        }

        DesignDataAgent.CommandExecuteResult IsNotEqual(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //value: [格值][判斷值]
            //compareValue: [比對值]
            //[splitChar]: [分割字元](預設|)

            var result = IsEqual(cellInfo, param);

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = (result.conditionResult == false),
                hideInLog = true,
            };
        }

        #endregion


        #region Utilities

        public static CommandParam SetDefaultValueParam(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            //若參數有帶值，就不會用格內值覆寫
            if (param.TryGetValue("value", out string v) == true)
            {
                return param;
            }

            return SetDefaultParam(param, "value", cellInfo.value);
        }

        public static CommandParam SetDefaultParam(CommandParam cellInfoParam, string paramKey, string paramDefaultValue)
        {
            if (cellInfoParam.TryGetValue(paramKey, out var v) == false)
            {
                cellInfoParam.Add(paramKey, paramDefaultValue);
            }
            else if (string.IsNullOrEmpty(v) == true)
            {
                cellInfoParam[paramKey] = paramDefaultValue;
            }

            return cellInfoParam;
        }

        #endregion


        #region Arithmetic

        static class Arithmetic
        {
            static Regex regexMultiplicationAndDivision { get; } = new Regex(@"(([-+]?\d+(\.(\d+))?)((\*|\/)([-+]?\d+(\.(\d+))?))+)");
            static Regex regexAdditionAndSubtraction { get; } = new Regex(@"\((([-+]?\d+(\.(\d+))?)((\+|\-)([-+]?\d+(\.(\d+))?))+)\)");
            static Regex regexEliminate { get; } = new Regex(@"\([-+]?\d+(\.(\d+))?\)");
            static Regex regexComplete { get; } = new Regex(@"(([-+]?\d+(\.(\d+))?)((\+|\-)([-+]?\d+(\.(\d+))?))*)");
            static Regex regexError { get; } = new Regex(@"\)\(|\)(\d+(\.(\d+))?)|(\d+(\.(\d+))?)\(");


            public static string Calculation(string expression)
            {
                if (regexError.IsMatch(expression))
                {
                    throw new Exception("進行運算時，輸入值錯誤");
                }

                while (true)
                {
                    int iNotMatch = 0;

                    if (regexMultiplicationAndDivision.IsMatch(expression))
                    {
                        expression = regexMultiplicationAndDivision.Replace(expression, MultiplicationAndDivision);
                    }
                    else
                    {
                        iNotMatch++;
                    }

                    if (regexAdditionAndSubtraction.IsMatch(expression))
                    {
                        expression = regexAdditionAndSubtraction.Replace(expression, AdditionAndSubtraction);
                    }
                    else
                    {
                        iNotMatch++;
                    }

                    if (regexEliminate.IsMatch(expression))
                    {
                        expression = regexEliminate.Replace(expression, Eliminate);
                    }
                    else
                    {
                        iNotMatch++;
                    }

                    if (regexComplete.Match(expression).Value == expression)
                    {
                        return Convert.ToSingle(regexComplete.Replace(expression, AdditionAndSubtraction)).ToString();
                    }

                    if (iNotMatch == 3)
                    {
                        throw new Exception($"Something wrong with Calc({expression})");
                    }

                }
            }

            static string MultiplicationAndDivision(Match match)
            {
                string text = match.Value;

                bool isPositive = true;

                foreach (char c in text)
                {
                    if (c == '-')
                    {
                        isPositive = !isPositive;
                    }
                }

                text = text.Replace("*+", "*");
                text = text.Replace("*-", "*");
                text = text.Replace("/+", "/");
                text = text.Replace("/-", "/");
                text = text.Replace("*", ",*");
                text = text.Replace("/", ",/");

                string[] numbers = text.Split(',');

                float result = Convert.ToSingle(numbers[0]) >= 0 ? Convert.ToSingle(numbers[0]) : (-Convert.ToSingle(numbers[0]));

                for (int i = 1; i < numbers.Length; i++)
                {
                    if (numbers[i] != "")
                    {
                        switch (numbers[i][0])
                        {
                            case '*':
                                result *= Convert.ToSingle(numbers[i].Substring(1, numbers[i].Length - 1));
                                break;
                            case '/':
                                result /= Convert.ToSingle(numbers[i].Substring(1, numbers[i].Length - 1));
                                break;
                        }
                    }

                }

                if (isPositive == false)
                {
                    result = -result;
                }

                return result >= 0 ? ("+" + result.ToString()) : result.ToString();
            }

            static string AdditionAndSubtraction(Match match)
            {

                string text = match.Value;
                text = text.Replace("(", "");
                text = text.Replace(")", "");
                text = text.Replace("++", "+");
                text = text.Replace("+-", "-");
                text = text.Replace("-+", "-");
                text = text.Replace("--", "+");
                text = text.Replace("+", ",+");
                text = text.Replace("-", ",-");

                string[] numbers = text.Split(',');
                float result = 0;
                foreach (string number in numbers)
                {
                    if (number != "")
                    {
                        result += Convert.ToSingle(number);
                    }
                }

                return result >= 0 ? ("+" + result.ToString()) : result.ToString();
            }

            static string Eliminate(Match match)
            {
                return match.Value.Substring(1, match.Value.Length - 2);
            }
        }

        #endregion
    }
}