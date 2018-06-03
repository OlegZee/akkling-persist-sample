open System
open Akkling
open Akkling.Persistence
open Newtonsoft.Json.Linq
open Newtonsoft.Json
open System.IO

type EventAdapter(__ : Akka.Actor.ExtendedActorSystem) =

    interface Akka.Persistence.Journal.IEventAdapter with

        member __.Manifest(_ : obj) = 
            let manifestType = typeof<Newtonsoft.Json.Linq.JObject>
            sprintf "%s,%s" manifestType.FullName <| manifestType.Assembly.GetName().Name

        member __.ToJournal(evt : obj) : obj = 
            new JObject(
                new JProperty("evtype", evt.GetType().FullName),
                new JProperty("value", JsonConvert.SerializeObject(evt))
            )
            :> obj

        member __.FromJournal(evt : obj, _ : string) : Akka.Persistence.Journal.IEventSequence =
            match evt with
            | :? JObject as jobj ->
                match jobj.TryGetValue("evtype") with
                    | false, _ -> box jobj
                    | _, typ ->
                        let t = Type.GetType(typ.ToString())
                        let value = jobj.["value"].ToString()
                        JsonConvert.DeserializeObject(value, t)
                |> Akka.Persistence.Journal.EventSequence.Single

            | _ ->
                Akka.Persistence.Journal.EventSequence.Empty

let quot = "\""
let config = Configuration.parse """akka {  
    stdout-loglevel = WARNING
    loglevel = DEBUG
    persistence.journal {
        plugin = "akka.persistence.journal.sqlite"
        sqlite {
            class = "Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite"
            connection-string = "Data Source=JOURNAL.db;cache=shared;"
            auto-initialize = on
            event-adapters {
              json-adapter = "Program+EventAdapter, akka-persist"
            }            
            event-adapter-bindings {
              # to journal
              "System.Object, mscorlib" = json-adapter
              # from journal
              "Newtonsoft.Json.Linq.JObject, Newtonsoft.Json" = [json-adapter]
            }
        }
    }
    actor {
        ask-timeout = 200000
        debug {
            # receive = on
            # autoreceive = on
            # lifecycle = on
            # event-stream = on
            unhandled = on
        }
    }

    }"""

type ChannelId = ChannelId of int
type UserId = UserId of string

type ChatEvent =
    { Message : string; User: UserId }

type ChatCommand =
    | Message of string
    | GetMessages

type ChatMessage =
    | Command of ChatCommand
    | Event of ChatEvent * ChannelId

[<EntryPoint>]
let main argv =
    printfn "Persistance test"

    let system = System.create "chatapp" config

    let actor (ctx: Eventsourced<_>) =
        let rec loop state = actor {
            let! msg = ctx.Receive()
            match msg with
            // | Event(evt) when ctx.IsRecovering() ->
            //     return! loop (evt.Message :: state)
            | Event (evt, chan) ->
                return! loop (evt.Message :: state)
            | Command(cmd) ->
                match cmd with
                | GetMessages ->
                    ctx.Sender() <! state
                    return! loop state
                | Message msg -> return Persist (Event ({ Message = msg; User = UserId "101" }, ChannelId 102))
        }
        loop []

    let chat = spawn system "chat-1" <| propsPersist actor

    chat <! Command (Message <| sprintf "New session started %A" System.DateTime.Now)
    async {
        let! (reply: string list) = chat <? Command GetMessages
        printfn "Messages:"
        reply |> List.iter (printfn "  %s")
    } |> Async.RunSynchronously

    printfn "Press enter to quit"
    ignore <| System.Console.ReadLine()

    0