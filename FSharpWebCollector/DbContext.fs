module DbContext

open System.Data.SQLite

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

let insertSite(url : string, connection : SQLiteConnection) =
    let sitesQuery = "insert into sites (url) " + 
                     "select '" + url + "' where not exists "+
                     "(select 1 from sites where url = '" + url + "');"
    let sitesCommand = new SQLiteCommand(sitesQuery, connection)
    sitesCommand.ExecuteNonQuery() |> ignore

let insertWord (word : string, wordCount : int, siteId : int, connection : SQLiteConnection) =        
    let wordWithEscapedQuote = word.Replace("'", "''")
    let query = "insert into words (word, wordcount, siteId) select '" + wordWithEscapedQuote + "', " + wordCount.ToString() + ", " + siteId.ToString() + 
                " where not exists " +
                "(select 1 from words where " +
                "word = '" + wordWithEscapedQuote +
                "' and wordcount = " + wordCount.ToString() + 
                " and siteId = " + siteId.ToString() + ");"
    let command = new SQLiteCommand(query, connection)
    command.ExecuteNonQuery() |> ignore

let updatePageRank (id : int, pageRank : float, connection : SQLiteConnection) =    
    let query = "update sites set pagerank = " + pageRank.ToString().Replace(",",".") + " where Id = " + id.ToString() + ";"
    let command = new SQLiteCommand(query, connection)
    command.ExecuteNonQuery() |> ignore

let isPageRankAlreadyCalculated (id : int, connection : SQLiteConnection) =
    let query = "SELECT Id, PageRank FROM sites where id = " + id.ToString() + " and PageRank is not null limit 1;"
    let command = new SQLiteCommand(query, connection)
    let reader = command.ExecuteReader();
    not(seq { while reader.Read() do yield System.Convert.ToInt32(reader.["Id"]) } |> Seq.isEmpty)
