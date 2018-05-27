// Learn more about F# at http://fsharp.org

open System
open System.Data.SQLite
open Migration
open DbContext
open FSharp.Data
open System.IO
open System.Diagnostics

[<Literal>]
let dbName = "MyIndexedWebDb"
[<Literal>]
let dbFilePath = __SOURCE_DIRECTORY__ + "\\" + dbName + ".db"

[<Literal>]
let connectionString = "Data Source=" + dbFilePath + ";Version=3;foreign keys=true"   

[<EntryPoint>]
let main argv =
    if not(System.IO.File.Exists(dbFilePath)) then
        SQLiteConnection.CreateFile(dbFilePath)
    use connection = new SQLiteConnection(connectionString)
    connection.Open()
    if initialMigration(connection) then
        Console.WriteLine("Initial migration succeed.\n-------------------------------------------------------")
        let depth = 
                try
                    System.Int32.Parse(argv.[0])          
                with
                    | :? Exception as _ex -> 0 
        if depth > 0 then
            let alpha = 
                try
                    System.Double.Parse(argv.[1].Replace(".",","))                    
                with
                    | :? Exception as _ex -> 0.0
            if alpha > 0.0 && alpha < 1.0 then
                Console.WriteLine("Selected depth for page rank: " + depth.ToString())  
                let stopWatch = new Stopwatch()
                stopWatch.Start()
                seedSites(connection)    
                Console.WriteLine("Finished sites seed.") 
                let urls = getAllSites(connection) |> Seq.map(fun x -> 
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
                        |> Seq.map(fun x -> (fst(x), fst(snd(x)), match snd(snd(x)) with
                                                            | Some x -> x
                                                            | None -> Unchecked.defaultof<HtmlNode>))
                        |> Seq.sortBy(fun x -> 
                                            let _, url, _ = x
                                            url) 
                        |> Seq.toArray  
                    
                seedWords(reachableBodies, connection)
                Console.WriteLine("Finished words seed.")
                seedPageRanks(reachableBodies, 0.85, depth, false, connection)                   
                Console.WriteLine("Finished Page Rank calculations.")
                File.AppendAllLines(__SOURCE_DIRECTORY__ + "\\sitesOutput.txt", getAllSites(connection) |> Seq.map(fun x -> x.ToString()))
                File.AppendAllLines(__SOURCE_DIRECTORY__ + "\\wordsOutput.txt", getAllWords(connection) |> Seq.map(fun x -> x.ToString()))
                stopWatch.Stop()
                File.AppendAllLines(__SOURCE_DIRECTORY__ + "\\metrics.txt", ["Execution time: " + stopWatch.Elapsed.Hours.ToString() + "h " + stopWatch.Elapsed.Minutes.ToString() + "m " + stopWatch.Elapsed.Seconds.ToString() + "s"])
                Console.ReadKey()        
                1  
            else
                Console.WriteLine("Alpha needs to be a real value between 0.0 and 1.0.")
                Console.ReadKey()  
                0
        else
            Console.WriteLine("Page rank depth needs to be a positive integer.")
            Console.ReadKey()  
            0
    else
        Console.WriteLine("Initial migration failed.")
        Console.ReadKey()
        0
    