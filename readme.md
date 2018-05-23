# Implementing persistent Akka actors on F#

My first attempts to implement persistent actors like any attempts to google the solution failed.
This reposotory provides several solutions.

## Reasons

Akka.NET implements persistance relies on event sourcing, which idea is to serialize the history of changes in journal and "replay" the journal when actor is restarted. Akka.NET writes commands (or messages) to a database and it has to serialize message object to a byte array and then restore the object from that byte array.

This serialization is performed by a serializer, which can be configured via akka configuration facility. Default serializer uses Json as a media and uses famous Newtonsoft Json serializer.

Also Akka Persistance provides configurable "event adapters" so that inner messages types are untied from persistance object model. Default binding is an "identity" binding.

Akka Persistance utilizes Newtonsoft's `TypeNameHandling` setting to persist the type of the object so that proper class is chosen for deserialization. Unfortunately this serializer contains a bug which prevents deserialization of F#'s discriminated unions.

The following sample demonstrates an issue:

```fsharp
open Newtonsoft.Json

type ChatEvent = { Message : string }
type ChatCommand = | Message of string | GetMessages
type ChatMessage = | Command of ChatCommand | Event of ChatEvent

[<EntryPoint>]
let main argv =
    let settings = new JsonSerializerSettings()
    settings.TypeNameHandling <- TypeNameHandling.All

    let serialized = JsonConvert.SerializeObject(Event {Message = "Hello world"}, settings)
    let deser = JsonConvert.DeserializeObject<_>(serialized, settings)

    printfn "%s\n\n%A" serialized deser
    0
```

## Solution

There's an event adapter that wraps the object (F# DU in this sample project) to a JObject with two fields: type and value. During deserialization the type is read from JObject (which Newtonsoft serializer handles very well) and passed to a respective `ToObject` overriden method.

The adapter is plugged to an Akka via `event-adapters` and `event-adapter-bindings` sections in config file. You have to modify class name and assembly name for your solution (or just get type information programmatically via `typeof EventAdapter`).

## More ideas

There're several more ideas other that correct ones such as fix an issue in Newtonsoft Serializer or configure other Akka serializer:

### Wrap event to an F# record type

```fsharp
type Message = {message: ChatMessage}
```

The message type is wrapped to record type. See fix/record-type branch in this repo.

### ...

## Kudos

Thank you @object for providing a hint and a sample code. That was the only and the great source of the information about Akka.NET persistance.