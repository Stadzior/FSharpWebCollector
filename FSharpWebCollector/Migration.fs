module Migration

open System.Data.SQLite
open System.IO
open FSharp.Data
open DbContext
open Helpers

[<Literal>]
let sitesListFilePath = __SOURCE_DIRECTORY__ + "\\SitesList.txt"

let initialMigration (connection : SQLiteConnection) =     
    let sitesQuery = "create table sites (Id integer PRIMARY KEY AUTOINCREMENT, Url text NOT NULL, PageRank real);"
    let sitesCommand = new SQLiteCommand(sitesQuery, connection)
    sitesCommand.ExecuteNonQuery() |> ignore
    let wordsQuery = "create table words (Id integer PRIMARY KEY AUTOINCREMENT, Word text NOT NULL, WordCount integer Check(WordCount>0), siteId integer, FOREIGN KEY(siteId) REFERENCES sites(Id));"
    let wordsCommand = new SQLiteCommand(wordsQuery, connection)    
    wordsCommand.ExecuteNonQuery() |> ignore
    let checkIfTablesExistsQuery = "SELECT name FROM sqlite_master WHERE type='table';"
    let checkIfTablesExistsCommand = new SQLiteCommand(checkIfTablesExistsQuery, connection)
    let reader = checkIfTablesExistsCommand.ExecuteReader();
    let output = seq { while reader.Read() do yield reader.["name"] }
    let sitesExists = output |> Seq.exists(fun x -> x.Equals("sites"))    
    let wordsExists = output |> Seq.exists(fun x -> x.Equals("words"))
    sitesExists && wordsExists

let seedSites (connection : SQLiteConnection) =             
        let sitesToIndex = seq {
                use sr = new StreamReader(sitesListFilePath)
                while not sr.EndOfStream do
                    yield sr.ReadLine()
            }    
        sitesToIndex |> Seq.iter(fun x ->                                             
                                        let sitesQuery = "insert into sites (url) values ('" + x + "');"
                                        let sitesCommand = new SQLiteCommand(sitesQuery, connection)
                                        sitesCommand.ExecuteNonQuery() |> ignore)

let seedWords (bodies : (int * HtmlNode)[], connection : SQLiteConnection) =
    bodies |> Seq.iter(fun x ->
                            getAllWordsFromNode(snd(x)) 
                                |> Seq.iter(fun y -> insertWord(fst(y), snd(y), fst(x), connection)))
                            
                            
                            