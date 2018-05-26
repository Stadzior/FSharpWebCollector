// Learn more about F# at http://fsharp.org

open System
open System.Data.SQLite

[<Literal>]
let dbName = "MyIndexedWebDb"
[<Literal>]
let dbFilePath = __SOURCE_DIRECTORY__ + "\\" + dbName + ".db"

[<Literal>]
let connectionString = "Data Source=" + dbFilePath + ";Version=3;foreign keys=true"   

let initialMigration (connection : SQLiteConnection) =     
    let sitesQuery = "create table sites (Id integer PRIMARY KEY, PageRank real NOT NULL);"
    let sitesCommand = new SQLiteCommand(sitesQuery, connection)
    sitesCommand.ExecuteNonQuery() |> ignore
    let wordsQuery = "create table words (Id integer PRIMARY KEY, Word text NOT NULL, WordCount integer Check(WordCount>0), siteId integer, FOREIGN KEY(siteId) REFERENCES sites(Id));"
    let wordsCommand = new SQLiteCommand(wordsQuery, connection)    
    wordsCommand.ExecuteNonQuery() |> ignore
    let checkIfTablesExistsQuery = "SELECT name FROM sqlite_master WHERE type='table';"
    let checkIfTablesExistsCommand = new SQLiteCommand(checkIfTablesExistsQuery, connection)
    let reader = checkIfTablesExistsCommand.ExecuteReader();
    let output = seq { while reader.Read() do yield reader.["name"] }
    let sitesExists = output |> Seq.exists(fun x -> x.Equals("sites"))    
    let wordsExists = output |> Seq.exists(fun x -> x.Equals("words"))
    sitesExists && wordsExists

[<EntryPoint>]
let main argv =
    System.IO.File.Delete(dbFilePath)
    SQLiteConnection.CreateFile(dbFilePath)
    use connection = new SQLiteConnection(connectionString)
    connection.Open()
    if initialMigration(connection) then
        Console.WriteLine("Initial migration succeed.")
        Console.ReadKey()
        1        
    else
        Console.WriteLine("Initial migration failed.")
        Console.ReadKey()
        0