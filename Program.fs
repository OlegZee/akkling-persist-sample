open Newtonsoft.Json

type ChatEvent = { Message : string }
type ChatCommand = | Message of string | GetMessages

[<EntryPoint>]
let main argv =

    let settings = new JsonSerializerSettings()
    settings.TypeNameHandling <- TypeNameHandling.All

    let serialized = JsonConvert.SerializeObject({Message = "hhh"}, settings)
    let deser = JsonConvert.DeserializeObject<_>(serialized, settings)

    printfn "%s\n%A\n" serialized deser

    let serialized = JsonConvert.SerializeObject(Message "Hello world", settings)
    let deser = JsonConvert.DeserializeObject<_>(serialized, settings)

    printfn "%s\n\n%A" serialized deser
    0