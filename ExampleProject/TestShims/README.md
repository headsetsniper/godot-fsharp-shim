This project hosts generated C# test shims for F# tests.

- It references the FSharp.Tests project so the generator can discover [TestSuite] types.
- It sets FSharpShimsMode=Tests so ShimGen emits GdUnit4-compatible wrappers.
- It points RunSettingsFilePath to the shared .runsettings in FSharp.Tests, so Godot binary and parameters are reused.

Build this project to regenerate shims:

- dotnet build ExampleProject/TestShims/FsharpWithShim.TestShims.csproj -c Debug
