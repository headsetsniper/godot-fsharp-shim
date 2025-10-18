namespace MyGodotFSharp.Tests

open GdUnit4
open MyGodotFSharp

[<TestSuite>]
type GameLogicTests() =
    [<TestCase(Description = "Converts click counts into the label text shown to the player.")>]
    member _.``labelText describes click counts``() =
        let counts = [ 0; 1; 5 ]

        let messages =
            counts
            |> List.map ClickCounterLogic.labelText
            |> List.toArray

        Assertions.AssertThat(messages[0]).IsEqual("Click anywhere on this control.") |> ignore
        Assertions.AssertThat(messages[1]).IsEqual("Clicks registered: 1") |> ignore
        Assertions.AssertThat(messages[2]).IsEqual("Clicks registered: 5") |> ignore
        [<TestCase(Description = "Converts click counts into the label text shown to the player.")>]
        member _.``labelText describes click counts``() =
            let counts = [ 0; 1; 5 ]

            let messages =
                counts
                |> List.map ClickCounterLogic.labelText
                |> List.toArray

            Assertions.AssertThat(messages[0]).IsEqual("Click anywhere on this control.") |> ignore
            Assertions.AssertThat(messages[1]).IsEqual("Clicks registered: 1") |> ignore
            Assertions.AssertThat(messages[2]).IsEqual("Clicks registered: 5") |> ignore
