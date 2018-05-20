open System
open Akkling
open Akkling.Persistence
open Newtonsoft.Json.Linq

let eventType = "eventType"
let anyEventTag = "anyEventTag"

let private deserializeEvent (evt : obj) =
    let json = evt :?> Newtonsoft.Json.Linq.JObject
    let objectType = json.[eventType]
    let typ = if objectType = null then null else Type.GetType(objectType.ToString())
    match typ with
    | null -> box json
    | typ -> json.ToObject(typ)
    
type EventAdapter(__ : Akka.Actor.ExtendedActorSystem) =

    interface Akka.Persistence.Journal.IEventAdapter with

        member __.Manifest(_ : obj) = 
            let manifestType = typeof<Newtonsoft.Json.Linq.JObject>
            sprintf "%s,%s" manifestType.FullName <| manifestType.Assembly.GetName().Name

        member __.ToJournal(evt : obj) : obj = 
            let jObject = Newtonsoft.Json.Linq.JObject.FromObject(evt)
            jObject.AddFirst(Newtonsoft.Json.Linq.JProperty(eventType, evt.GetType().FullName));
            Akka.Persistence.Journal.Tagged(box jObject, [| anyEventTag |]) :> obj

        member __.FromJournal(evt : obj, _ : string) : Akka.Persistence.Journal.IEventSequence =
            if evt :? Newtonsoft.Json.Linq.JObject then
                Akka.Persistence.Journal.EventSequence.Single(deserializeEvent evt)
            else
                Akka.Persistence.Journal.EventSequence.Empty

let adapterRef = typeof<EventAdapter>.FullName


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
              json-adapter = """ + quot + adapterRef + """, Upload"
            }            
        }
    }
    actor {
        ask-timeout = 2000
        debug {
            # receive = on
            # autoreceive = on
            # lifecycle = on
            # event-stream = on
            unhandled = on
        }
    }

    }"""

type ChatEvent =
    { Message : string }

type ChatCommand =
    | Message of string
    | GetMessages

type ChatMessage =
    | Command of ChatCommand
    | Event of ChatEvent


[<EntryPoint>]
let main argv =
    printfn "Persistance test"

    let system = System.create "chatapp" config

    let actor (mailbox: Eventsourced<_>) =
        let rec loop state = actor {
            let! (msg: obj) = mailbox.Receive()
            match msg with
            | :? ChatMessage as cm ->
            match cm with
                | Event evt when mailbox.IsRecovering() ->
                    printfn "Got event while recovering"
                    return! loop (evt.Message :: state)
                | Event(evt) ->
                    return! loop (evt.Message :: state)
                | Command(cmd) ->
                    match cmd with
                    | GetMessages ->
                        mailbox.Sender() <! state
                        return! loop state
                    | Message msg -> return Persist <| box(Event { Message = msg })

            | :? LifecycleEvent as x -> printfn "Lifecycle %A" x; return! loop state
            | :? PersistentLifecycleEvent as x -> printfn "Persistence Lifecycle %A" x; return! loop state          
            | :? JObject as j when mailbox.IsRecovering() ->
                match j.ToObject<ChatMessage>() with
                | Event evt ->
                    do printfn "%s" "Got JObject while recovering"
                    return! loop (evt.Message :: state)
                | x ->
                    printfn "Unhandled JObject payload %A" x;
                    return! loop state
            | x ->
                printfn "Catch all %A" x;
                return! loop state
        }
        loop []

    let chat : ChatMessage IActorRef =
        spawn system "chat-1" <| propsPersist actor |> retype

    chat <! Command (Message <| sprintf "New session started %A" System.DateTime.Now)
    async {
        let! (reply: string list) = chat <? Command GetMessages
        printfn "Messages:"
        reply |> List.iter (printfn "  %s")
    } |> Async.RunSynchronously

    printfn "Press enter to quit"
    ignore <| System.Console.ReadLine()

    0