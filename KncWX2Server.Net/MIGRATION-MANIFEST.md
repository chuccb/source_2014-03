# KncWX2Server migration manifest

Updated: 2026-08-27

## Scope

This stage covers the shared serializer/security foundation, TCP transport/session layer, the server-side actor/event pipeline, and the shared local performer routing contract. `CA.exe.c` is explicitly excluded from this migration.

## Target stack

- C# 15 preview
- latest repository-targeted .NET 11 (`net11.0`)
- NativeAOT for executable projects
- `.slnx` solution format

The repository is configured for preview C#, `net11.0`, AOT compatibility, server GC and Tiered PGO, with NativeAOT enabled for executable projects.

## Current dependency graph

```text
Serializer / SerBuffer / ByteStream
        ↓
SecurityAssociation / SADB / SecureBuffer
        ↓
KEvent / KPerformerInfo
        ↓
Legacy TCP framing
        ↓
KncServerSession
        ↓
ServerActor event queue
        ↓
ServerActorManager
        ↓
ServerEventRouter
        ├─ local user actor routing
        ├─ local internal-performer routing
        ├─ lower-server trace routing (local target only)
        └─ remote-route result for proxy-required paths
        ↓
Role-specific event dispatch
        ├─ Login
        ├─ Center
        ├─ Channel
        └─ Game
        ↓
Persistence / external service managers
```

## Actor/event source cross-check

Verified native declarations and implementations:

- `KncWX2Server/Common/Performer.h/.cpp`
- `KncWX2Server/Common/Actor.h/.cpp`
- `KncWX2Server/Common/ActorManager.h/.cpp`
- `KncWX2Server/Common/SimLayer.h/.cpp`
- `KncWX2Server/Common/FSM/FSMclass.h/.cpp`
- `KncWX2Server/Common/FSM/FSMstate.h/.cpp`
- `KncWX2Server/Common/FSM/support_FSM.h`
- `KncWX2Server/Common/Event.h/.cpp`
- `KncWX2Server/Common/KncUidType.h/.cpp`
- `KncWX2Server/Common/KncSend.h/.cpp`
- `KncWX2Server/Common/ActorFactory.h`
- `KncWX2Server/GameServer/GSGameDBThread2nd.h/.cpp`
- `KncWX2Server/Common/ServerPacket.h`
- `KncWX2Server/Common/ClientPacket.h`

Important native semantics preserved:

- `KPerformer::QueueingEvent` is FIFO and synchronized; `Tick()` consumes the queue until empty.
- `KActorManager::Tick()` order is actor processing first, deferred delete second, deferred add third.
- `m_vecAct` is the ordered actor collection; `m_mapUID` exists for UID lookup. Managed code therefore keeps an ordered list plus a UID dictionary rather than iterating a dictionary as an ordering contract.
- temporary actor UID uses bit 62 as the marker with a 40-bit pure UID region.
- `KncSend` compares the destination/current server class first, then routes same-level events by performer class.
- Local `PC_USER` routing clones an event for every local target; missing local users are forwarded through the proxy only when trace exists.
- Internal DB/log performer classes are queued into a DB-layer performer rather than treated as a client socket.
- Lower-level routing uses the final sender UID when trace exists; empty-trace lower-level routing is rejected.
- Higher-level routing requires the proxy path.

## Managed actor/routing implementation

- `Common/ServerEventQueue.cs`: concurrent FIFO with queue-depth statistics.
- `Common/ServerActor.cs`: explicit actor event queue and optional FSM state.
- `Common/ServerActorManager.cs`: ordered active actor list + UID lookup, deferred add/delete, UID migration and actor multicast.
- `Common/PerformerIds.cs`: strongly typed masks/classes for the verified `KncSend` performer/server routing bits.
- `Common/ServerPerformer.cs`: FIFO internal performer queue without socket ownership.
- `Common/ServerPerformerManager.cs`: ordered internal performer registry and tick processing.
- `Common/ServerEventRouter.cs`: compile-time/AOT-friendly local routing of user and internal performer events, with explicit `RemoteRouteRequired` / unsupported results instead of inventing proxy behavior.

## Protocol / ABI

`KncSend.cpp` is routing metadata rather than a packet byte format. Its performer IDs are `DWORD`-width bitfields. The managed mapping therefore uses `uint` and preserves:

- PC mask `0xFF`
- server-class mask `0x0F00`
- send-type mask `0xF000`
- server classes `SC_CLIENT..SC_GLOBAL`
- documented performer classes including `PC_USER`, `PC_CHARACTER`, `PC_SERVER`, `PC_ROOM`, `PC_ACCOUNT_DB`, `PC_GAME_DB`, `PC_LOG_DB`, `PC_GAME_DB_2ND`, `PC_LOG_DB_2ND`

No packet field order, endian rule, encryption rule, or serializer layout was changed in this stage.

## Regression coverage

`KncWX2Server.Core.RegressionTests` now verifies:

- FIFO actor event order
- deferred actor insertion/deletion
- temporary UID bit-62 marker
- pre-add actor cancellation
- deterministic actor insertion-order processing
- local `PC_USER` routing
- partial local/remote-required user routing
- internal `PC_GAME_DB_2ND` routing through an internal performer queue
- existing serializer, UTF-16LE, compression, KEvent, TCP framing, SecureBuffer, ICV and replay checks

## Known partial / blocked areas

- Native proxy/remote forwarding is not yet implemented because `KProxyManager` / cross-server transport ownership has not been converted.
- `PC_CHARACTER` / room routing still requires their native manager contracts before implementation.
- `PC_SERVER` local base-server routing still requires the managed equivalent of the native `KBaseServer` performer registration.
- Role-specific opcode dispatch is not yet implemented.
- `KEvent::SetData<T>` generic payload serialization remains blocked until a strongly typed payload registry/contract is established from real callers.

## Build verification

Not executed successfully in the available execution environment. The container has no installed .NET SDK, so `dotnet build`, regression execution and NativeAOT publish cannot truthfully be reported as successful.

## Current status

The shared serializer/security foundation, exact legacy TCP framing, per-session security/lifetime, ordered actor/event pipeline, and the local performer routing contract are source-level cross-checked and committed on the rewrite branch. The implementation intentionally does not fabricate proxy, room, or role-specific opcode behavior.

## Next subsystem

**Role-specific event dispatch** is now the highest-leverage next stage. Start with a role whose event declaration, packet type, serializer, caller/callee and response path are all available in source; do not infer a handler solely from an event ID name.
