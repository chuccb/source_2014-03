# KncWX2Server migration manifest

Updated: 2026-08-27

## Scope

This stage covers the shared serializer/security foundation, TCP transport/session layer, the server-side actor/event pipeline, the shared local performer routing contract, and the audit of the Login role dispatch frontier. `CA.exe.c` is explicitly excluded from this migration.

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
        ├─ Login [blocked: typed payload/serializer contract]
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
- `Common/Routing/PerformerRouting.cs`: authoritative performer/server bitfield constants.
- `Common/Routing/PerformerIds.cs`: authoritative strongly typed combined performer IDs.
- `Common/ServerPerformer.cs`: FIFO internal performer queue without socket ownership.
- `Common/ServerPerformerManager.cs`: ordered internal performer registry and tick processing.
- `Common/ServerEventRouter.cs`: compile-time/AOT-friendly local routing of user and internal performer events, with explicit `RemoteRouteRequired` / unsupported results instead of inventing proxy behavior.

## Login role dispatch audit

Native Login is divided into a `KLoginServer : KBaseServer` and per-connection `KLoginUser : KActor`.

- `KLoginServer::Init()` creates `KLoginSimLayer`, `KLoginNetLayer`, `KActorManager`, and a `KActorFactory<KLoginUser, KDefaultFSM, ...>`.
- `KLoginUser::ProcessEvent()` performs `RoutePacket()` before the role-local event switch.
- Native `_CASE` deserializes a concrete `K<event>` payload with `KSerializer`, calls `ON_<event>(trace, packet)`, and resets the event buffer.
- `KLoginUser::RoutePacket()` is performer-aware and may target BaseServer, DB, room, authentication or proxy paths instead of being a direct socket send.
- `KLoginUser::OnDestroy()` unregisters users from `KLoginSimLayer` and performs additional conditional cleanup.

The managed Login project still contains only `Program.cs` plus its project file. `KncServerHost` currently creates a generic `ServerActor` and installs a shared placeholder processor. There is no trustworthy managed Login packet registry/serializer layer yet.

Therefore the full Login opcode switch is **blocked**, not approximated. The next implementation must establish concrete packet declarations, serializer mappings, FSM requirements, caller/callee behavior and service ownership for one source-proven Login event before wiring a real handler.

Candidate events were re-audited:

- `ELG_USER_DISCONNECT_REQ`: not a safe no-payload starter. Its handler calls `UnRegUser`, writes `DBE_UPDATE_IS_LOGIN_NOT`, and branches ACK behavior based on request event semantics; optional account-count data is also profile dependent.
- `DBE_VERIFY_SERVER_CONNECT_ACK`: source is explicit, but its handler mutates server identity/state, checks duplicate UID, transitions to authenticated state, and depends on BaseServer performer registration plus the concrete packet contract.
- Large authentication/register events were rejected as first targets because their behavior depends on multiple legacy build profiles and external service managers.

## Performer source-of-truth correction

The audit found three managed performer-id definition files. Two obsolete duplicates were removed:

- `src/KncWX2Server.Core/Common/PerformerIds.cs`
- `src/KncWX2Server.Core/PerformerIds.cs`

The authoritative definitions remain under `src/KncWX2Server.Core/Common/Routing/`:

- `PerformerRouting.cs` for masks/classes.
- `PerformerIds.cs` for exact combined IDs.

This prevents Login/Center/Channel/Game routing changes from silently maintaining multiple incompatible constant sets.

## Protocol / ABI

`KncSend.cpp` is routing metadata rather than a packet byte format. Its performer IDs are `DWORD`-width bitfields; managed routing therefore uses `uint` and preserves the native masks and verified numeric values.

`UidType` is native `__int64`, represented by managed `long` in the current event/actor layer.

Legacy outer TCP framing remains `[TotalLength:u16 LE, inclusive] + SecureBuffer`. Native serializer/network primitive endian rules and SecureBuffer security semantics were not changed by this stage.

`KEvent::SetData<T>` and the `_CASE` typed-payload path remain **blocked** until a strongly typed managed payload contract is established from real callers.

## Regression coverage

`KncWX2Server.Core.RegressionTests` currently verifies:

- FIFO actor event order
- deferred actor insertion/deletion
- temporary UID bit-62 marker
- pre-add actor cancellation
- deterministic actor insertion-order processing
- local `PC_USER` routing
- partial local/remote-required user routing
- internal `PC_GAME_DB_2ND` routing through an internal performer queue
- existing serializer, UTF-16LE, compression, KEvent, TCP framing, SecureBuffer, ICV and replay checks

No Login opcode test was added because no Login handler was fabricated.

## Build verification

Not executed successfully in the available execution environment. The container has no installed .NET SDK, so `dotnet restore`, `dotnet build`, `dotnet test`, and NativeAOT publish cannot truthfully be reported as successful.

## Status

- Shared serializer/security foundation: completed
- Exact legacy TCP framing: completed
- Per-session security/lifetime: completed
- Ordered actor/event pipeline: completed
- Verified local performer routing subset: completed
- Performer routing source-of-truth cleanup: completed
- Login role-specific event dispatch: blocked

## Known partial / blocked areas

- Native proxy/remote forwarding is not yet implemented because `KProxyManager` / cross-server transport ownership has not been converted.
- `PC_CHARACTER` / room routing still requires their native manager contracts before implementation.
- `PC_SERVER` local base-server routing still requires the managed equivalent of native `KBaseServer` performer registration.
- Login role-specific opcode dispatch is blocked until the typed event-payload/serializer contract exists.
- `KEvent::SetData<T>` generic payload serialization remains blocked until its strongly typed payload contract is established from real callers.
- Native conditional build-profile selection is still not bound to an effective managed runtime profile.
- Mail and several external service managers are not yet converted.

## Next subsystem

**First source-proven typed Login event contract.** `DBE_VERIFY_SERVER_CONNECT_ACK` is currently the strongest candidate, but only after the concrete packet declaration/serialization and `KBaseServer` registration path are fully traced. The next round must not infer an opcode or payload layout merely from names.