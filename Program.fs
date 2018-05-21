open System
open Akkling
open Akkling.Persistence

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

type ChatEvent =
    { Message : string }

type ChatCommand =
    | Message of string
    | GetMessages

type ChatMessage =
    | Command of ChatCommand
    | Event of ChatEvent

type Message = {
    message: ChatMessage
}

[<EntryPoint>]
let main argv =
    printfn "Persistance test"

    let system = System.create "chatapp" config

    let actor (ctx: Eventsourced<_>) =
        let rec loop state = actor {
            let! {message = msg} = ctx.Receive()
            match msg with
            | Event(evt) when ctx.IsRecovering() ->
                return! loop (evt.Message :: state)
            | Event(evt) ->
                return! loop (evt.Message :: state)
            | Command(cmd) ->
                match cmd with
                | GetMessages ->
                    ctx.Sender() <! state
                    return! loop state
                | Message msg -> return Persist ({message = Event { Message = msg }})
        }
        loop []

    let chat = spawn system "chat-1" <| propsPersist actor

    chat <! { message = Command (Message <| sprintf "New session started %A" System.DateTime.Now) }
    async {
        let! (reply: string list) = chat <? { message = Command GetMessages }
        printfn "Messages:"
        reply |> List.iter (printfn "  %s")
    } |> Async.RunSynchronously

    printfn "Press enter to quit"
    ignore <| System.Console.ReadLine()

    0