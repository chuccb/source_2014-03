# KncWX2Server migration manifest

Updated: 2026-08-28

## Scope

This stage covers the shared serializer/security foundation, TCP transport/session layer, the server-side actor/event pipeline, the shared local performer routing contract, and the audit plus explicit boundary for the Login role dispatch frontier. `CA.exe.c` is explicitly excluded from this migration.

## Target stack

- C# 15 preview
- latest repository-targeted .NET 11 (`net11.0`)
- NativeAOT for executable projects
- `.slnx` solution format
- SQLite 3.53-compatible persistence design

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
        ├─ ordered active actor list
        ├─ UID lookup
        ├─ deferred delete/add
        ├─ pooled tick snapshot
        ├─ source-proven native UpdateUID semantics
        └─ GetFirstActorKey() → minimum registered UID
        ↓
Server identity boundary
        ├─ ServerInfo
        └─ ServerIdentity
             └─ KBaseServer::SetServerInfo field application
        ↓
ServerEventRouter
        ├─ local PC_USER → actor manager
        ├─ local PC_SERVER → server-level performer queue
        ├─ local internal DB/Log performers
        ├─ lower-server traced route
        └─ explicit remote-route result
        ↓
Role-specific dispatch
        ├─ Login
        │    └─ typed-payload boundary [blocked]
        ├─ Center
        ├─ Channel
        └─ Game
        ↓
Persistence / external services
```

## Server identity boundary

The native `KServerInfo` declaration was re-audited in `Common/ServerPacket.h`, with the related `KNetAddress` declaration in `Common/CommonPacket.h`, and `KBaseServer::SetServerInfo()` in `Common/BaseServer.cpp`.

Common `KServerInfo` fields are source-proven as:

- `int m_iUID`
- `std::wstring m_wstrName`
- `int m_iServerGroupID`
- `int m_iServerClass`
- `std::wstring m_wstrIP` for the non-private-networking build
- `u_short m_usMasterPort`
- `u_short m_usNCUDPPort`
- `int m_nMaxUser`
- `int m_nCurrentUser`
- `bool m_bOn`

Managed `ServerInfo` preserves those widths, and `ServerIdentity` applies the pure identity/data fields. Network-layer side effects remain outside this boundary.

## Server-level performer routing

Native `KLoginUser::RoutePacket()` compares destination server level first. For the same server level, a `PC_SERVER` destination is cloned, the current LoginUser UID is pushed into the event trace, and the event is queued to the `KBaseServer` singleton itself.

The managed router now recognizes `PC_SERVER` as a first-class local performer class and queues it through the existing `ServerPerformerManager`. This intentionally does not claim that a concrete Login/KBaseServer opcode processor has been converted: the server-level queue and the role-specific event processor remain separate responsibilities.

The regression runner registers the authoritative `PerformerId.LoginServer`, routes a `PC_SERVER` event through the Login route, ticks the performer manager, and verifies the exact event reaches the local server-level performer.

## Actor/event source cross-check

Verified native declarations and implementations include `Performer`, `Actor`, `ActorManager`, `SimLayer`, FSM support, `Event`, `KncUidType`, `KncSend`, `ActorFactory`, `ServerPacket`, `ClientPacket`, `BaseServer`, and Login server/user source.

Important native semantics preserved:

- `KActor::Tick()` consumes queued events in FIFO order.
- `KActorManager::Tick()` processes the existing actor vector, then deferred delete, then deferred add.
- `m_vecAct` is insertion-ordered; `m_mapUID` is the lookup index.
- native temporary UID is a random 40-bit pure UID with bit 62 set for the default/non-extended profile.
- native `KActorManager::UpdateUID(newUid, actor)` removes the actor's old UID mapping, mutates the actor UID, then attempts insertion of the new mapping; insertion failure is returned without rolling back the mutation.
- native `KActorManager::GetFirstActorKey()` returns `m_mapUID.begin()->first`, or `0` when the map is empty. This means the returned key is the minimum registered UID, not the first actor in insertion order.

## Managed actor/routing implementation

- `Common/ServerEventQueue.cs`: concurrent FIFO with queue-depth statistics.
- `Common/ServerActor.cs`: explicit actor event queue and optional FSM state.
- `Common/ServerActorManager.cs`: ordered active actor list + UID lookup, deferred add/delete, pooled tick snapshot, source-proven `UpdateUid` mutation semantics, and native-equivalent `GetFirstActorKey()` minimum-UID lookup.
- `Common/Routing/PerformerRouting.cs`: authoritative performer/server bitfield constants.
- `Common/Routing/PerformerIds.cs`: authoritative combined performer IDs.
- `Common/ServerPerformer.cs`: FIFO internal performer queue without socket ownership.
- `Common/ServerPerformerManager.cs`: ordered internal performer registry and tick processing.
- `Common/ServerEventRouter.cs`: local routing with explicit remote/unsupported results, including local `PC_SERVER` performer queueing.

## Login role dispatch audit

The native Login role is split across two distinct performers:

```text
KLoginServer : KBaseServer
  ├─ Init()
  │    ├─ KLoginSimLayer
  │    ├─ KLoginNetLayer
  │    ├─ KActorManager
  │    └─ KActorFactory<KLoginUser, KDefaultFSM, ...>
  │
  └─ ProcessEvent()
       └─ server-level / DB / auth / ranking / service events

KLoginUser : KActor
  ├─ ProcessEvent()
  │    ├─ RoutePacket()
  │    └─ opcode switch
  │         └─ CASE / _CASE macros
  │              ├─ deserialize concrete K<event> packet
  │              └─ call ON_<event>(trace, packet)
  └─ Tick()/OnDestroy()
```

Native `_CASE` performs concrete payload deserialization with `KSerializer`, invokes `ON_<event>(trace, packet)`, then resets the event payload buffer. `KEvent::SetData<T>` uses the same typed serializer path. Therefore concrete Login opcode dispatch is still blocked without the packet contract.

## Ownership / lifetime / threading

- `ServerActorManager` owns active actor identity/order; its list corresponds to native `m_vecAct` while its dictionary provides UID lookup.
- `ServerActorManager.TickAsync()` uses a per-call pooled snapshot; actors created during a tick are deferred to the next tick.
- `ServerPerformer` owns its event-processing queue and never owns a socket.
- `ServerPerformerManager` owns performer registration order and queue dispatch; the new server-level `PC_SERVER` route uses this same queue abstraction.
- `KncServerSession` owns one socket/security/lifetime domain; cancellation closes session resources.
- `ServerIdentity` owns only local server metadata and does not own TCP/UDP resources.
- Native `KBaseServer::ShutDown()` still has broader layer/DB/socket teardown ordering than the managed identity/performer boundary; that lifecycle remains unported.

## Performer ID / ABI

Native performer IDs are `DWORD` bitfields; managed routing uses `uint`.

- performer mask `0x000000FF`
- server class mask `0x00000F00`
- send type mask `0x0000F000`
- `PerformerId.LoginServer` is the authoritative Login server performer ID used by the local regression.

Native actor `UidType` is `__int64`, represented by managed `long` in the actor/event layer.
Native `KServerInfo::m_iUID` is a separate 32-bit `int` identity field and is therefore represented by managed `int` in `ServerInfo` / `ServerIdentity`.

## Protocol / serializer status

- Outer TCP frame remains `[TotalLength:u16 LE, inclusive] + SecureBuffer`.
- Native KEvent field order remains destination performer info, trace[2], event id, then serialized payload buffer (plus feature-gated source metadata when active).
- `KEvent::SetData<T>` / `_CASE` remain blocked from a concrete managed Login payload implementation until packet declaration and serializer evidence are complete.
- `KSerializer` explicitly encodes numeric primitives in network byte order and Win32 `wchar_t` code units as 2-byte UTF-16LE on the target platform; WString has an existing regression.
- `ServerInfo` is a domain model and is not yet claimed as a completed wire packet serializer.

## Regression coverage

`KncWX2Server.Core.RegressionTests` verifies:

- serializer byte order
- Win32 UTF-16LE strings
- compression
- `KPerformerInfo` UID limit
- KEvent field order/round-trip
- TCP framing
- SecureBuffer authentication/tampering/replay
- actor FIFO ordering
- deferred insertion/deletion
- temporary UID marker
- actor snapshot semantics
- native `UpdateUID` duplicate failure semantics
- native `GetFirstActorKey` minimum-UID semantics
- performer routing
- local Login server-level (`PC_SERVER`) performer routing
- Login typed-payload boundary
- `KServerInfo` field application into managed server identity state

## Known partial / blocked areas

- Native `KProxyManager` / cross-server forwarding remains blocked until socket/session ownership is converted.
- `PC_CHARACTER` and `PC_ROOM` routing need the native manager contracts.
- `KBaseServer::SetServerInfo()` network-layer side effects (`SetPort`, `InitNCUDP`) remain blocked until the managed network layer owns those resources.
- Full managed host integration of the server-level performer remains partial because concrete role-specific server event processing is not yet converted.
- Concrete Login opcode dispatch is blocked until typed event-payload contracts exist.
- `KEvent::SetData<T>` generic payload serialization remains blocked until a strongly typed payload contract is established from real callers.
- Native conditional build-profile selection is not bound to an effective managed runtime profile.
- Mail and several external service managers are not yet converted.

## Next subsystem

**Continue the managed server-level performer lifecycle:** bind the server-level performer registration to the actual role host initialization/shutdown boundary, then proceed to the source-proven `DBE_VERIFY_SERVER_CONNECT_ACK` packet declaration + serializer mapping. Do not claim concrete Login handler completion until the full typed packet contract and FSM/error paths are available.
