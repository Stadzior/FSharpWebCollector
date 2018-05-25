// Learn more about F# at http://fsharp.org

open System

[<Literal>]
let connectionString = "Data Source=" + __SOURCE_DIRECTORY__ + "MyIndexedWebDb.db" + "Version=3;foreign keys=true"   

type sql = SqlDataProvider<
                Common.DatabaseProviderTypes.SQLITE, 
                SQLiteLibrary = SystemDataSQLite,
                ConnectionString = connectionString, 
                ResolutionPath = resolutionPath, 
                CaseSensitivityChange = Common.CaseSensitivityChange.ORIGINAL>


let context = sql.GetDataContext()

[<EntryPoint>]
let main argv =