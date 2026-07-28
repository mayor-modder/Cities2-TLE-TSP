# Vendored Lane System source

The `LaneSystem` project is tracked directly in this repository so clean checkouts can build Traffic Lights Enhancement without resolving a separate Git submodule.

The source files were copied from [`C2VM/CommonLibraries`](https://github.com/C2VM/CommonLibraries) commit `edcbd8ee048a9fd2a7ff6cdde738e1e2a16d2319`. The project file has formatting-only whitespace normalization, and `LaneSystem/Mod.cs` is intentionally omitted. Traffic Lights Enhancement owns the single `Game.Modding.IMod` entry point and performs Lane System's required startup action from its existing `Mod.OnLoad`.

Keep the `C2VM.CommonLibraries.LaneSystem` assembly name, namespaces, serialized component types, and schemas unchanged for save compatibility.
