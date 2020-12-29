
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace OnionCollections.DesignDataAgent
{
    using CommandParam = Dictionary<string, string>;
    using CommandExecuter = Func<DesignDataAgent.CellInfo, System.Collections.Generic.Dictionary<string, string>, DesignDataAgent.CommandExecuteResult>;

    internal class DesignDataAgentMethodManager
    {

        DesignDataAgent designDataAgent;
        internal DesignDataAgentMethodManager(DesignDataAgent designDataAgent)
        {
            this.designDataAgent = designDataAgent;

            parseMethodQuery = new Dictionary<string, Func<DesignDataAgent.CellInfo, CommandParam, DesignDataAgent.CommandExecuteResult>>();
            paramFuncQuery = new Dictionary<string, Func<string[], string>>();

        }


        #region ParseMethod

        Dictionary<string, CommandExecuter> parseMethodQuery;

        public void RegisterMethod(Dictionary<string, CommandExecuter> method, bool logRegister = true)
        {
            foreach (var methodPair in method)
            {
                if (logRegister == true)
                {
                    Debug.Log($"Register ParseMethod : {methodPair.Key}");
                }
                parseMethodQuery.Add(methodPair.Key, methodPair.Value);
            }
        }

        public bool TryGetParseMethod(string key, out CommandExecuter result)
        {
            return parseMethodQuery.TryGetValue(key, out result);
        }

        #endregion

        #region ParamFunction

        Dictionary<string, Func<string[], string>> paramFuncQuery;
        public void RegisterParamFunc(Dictionary<string, Func<string[], string>> paramFunc, bool logRegister = true)
        {
            foreach (var paramFuncPair in paramFunc)
            {
                if (logRegister == true)
                {
                    Debug.Log($"Register ParamFunc : {paramFuncPair.Key}");
                }
                paramFuncQuery.Add(paramFuncPair.Key, paramFuncPair.Value);
            }
        }

        public bool TryGetParamFunc(string key, out Func<string[], string> result)
        {
            return paramFuncQuery.TryGetValue(key, out result);
        }

        #endregion

        #region LinkTable

        public string GetLinkTableValue(string linkTablePath, string colName, string id)
        {
            if (designDataAgent.linkTableCache.TryGetValue(linkTablePath, out LinkTable linkTable) == false)
            {
                linkTable = AssetDatabase.LoadAssetAtPath<LinkTable>(linkTablePath);
                designDataAgent.linkTableCache.Add(linkTablePath, linkTable);
            }

            string result = linkTable.Get(colName, id);

            result = SafeCheck(result);

            return result;

            string SafeCheck(string value)
            {
                char[] unsafeChar = { ',', '(', ')' };

                if (value.IndexOfAny(unsafeChar) > 0)
                {
                    Debug.LogError($"LinkTable {linkTablePath}[{colName}][{id}] 含有不合法內容：{value}");

                    var s = value.Where(c => unsafeChar.Contains(c) == false).ToArray();    //去除不合法字元
                    return new string(s);
                }

                return value;
            }
        }

        #endregion

        #region Extensions

        public static Dictionary<string, CommandExecuter> GetParseMethod(object target)
        {
            //parseMethod

            var mInfos = target.GetType().GetMethods();

            var methods = mInfos.Where(item =>
            {
                var ps = item.GetParameters();
                return ps.Count() == 2 &&                                               //只有2個參數
                    ps[0].ParameterType == typeof(DesignDataAgent.CellInfo) &&          //參數1型別為DesignDataAgent.Command
                    ps[1].ParameterType == typeof(CommandParam) &&                      //參數2型別為CommandParam
                    item.ReturnType == typeof(DesignDataAgent.CommandExecuteResult);    //回傳型別為DesignDataAgent.CommandExecuteResult
        });

            Dictionary<string, CommandExecuter> result = new Dictionary<string, CommandExecuter>();
            foreach (var method in methods)
            {
                var d =
                   (Func<DesignDataAgent.CellInfo, CommandParam, DesignDataAgent.CommandExecuteResult>)
                   method.CreateDelegate(typeof(Func<DesignDataAgent.CellInfo, CommandParam, DesignDataAgent.CommandExecuteResult>), target);

                result.Add(method.Name, d);
            }

            return result;
        }

        public static Dictionary<string, Func<string[], string>> GetParamMethod(object target)
        {
            //paramFunction

            var mInfos = target.GetType().GetMethods();

            var paramMethods = mInfos.Where(item =>
            {
                var ps = item.GetParameters();
                return ps.Count() == 1 &&                           //只有一個參數
                    ps[0].ParameterType == typeof(string[]) &&      //參數型別為string[]
                    item.ReturnType == typeof(string);              //回傳型別為string
            });

            Dictionary<string, Func<string[], string>> result = new Dictionary<string, Func<string[], string>>();
            foreach (var paramMethod in paramMethods)
            {
                var d = (Func<string[], string>)paramMethod.CreateDelegate(typeof(Func<string[], string>), target);

                result.Add(paramMethod.Name, d);
            }

            return result;
        }


        #endregion

    }

    interface IDesignDataAgentMethods
    {
        void Import(DesignDataAgent designDataAgent, DesignDataAgentMethodManager designDataAgentMethodManager);
    }
}