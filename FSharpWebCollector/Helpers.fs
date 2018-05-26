module Helpers

open FSharp.Data
open System
open System.Threading
open System.Threading.Tasks
open System.Net
open System.IO
open System.Text.RegularExpressions

// e.g. aaaa-aaa'a
let wordPattern = "(^[\w]$)|(^[\w](\w|\-|\')*[\w]$)"

type Microsoft.FSharp.Control.Async with
    static member AwaitTask (t : Task<'T>, timeout : int) =
        async {
            use cts = new CancellationTokenSource()
            use timer = Task.Delay (timeout, cts.Token)
            let! completed = Async.AwaitTask <| Task.WhenAny(t, timer)
            if completed = (t :> Task) then
                cts.Cancel ()
                let! result = Async.AwaitTask t
                return Some result
            else return None
        }

let tryGetBodyFromUrlAsyncWithTimeout(url : string, timeout : int) : string * HtmlNode option =
     (url, match (Async.AwaitTask(HtmlDocument.AsyncLoad(url) |> Async.StartAsTask, timeout) |> Async.RunSynchronously) with                
                | Some x -> x.TryGetBody()
                | _ -> None)

let tryGetBodyFromUrl(url : string) : string * HtmlNode option =
    try
        tryGetBodyFromUrlAsyncWithTimeout(url, 3000)
    with
        | :? WebException as _ex -> 
                try
                    tryGetBodyFromUrlAsyncWithTimeout(url.Replace("www.",""), 3000)
                with
                    | :? WebException as _ex -> (url.Replace("www.",""), None)
                    | :? UriFormatException as _ex -> (url.Replace("www.",""), None)
        | :? AggregateException as _ex ->        
                try
                    tryGetBodyFromUrlAsyncWithTimeout(url.Replace("www.",""), 3000)
                with
                    | :? WebException as _ex -> (url.Replace("www.",""), None)
                    | :? UriFormatException as _ex -> (url.Replace("www.",""), None)
                    | :? AggregateException as _ex -> (url.Replace("www.",""), None)
        | :? UriFormatException as _ex -> (url, None)        
        | :? NotSupportedException as _ex -> (url, None)
        | :? ArgumentException as _ex -> (url, None)
        | :? FileNotFoundException as _ex -> (url, None)
        | :? DirectoryNotFoundException as _ex -> (url, None)
        | :? IOException as _ex -> (url, None)
        | :? CookieException as _ex -> (url, None)

let getAllWordsFromNode (node : HtmlNode) =
    node.Descendants()
        |> Seq.filter(fun x -> x.Descendants() |> Seq.isEmpty)
        |> Seq.map(fun x -> x.InnerText().Split(' '))
        |> Seq.concat
        |> Seq.map(fun x -> x.Trim().ToLower())
        |> Seq.filter(fun x -> Regex.IsMatch(x, wordPattern))         
        |> Seq.countBy(fun x -> x)
        |> Seq.sortBy(fun x -> snd(x))