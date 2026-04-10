using Cyberland.Engine;

// Host executable: loads engine + mod assemblies from ./Mods (base game is one of them).
using var app = new GameApplication(args);
app.Run();
