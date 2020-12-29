using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using LitJson;
using System;
using System.IO;

namespace OnionCollections.DesignDataAgent
{
    using CommandType = Dictionary<string, string>;
    //using IfDoStructType = System.Collections.Generic.Dictionary<string, List<CommandType>>;
    using IfDoStructType = Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;

    internal class Table
    {

        public JsonData tableDefine;
        public int tableVersion { get; private set; }

        #region Cache

        Dictionary<string, string> dataCache = new Dictionary<string, string>();
        Dictionary<string, int> colNameCache = new Dictionary<string, int>();

        #endregion

        public List<bool> colExecutables;
        public List<Dictionary<string, List<Dictionary<string, List<CommandType>>>>> colDefines;
        public List<string> colNames;

        public List<Row> rows = new List<Row>();

        //locationDefine
        public int colExecutableRowIndex { get; private set; } = -1;
        public int colDefineRowIndex { get; private set; } = 0;
        public int colNameRowIndex { get; private set; } = 1;
        public int dataBeginRowIndex { get; private set; } = 3;

        public const string idColName = "ID";

        public string GetDefineValue(string path)
        {
            return PeekDefineValue(path, tableDefine);
        }

        public static string PeekDefineValue(string path, JsonData tableDefine)
        {
            string result;

            JsonData pointer = tableDefine;
            var chip = path.Replace("[", ".[").Split('.');

            for (int i = 0; i < chip.Length; i++)
            {
                if (chip[i][0] == '[' && chip[i][chip.Length - 1] == ']')
                {
                    //index
                    var inx = chip[i].Substring(1, chip[i].Length - 2);
                    if (pointer.IsArray)
                    {
                        pointer = pointer[inx];
                    }
                    else
                    {
                        throw new Exception($"GetDefineValue嘗試進入一個非陣列物件：{path}");
                    }
                }
                else
                {
                    //property
                    if (pointer.Keys.Contains(chip[i]) == true)
                    {
                        pointer = pointer[chip[i]];
                    }
                    else
                    {
                        throw new Exception($"GetDefineValue物件無 {chip[i]} 屬性：{path}");
                    }
                }
            }

            result = (string)pointer;

            return result;
        }

        public static List<Table> CreateTables(StreamReader sr, ParseType parseType)
        {
            List<Table> result = new List<Table>();
            switch (parseType)
            {
                case ParseType.tsv:

                    JsonData tableDefine = null;
                    List<string> header = new List<string>();
                    List<string> content = new List<string>();

                    int dataBeginIndex = -1;
                    int index = 0;

                    while (sr.Peek() >= 0)
                    {
                        string line = sr.ReadLine();

                        //如果還沒有tableDefine，就讀取；順便取得dataBegin
                        if (tableDefine == null)
                        {
                            tableDefine = JsonMapper.ToObject(line.Split('\t')[0]);
                            dataBeginIndex = int.Parse(PeekDefineValue("locationDefine.dataBegin", tableDefine));
                            header.Clear();
                            index = 0;
                        }

                        if (IsSplitLine(line, '-') == true)        //---------
                        {
                            //收拾目前的header和content轉成table
                            Table table = ParseFromTSV(header, content);
                            result.Add(table);

                            //單分隔線只需要清除content，下一個table就會繼續沿用同一個header
                            content.Clear();
                        }
                        else if (IsSplitLine(line, '=') == true)   //========
                        {
                            //收拾目前的header和content轉成table
                            Table table = ParseFromTSV(header, content);
                            result.Add(table);

                            //清除所有的header、content、tableDefine，接下來重新開始
                            tableDefine = null;
                            header.Clear();
                            content.Clear();
                        }
                        else
                        {
                            //判斷是否為分隔線，若否則加入header或content
                            if (index < dataBeginIndex)
                            {
                                header.Add(line);
                            }
                            else
                            {
                                content.Add(line);
                            }
                        }

                        index++;
                    }

                    if (content.Count > 0)
                    {
                        Table table = ParseFromTSV(header, content);
                        result.Add(table);
                    }

                    break;

            }

            return result;

            bool IsSplitLine(string line, char targetChar)
            {
                return
                    string.IsNullOrEmpty(line) == false &&              //line不為空
                    line[0] == targetChar &&                            //第一個字元符合
                    line.Take(3).All(c => c == targetChar) == true;     //檢查條件
            }

            Table ParseFromTSV(IEnumerable<string> header, IEnumerable<string> content)
            {
                Table table = new Table();
                table.ParseFormTSV(header.Concat(content));
                return table;
            }
        }




        public class Row
        {
            public Table table;

            public List<string> data;

            public int index;

            public string this[string colID] => GetColumeValue(colID);

            public Row(Table table, List<string> data, int index)
            {
                this.table = table;
                this.data = data;
                this.index = index;
            }

            string GetColumeValue(string colName)
            {
                if (table.colNameCache.TryGetValue(colName, out int resultIndex) == false)
                {
                    resultIndex = table.colNames.IndexOf(colName);
                    if (resultIndex < 0)
                    {
                        throw new Exception($"尋找了不存在的 Col:{colName}");
                    }
                    table.colNameCache.Add(colName, resultIndex);
                }

                return data[resultIndex];
            }

            public Row GetPreviousRow()
            {
                int targetIndex = index - 1;
                if (targetIndex < 0)
                    return null;

                return table.rows[targetIndex];
            }

            public Row GetNextRow()
            {
                int targetIndex = index + 1;
                if (targetIndex >= table.rows.Count)
                    return null;

                return table.rows[targetIndex];
            }

        }

        public enum ParseType
        {
            tsv
        }


        #region ParseFrom

        void ParseFormTSV(IEnumerable<string> lines)
        {
            const char splitItem = '\t';

            rows.Clear();
            colNameCache.Clear();

            //取得TableDefine
            tableDefine = JsonMapper.ToObject(lines.ElementAt(0).Split(splitItem)[0]);

            //取得TableVersion
            const string version = "version";
            if (tableDefine.Keys.Contains(version) == true)
                tableVersion = int.Parse(GetDefineValue(version));
            else
                tableVersion = 0;

            //取得各個Row的Index並拿資料
            if (tableVersion >= 1)
            {
                colExecutableRowIndex = int.Parse(GetDefineValue("locationDefine.executable"));
                colDefineRowIndex = int.Parse(GetDefineValue("locationDefine.define"));
                colNameRowIndex = int.Parse(GetDefineValue("locationDefine.colName"));
                dataBeginRowIndex = int.Parse(GetDefineValue("locationDefine.dataBegin"));
            }
            else if (tableVersion >= 0)
            {
                colExecutableRowIndex = int.Parse(GetDefineValue("colExecutableRowIndex"));
                colDefineRowIndex = int.Parse(GetDefineValue("colDefineRowIndex"));
                colNameRowIndex = int.Parse(GetDefineValue("colNameRowIndex"));
                dataBeginRowIndex = int.Parse(GetDefineValue("dataBeginRowIndex"));
            }

            Debug.Log($"[TABLE] " +
                $"executable = {colExecutableRowIndex}, " +
                $"define = {colDefineRowIndex}, " +
                $"colName = {colNameRowIndex}, " +
                $"dataBegin = {dataBeginRowIndex}");

            //取得colExecutableList
            if (colExecutableRowIndex >= 0)
            {
                colExecutables = lines.ElementAt(colExecutableRowIndex).Split(splitItem)
                    .Skip(1)
                    .Select(_ => string.IsNullOrEmpty(_.Trim()) ? true : bool.Parse(_.Trim()))
                    .ToList();
            }
            else
            {
                colExecutables = new List<bool>();
            }
            colExecutables.Insert(0, false); //插入一個位置作為tableDefine佔位(但無作用)


            //取得colDefine
            colDefines = lines.ElementAt(colDefineRowIndex).Split(splitItem)
                .Skip(1)        //colDefine必定略過第1個(通常為tableDefine)
                .Select(_ =>
                {
                    _ = _.Trim();

                    if (string.IsNullOrEmpty(_))
                        return null;
                    else if (_.Length > 2 && _.Substring(0, 2) == "//")     //if start width "//"
                    return null;
                    else
                    {
                        return JsonMapper.ToObject<Dictionary<string, List<Dictionary<string, List<CommandType>>>>>(_);
                    }

                })
                .ToList();
            colDefines.Insert(0, null); //插入一個位置作為tableDefine佔位(但無作用)

            //取得colName
            colNames = lines.ElementAt(colNameRowIndex).Split(splitItem).Select(_ => _.Trim()).ToList();

            if (colNames.Contains(idColName) == false)
            {
                throw new Exception("這份表格沒有ID欄位，請新增ID欄位。");
            }

            //取得data
            int index = 0;
            foreach (var line in lines.Skip(dataBeginRowIndex))
            {
                string l = line.Trim();

                //刪掉空白row
                if (string.IsNullOrEmpty(l) == true)
                {
                    continue;
                }

                rows.Add(new Row(this, line.Split(splitItem).Select(_ => _.Trim()).ToList(), index));
                index++;
            }
        }

        #endregion


        public static string GetTableTitle(string text, ParseType parseType)
        {
            switch (parseType)
            {
                case ParseType.tsv:
                    const char sp = '\t';

                    return (string)JsonMapper.ToObject(text.Split(sp)[0])["title"];
            }

            return null;
        }

        static Dictionary<int, string> baseCache = new Dictionary<int, string>();
        const string mappingArray = " ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static string GetTicket(int index)
        {
            //目前不支援2位以上

            int _2 = index / mappingArray.Length;
            int _1 = index % mappingArray.Length;

            if (_2 == 0)
                return $"{mappingArray[_1]}";

            return $"{mappingArray[_2]}{mappingArray[_1]}";
        }


    }
}