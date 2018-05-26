// Learn more about F# at http://fsharp.org

open System
open System.Data.SQLite
open Migration
open DbContext
open FSharp.Data
open System.IO

[<Literal>]
let dbName = "MyIndexedWebDb"
[<Literal>]
let dbFilePath = __SOURCE_DIRECTORY__ + "\\" + dbName + ".db"

[<Literal>]
let connectionString = "Data Source=" + dbFilePath + ";Version=3;foreign keys=true"   

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
            Console.WriteLine("Selected depth for page rank: " + pageRankDepth.ToString())  
            seedSites(connection)    
            Console.WriteLine("Finished sites seed.") 
            let urls = getAllSides(connection) |> Seq.map(fun x -> 
                                                                let id, url, _ = x
                                                                (System.Convert.ToInt32(id), url.ToString()))
            let bodies = urls |> Seq.map(fun x -> 
                                            Console.WriteLine("Attempting to reach " + snd(x) + "...")
                                            (fst(x),Helpers.tryGetBodyFromUrl(snd(x))))                                            
                                            |> Seq.toArray
            if bodies |> Seq.exists(fun x -> snd(snd(x)).IsNone) then
                Console.WriteLine("Following urls are impossible to reach (incorrect url?) or lacks body tag (not a proper html file?):")
                bodies 
                    |> Seq.filter(fun x -> snd(snd(x)).IsNone) 
                    |> Seq.iter(fun x -> Console.WriteLine(fst(snd(x))))
                Console.WriteLine("Proceeding with reachable ones...")
            
            let reachableBodies = 
                bodies 
                    |> Seq.filter(fun x -> snd(snd(x)).IsSome)
                    |> Seq.map(fun x -> (fst(x), match snd(snd(x)) with
                                                        | Some x -> x
                                                        | None -> Unchecked.defaultof<HtmlNode>))
                    |> Seq.sortBy(fun x -> fst(x)) 
                    |> Seq.toArray  
                    
            seedWords(reachableBodies, connection)
            File.AppendAllLines(__SOURCE_DIRECTORY__ + "\\wordsOutput.txt", getAllWords(connection) |> Seq.map(fun x -> x.ToString()))
            Console.WriteLine("Finished words seed.")
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
    