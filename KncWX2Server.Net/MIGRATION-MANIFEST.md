# KncWX2Server migration manifest

Updated: 2026-08-27

## Scope

This stage covers the shared serializer/security foundation, TCP transport/session layer, the server-side actor/event pipeline, the shared local performer routing contract, and the audit plus explicit boundary for the Login role dispatch frontier. `CA.exe.c` is explicitly excluded from this migration.

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
        │    └─ explicit typed-payload boundary [blocked until packet contract]
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

The managed Login project now contains an explicit `LoginEventDispatcher` boundary, but it deliberately does not claim any concrete Login opcode is ported. The dispatcher classifies a Login-user event as `TypedPayloadContractMissing` until a source-proven concrete packet declaration and serializer exist; non-Login destinations are rejected from the Login role boundary. This keeps the migration AOT-friendly and prevents generic/guessing fallbacks.

## Candidate event re-audit

- `ELG_USER_DISCONNECT_REQ`: not a safe no-payload starter. Its handler calls `UnRegUser`, writes `DBE_UPDATE_IS_LOGIN_NOT`, and branches ACK behavior based on request event semantics; optional account-count data is also profile dependent.
- `DBE_VERIFY_SERVER_CONNECT_ACK`: strongest next candidate. Its handler mutates server identity/state, checks duplicate UID, transitions to authenticated state, and depends on BaseServer performer registration plus the concrete packet contract.
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

`KncWX2Server.Core.RegressionTests` now additionally verifies the Login dispatch boundary:

- Login-user destinations are classified as `TypedPayloadContractMissing` rather than silently accepted or guessed.
- non-Login destinations are rejected from Login role dispatch.

Existing regression coverage remains:

- FIFO actor event order
- deferred actor insertion/deletion
- temporary UID bit-62 marker
- pre-add actor cancellation
- deterministic actor insertion-order processing
- local `PC_USER` routing
- partial local/remote-required user routing
- internal `PC_GAME_DB_2ND` routing through an internal performer queue
- serializer, UTF-16LE, compression, KEvent, TCP framing, SecureBuffer, ICV and replay checks

## Build verification

Not executed successfully in the available execution environment. The container has no installed .NET SDK, so `dotnet restore`, `dotnet build`, `dotnet test`, and NativeAOT publish cannot truthfully be reported as successful.

GitHub Actions also returned no workflow run/status for the latest branch HEAD, so no CI success is claimed.

## Status

- Shared serializer/security foundation: completed
- Exact legacy TCP framing: completed
- Per-session security/lifetime: completed
- Ordered actor/event pipeline: completed
- Verified local performer routing subset: completed
- Performer routing source-of-truth cleanup: completed
- Login dispatch boundary: ported-partial
- Login role-specific opcode handlers: blocked

## Known partial / blocked areas

- Native proxy/remote forwarding is not yet implemented because `KProxyManager` / cross-server transport ownership has not been converted.
- `PC_CHARACTER` / room routing still requires their native manager contracts.
- `PC_SERVER` local base-server routing still requires the managed equivalent of native `KBaseServer` performer registration.
- Login concrete opcode dispatch is blocked until typed event-payload contracts exist.
- `KEvent::SetData<T>` generic payload serialization remains blocked until its strongly typed payload contract is established from real callers.
- Native conditional build-profile selection is still not bound to an effective managed runtime profile.
- Mail and several external service managers are not yet converted.

## Next subsystem

**First source-proven typed Login event contract:** `DBE_VERIFY_SERVER_CONNECT_ACK`. Before implementing its handler, fully trace its concrete packet declaration, serializer mapping, `KBaseServer` registration/ownership, state transition, duplicate UID behavior, caller/callee chain, shutdown behavior and all effective build-profile branches. No opcode or payload layout should be inferred from names alone.
