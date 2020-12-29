# Design Data Agent

Design data agent is a table parse tool for unity3D.
You can define parse method easily, and parse data to unity scriptable object.

## Dependencies
Import the dependencies into your project.
- Onion Data Editor : https://github.com/MacacaGames/OnionDataEditor

## Usage

1. Import Design Data Agent in your unity.
2. Create a table that format is `TSV`.
3. Open window from menu : `Window/TableParseWindow`.
4. Press `+` button, and select the `TSV` table.
5. Press `Execute` to parse table data to target scriptable object.

## Table Format
The `.` markers just use for format, please ignore them.

  `.`               |  `.`              | `.`               | `.`
:---------------    | :--------------   | :--------------   | :-------------- 
 `{TableDefine}`    | `{Executable01}`  | `{Executable02}`  | `{Executable03}`  
 `.`                | `{Col01Define}`   | `{Col02Define}`   | `{Col03Define}`   
 `ID`               | `{Col01Name}`     | `{Col02Name}`     | `{Col03Name}`    
 ID                 | {NameForRead}     | {NameForRead}     | {NameForRead}   
 1                  | {Col01Data}       | {Col02Data}       | {Col03Data}     
 ...                | ...               | ...               | ...     
 999                | {Col01Data}       | {Col02Data}       | {Col03Data}     


### TableDefine
```
{
    "title": "DataTitleInEditorWindow",
    "version": "1",
    "defaultRoot": "Assets/.../TargetScriptableObject.asset",
    "import": [],
    "locationDefine": {
        "executable":"0",
        "define":"1",
        "colName":"2",
        "dataBegin":"4"
    },
    "data": {
        "custumName01" : "data[0]"
    }
}
```
- `title` : Title display in editor window. It will help you to pick right table.
- `version` : Define the design data agent version. If version is too old to execute, parse window will tell you.
- `defaultRoot` : The target scriptable object asset path.
- `import` : If you have extension methods ( `IDesignDataAgentMethods` ), can import via the class name.
- `locationDefine` : Define all funcitonal row location, location start with zero. 
  - `executable` : Is this col will execute or not. True or false.
  - `define` : The column define row.
  - `colName` : The column name row. Some method need use column name to find column.
  - `dataBegin` : The first data row.
- `data` : You can define few data to replace some value in column define. Use data by `{data.NAME}`.

### ColDefine
```
{
    "methods": [ {
            "if": [
                { "name": "IsNotEmpty" }
            ],
            "do": [
                { "name": "string", "target": "{data.custumName01}.stringField" }
            ]
        }
    ],
    "onEnd": [ { ... } ]
}
```
### Method Hooks
- `methods` : Default method. It will execute every column cell.
- `onStart` : It will execute when table parse start.
- `onEnd` : It will execute when table parse end.

### Method Types
There are few method types can use on if/do name.

Do : 
- `int` : Parse int and fill in target.
- `float` : Parse float and fill in target.
- `bool` : Parse bool and fill in target.
- `string` : Parse string and fill in target.
- `object` : Parse asset object and fill in target.
- `Vector2` : Parse vector2 and fill in target.
- `Sprite` : Parse sprite and fill in target.
- `enum` : Parse enum and fill in target.
- `Method`
- `ForMethod`
- `CreateAsset` : Create asset.
- `SaveAssets` : Save modified assets.
- `Reset` : Reset target feild.
- `ClearArray` : Clear target array.
- `Print` : Print log in console.
- `Invoke` : Invoke method from root object.
- `SetRoot` : Set root object.

If :
- `NotEmpty` : If value is not empty, do method.
- `IsEmpty` : If value is empty, do method.
- `IsEmptyString` : If value is empty string, do method.
- `IsNotEmptyString` : If value is not empty string, do method.
- `IsFirst` : If value is the first in this column, do method.
- `IsLast` : If value is the last in this column, do method.
- `IsExist` : If the target asset exist, do method.
- `IsNotExist` : If the target asset doesn't exist, do method.
- `IsTrue` : If value is true, do method.
- `IsFalse` : If value is false, do method.
- `IsEqual` : If value equal target value, do method.
- `IsNotEqual` : If value doesn't equal target value, do method.