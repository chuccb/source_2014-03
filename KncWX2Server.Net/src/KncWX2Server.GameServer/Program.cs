using KncWX2Server.Core;
using KncWX2Server.Host;

await KncServerBootstrap.RunAsync(args, ServerRole.Game, 9004);
