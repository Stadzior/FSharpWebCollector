module DbContext

open System.Data.SQLite
open System

let getAllSites (connection : SQLiteConnection) =     
    let query = "SELECT Id, Url, PageRank FROM sites;"
    let command = new SQLiteCommand(query, connection)
    let reader = command.ExecuteReader();
    let output = seq { while reader.Read() do yield (reader.["Id"], reader.["Url"], reader.["PageRank"]) }
    output

let getAllWords (connection : SQLiteConnection) =     
    let query = "SELECT w.Id, w.Word, w.WordCount, s.PageRank, s.Url FROM words w inner join sites s on w.siteId = s.Id;"
    let command = new SQLiteCommand(query, connection)
    let reader = command.ExecuteReader();
    let output = seq { while reader.Read() do yield (reader.["Id"], reader.["Word"], reader.["WordCount"], reader.["PageRank"], reader.["Url"]) }
    output

let getSiteIdByUrl (url : string, connection : SQLiteConnection) =
    let query = "SELECT Id FROM sites where url = '" + url + "' limit 1;"
    let command = new SQLiteCommand(query, connection)
    let reader = command.ExecuteReader();
    seq { while reader.Read() do yield System.Convert.ToInt32(reader.["Id"]) } |> Seq.find(fun _ -> true)

let insertWord (word : string, wordCount : int, siteId : int, connection : SQLiteConnection) =        
    let wordWithEscapedQuote = word.Replace("'", "''")
    let query = "insert into words (word, wordcount, siteId) values ('" + wordWithEscapedQuote + "'," + wordCount.ToString() + "," + siteId.ToString() + ");"
    let command = new SQLiteCommand(query, connection)
    command.ExecuteNonQuery() |> ignore

let updatePageRank (id : int, pageRank : float, connection : SQLiteConnection) =    
    let query = "update sites set pagerank = " + pageRank.ToString() + " where Id = " + id.ToString() + ";"
    let command = new SQLiteCommand(query, connection)
    command.ExecuteNonQuery() |> ignore