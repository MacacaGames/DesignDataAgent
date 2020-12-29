
using UnityEngine;
using System.Collections.Generic;
using System;


namespace OnionCollections.DesignDataAgent
{
    using CommandParam = Dictionary<string, string>;

    internal class DesignDataAgentExtensionMethodsTemplate : IDesignDataAgentMethods
    {

        DesignDataAgent designDataAgent;
        public void Import(DesignDataAgent designDataAgent, DesignDataAgentMethodManager designDataAgentMethodManager)
        {
            this.designDataAgent = designDataAgent;

            //Register method by this way.
            designDataAgentMethodManager.RegisterMethod(DesignDataAgentMethodManager.GetParseMethod(this));
            
            //Register param function by this way.
            designDataAgentMethodManager.RegisterParamFunc(DesignDataAgentMethodManager.GetParamMethod(this));

        }

        #region ExtensionParseMethods

        public DesignDataAgent.CommandExecuteResult MethodTemplate01(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            Debug.Log("Do Method Template01");

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = true,
                hideInLog = true
            };
        }

        public DesignDataAgent.CommandExecuteResult MethodTemplate02(DesignDataAgent.CellInfo cellInfo, CommandParam param)
        {
            Debug.Log("Do Method Template02");

            return new DesignDataAgent.CommandExecuteResult
            {
                conditionResult = true,
                hideInLog = true
            };
        }

        #endregion


        #region ExtensionParamFunctions

        public string ParamFunctionTemplate01(string[] param)
        {
            return $"Do Param Function Template01 ({string.Join(",", param)})";
        }

        public string ParamFunctionTemplate02(string[] param)
        {
            return $"Do Param Function Template02 ({string.Join(",", param)})";
        }

        #endregion

    }
}