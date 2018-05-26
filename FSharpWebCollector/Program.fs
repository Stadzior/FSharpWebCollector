// Learn more about F# at http://fsharp.org

open System
open System.Data.SQLite
open Migration

[<Literal>]
let dbName = "MyIndexedWebDb"
[<Literal>]
let dbFilePath = __SOURCE_DIRECTORY__ + "\\" + dbName + ".db"

[<Literal>]
let connectionString = "Data Source=" + dbFilePath + ";Version=3;foreign keys=true"   

let displayAllSites (connection : SQLiteConnection) =     
    let query = "SELECT * FROM sites;"
    let command = new SQLiteCommand(query, connection)
    let reader = command.ExecuteReader();
    let output = seq { while reader.Read() do yield (reader.["Id"], reader.["Url"], reader.["PageRank"]) }
    output |> Seq.iter(fun x -> Console.WriteLine(x))

let displayAllWords (connection : SQLiteConnection) =     
    let query = "SELECT w.Id, w.Word, w.WordCount, s.Url FROM words w inner join sites s on w.siteId = s.Id;"
    let command = new SQLiteCommand(query, connection)
    let reader = command.ExecuteReader();
    let output = seq { while reader.Read() do yield (reader.["Id"], reader.["Word"], reader.["WordCount"], reader.["Url"]) }
    output |> Seq.iter(fun x -> Console.WriteLine(x))

[<EntryPoint>]
let main argv =
    System.IO.File.Delete(dbFilePath)
    SQLiteConnection.CreateFile(dbFilePath)
    use connection = new SQLiteConnection(connectionString)
    connection.Open()
    if initialMigration(connection) then
        Console.WriteLine("Initial migration succeed.\n-------------------------------------------------------")
        let pageRankDepth = 
                try
                    System.Convert.ToInt32(argv.[0])              
                with
                    | :? FormatException as _ex -> 0 
        if pageRankDepth > 0 then
            seedSites(connection)
            displayAllSites(connection)
            seedWords(connection)
            displayAllWords(connection)
            Console.ReadKey()     
            1   
        else
            Console.WriteLine("Page rank depth needs to be a positive integer.")
            Console.ReadKey()  
            0
    else
        Console.WriteLine("Initial migration failed.")
        Console.ReadKey()
        0
    