using Cyberland.Engine;
using Cyberland.Engine.Diagnostics;

// Host executable: loads engine + mod assemblies from ./Mods (base game is one of them).
EngineUnhandledExceptionBootstrap.Install();
using var app = new GameApplication(args);
app.Run();
