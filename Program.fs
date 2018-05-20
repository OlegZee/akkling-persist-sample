open Akkling
open Akkling.Persistence

let config = Configuration.parse """akka {  
    stdout-loglevel = WARNING
    loglevel = DEBUG
    persistence.journal {
        plugin = "akka.persistence.journal.sqlite"
        sqlite {
            class = "Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite"
            plugin-dispatcher = "akka.actor.default-dispatcher"
            connection-string = "Data Source=JOURNAL.db;cache=shared;"
            connection-timeout = 30s
            schema-name = dbo
            table-name = event_journal
            auto-initialize = on
            timestamp-provider = "Akka.Persistence.Sql.Common.Journal.DefaultTimestampProvider, Akka.Persistence.Sql.Common"
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

    let system = System.create "chatapp" (config.WithFallback <| Akkling.Configuration.defaultConfig())

    let actor (ctx: Eventsourced<_>) =
        let rec loop state = actor {
            let! msg = ctx.Receive()
            match msg with
            | Event(evt) when ctx.IsRecovering() ->
                printfn "Got event while recovering"
                return! loop (evt.Message :: state)
            | Event(evt) ->
                return! loop (evt.Message :: state)
            | Command(cmd) ->
                match cmd with
                | GetMessages ->
                    ctx.Sender() <! state
                    return! loop state
                | Message msg -> return Persist (Event { Message = msg })
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