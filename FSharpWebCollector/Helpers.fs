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

let fullUrlPattern = "(?i)^((https|http)://)?(www\.)?\w[\w\-\,\„\”\!\?\&\=\%\;]*(\.\w([\-\w\,\„\”\!\?\&\=\%\;]*\w)*)*\.\w{2,3}[\/]?$"
let softFullUrlPattern = "(?i)((https|http)://)?(www\.)?\w[\w\-\,\„\”\!\?\&\=\%\;]*(\.\w([\-\w\,\„\”\!\?\&\=\%\;]*\w)*)*\.\w{2,3}[\/]?"

// e.g. /aaa/bb/c-c/ddd.html
let relativeUrlPattern = "(?i)^([\/]?[\w\-\,\„\”\!\?\&\=\%\;]+)+(\.[\w]{1,4})?[\/]?$"
// e.g. aa.com, aa-aa.com.pl, aaaaaaa.co.uk
let baseHostUrlPattern = "(?i)^[\w\-\,\„\”\!\?\&\=\%\;]*(\.\w([\-\w\,\„\”\!\?\&\=\%\;]*\w)*)*\.\w{2,3}[\/]?$"
// same as above but removes "exact match" constraint
let softBaseHostUrlPattern = "(?i)[\w\-\,\„\”\!\?\&\=\%\;]*(\.\w([\-\w\,\„\”\!\?\&\=\%\;]*\w)*)*\.\w{2,3}[\/]?"

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

let executeWithTimeout(task : Async<'T>, timeout : int) =
        Async.AwaitTask(task |> Async.StartAsTask, timeout) |> Async.RunSynchronously

let tryGetBodyFromUrlAsyncWithTimeout(url : string, timeout : int) : string * HtmlNode option =
     (url, match executeWithTimeout(HtmlDocument.AsyncLoad(url), timeout) with                
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

let findInMapByUrl(url : string, map : (string * seq<string>) []) =
    map |> Array.find(fun x -> fst(x).Equals(url))

let getLinksCount(url : string, map : (string * seq<string>) []) = 
    let count = snd(findInMapByUrl(url, map)) |> Seq.length
    if count > 0 then
        count
    else
        map.Length

let normalizeUrl (inputUrl : string) =
    let uri =
        match Uri.TryCreate(inputUrl, UriKind.Absolute) with
        | true, str -> Some str
        | _ ->  let url' = Uri.TryCreate("http://" + inputUrl, UriKind.Absolute)
                match url' with
                | true, str -> Some str
                | _ -> let url'' = Uri.TryCreate(inputUrl.Replace("www.",""), UriKind.Absolute)
                       match url'' with
                       | true, str -> Some str
                       | _ -> let url''' = if inputUrl.EndsWith("/") then
                                                Uri.TryCreate(inputUrl.Substring(0,inputUrl.Length-1).Replace("www",""), UriKind.Absolute)
                                            else
                                                Uri.TryCreate("nope", UriKind.Absolute)
                              match url''' with
                              | true, str -> Some str
                              | _ -> None
    match uri with
    | Some x -> let host = x.Host.Replace("www.","")
                let path = x.AbsolutePath
                let host' = Regex(baseHostUrlPattern, RegexOptions.RightToLeft).Match(host).Value
                let pattern = "(?i)^https?://((www\.)|([^\.]+\.))" + Regex.Escape(host') + "[^\"]*"
                let m = Regex(pattern).IsMatch(string x)
                match m with
                | true -> "http://" + host + path
                | false -> "http://www." + host + path
    | None -> raise(UriFormatException(inputUrl))

let transformRelativeToFullUrl (inputUrl : string, baseUrl : string) =
    if (inputUrl.StartsWith('/') || baseUrl.EndsWith('/')) then
        normalizeUrl(baseUrl + inputUrl)
    else
        normalizeUrl(baseUrl + "/" + inputUrl)

let getExplorableUrls (urls : seq<string>, baseUrl : string) = 
    urls
        |> Seq.map(fun x -> x.Replace("%20","").Replace(" ", ""))
                                |> Seq.map(fun x -> 
                                    if (x.Contains("?")) then
                                        x.Substring(0, x.IndexOf('?'))
                                    else
                                        x)                             
                                |> Seq.filter(fun x -> not(String.IsNullOrWhiteSpace(x) || x.Contains("mailto") || x.Contains("#") || x.EndsWith(".pdf") || x.EndsWith(".jpg")))
                                |> Seq.map(fun x -> 
                                    if Regex.IsMatch(x, relativeUrlPattern) then
                                        transformRelativeToFullUrl(x, baseUrl)
                                    else
                                        x)
                                |> Seq.distinct

let getNormalizedBaseUrl (inputUrl : string) =
    normalizeUrl(Regex.Match(inputUrl, softBaseHostUrlPattern).Value)

let getLinksFromNode (includeExternal : bool, includeInternal : bool, urlNodeTuple : string * HtmlNode) =
    snd(urlNodeTuple).Descendants["a"]
        |> Seq.choose(fun x -> 
            x.TryGetAttribute("href")
                |> Option.map(fun x -> x.Value()))
        |> Seq.filter (fun x -> not(["%";",";";";"!"] |> Seq.exists(fun y -> x.Contains(y))))
        |> Seq.filter (fun x ->
                            let asyncMatching = executeWithTimeout(async {
                                let relativeMatch = Regex.IsMatch(x, relativeUrlPattern)
                                let fullMatch = Regex.Match(fst(urlNodeTuple), fullUrlPattern)
                                let softMatch = Regex.Match(x, softFullUrlPattern)
                                return (includeExternal || relativeMatch || fullMatch.Value.Equals(softMatch.Value))
                            }, 1000)
                            if (asyncMatching).IsNone then                 
                                Console.WriteLine(x + " matching timed out.")
                                false
                            else
                                asyncMatching.Value                           
                                )
        |> Seq.filter (fun x ->
                            let asyncMatching = executeWithTimeout(async {
                                let relativeMatch = Regex.IsMatch(x, relativeUrlPattern)
                                let fullMatch = Regex.Match(fst(urlNodeTuple), fullUrlPattern)
                                let softMatch = Regex.Match(x, softFullUrlPattern)
                                return (includeInternal || not(relativeMatch) || not(fullMatch.Value.Equals(softMatch.Value)))
                            }, 1000)
                            if (asyncMatching).IsNone then                 
                                Console.WriteLine(x + " matching timed out.")
                                false
                            else
                                asyncMatching.Value                           
                                )
        |> Seq.distinct

let rec getNetMap(startingPoint : string * HtmlNode, depth : int) =
    (if depth < 1 then
        [|(fst(startingPoint), Seq.empty<string>)|]
    else
        let normalizedLinks = getExplorableUrls(getLinksFromNode(true, false, startingPoint), getNormalizedBaseUrl(fst(startingPoint)))
                                |> Seq.map(fun x ->
                                    let explorableLink = Regex.Match(x, softFullUrlPattern).Value
                                    if explorableLink.EndsWith('/') then
                                        explorableLink.Remove(explorableLink.Length-1)
                                    else
                                        explorableLink)
                                |> Seq.distinct
                                |> Seq.filter(fun x -> not(String.IsNullOrWhiteSpace(x) || Regex.Match(fst(startingPoint), fullUrlPattern).Value.Equals(x)))
                                |> Seq.map(fun x -> tryGetBodyFromUrl(x))
                                |> Seq.filter(fun x -> snd(x).IsSome)
                                |> Seq.map(fun x -> (fst(x), match snd(x) with
                                                        | Some x -> x
                                                        | None -> Unchecked.defaultof<HtmlNode>))
                                |> Seq.toArray
        let subNetMaps = [|[|(fst(startingPoint), normalizedLinks |> Seq.map(fun x -> fst(x)))|]; normalizedLinks |> Array.collect(fun x -> getNetMap(x, depth - 1))|] 
                            |> Array.collect(fun x -> x) 
        subNetMaps
            |> Array.map(fun x -> fst(x))
            |> Array.distinct
            |> Array.map(fun x -> (x, subNetMaps 
                                        |> Seq.filter(fun y -> fst(y).Equals(x))
                                        |> Seq.collect(fun y -> snd(y))
                                        |> Seq.distinct)))

let rec getPageRank(url : string, map : (string * seq<string>) [], alpha : float) =
    let firstThingy = (1.0-alpha)/float(map.Length)
    let minorPageRanksTuples = 
           snd(findInMapByUrl(url, map)) |> Seq.map(fun x -> (getPageRank(x, map, alpha), float(getLinksCount(x, map)))) |> Seq.toArray
    let sumOfPageRanks = (minorPageRanksTuples |> Array.sumBy(fun x -> fst(x)/snd(x)))
    let secondThingy = 
        if sumOfPageRanks > 0.0 then
            alpha * sumOfPageRanks
        else
            alpha
    firstThingy + secondThingy