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
        ├─ local user actor routing
        ├─ local internal-performer routing
        ├─ lower-server trace routing
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

Native `KBaseServer::SetServerInfo()` applies UID, name, server group, server class, networking addresses, max-user configuration and network-layer ports. The managed stage currently ports the pure identity/data portion only. `KNetLayer::SetPort()` and `InitNCUDP()` remain outside this boundary because their socket/UDP ownership has not yet been migrated.

Managed implementation:

- `Common/ServerInfo.cs`: exact common data contract; native `int` stays `int`, native `u_short` stays `ushort`, and `ServerClassId` is an `int`-backed enum matching native `EServerClass` values.
- `Common/ServerIdentity.cs`: mutable local server identity state with explicit field application and no socket/network ownership.

This is intentionally a domain model, not a claim that `KServerInfo` wire serialization is already complete.

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
- `Common/ServerEventRouter.cs`: local routing with explicit remote/unsupported results.

## Login role dispatch audit

Native Login is divided into `KLoginServer : KBaseServer` and per-connection `KLoginUser : KActor`.

- `KLoginServer::Init()` creates `KLoginSimLayer`, `KLoginNetLayer`, `KActorManager`, and `KActorFactory<KLoginUser, KDefaultFSM, ...>`.
- `KLoginUser::ProcessEvent()` performs `RoutePacket()` before the role-local event switch.
- `_CASE` deserializes a concrete `K<event>` payload with `KSerializer`, calls `ON_<event>(trace, packet)`, then resets the event buffer.
- `DBE_VERIFY_SERVER_CONNECT_ACK` mutates identity/state and depends on BaseServer registration, duplicate UID behavior, and build-profile branches.

Managed Login still exposes only the explicit `LoginEventDispatcher` boundary until a complete packet declaration/serializer contract is source-proven.

## Protocol / ABI

- Performer IDs are `DWORD` bitfields and managed routing uses `uint`.
- Actor `UidType` is native `__int64` and managed `long`.
- `KServerInfo::m_iUID` is a separate native 32-bit `int` identity field and managed `ServerInfo.Uid` is therefore `int`.
- Outer TCP framing remains `[TotalLength:u16 LE, inclusive] + SecureBuffer`.
- Numeric serializer/network primitive endian rules and SecureBuffer security semantics remain unchanged.
- Concrete Login typed payloads remain blocked until their exact declaration and serialization path are established.

## Regression coverage

Existing regressions remain enabled for:

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
- Login typed-payload boundary
- `KServerInfo` field application into managed server identity state

## Build verification

GitHub Actions is the authoritative CI environment for this repository. Local `.NET` SDK execution is unavailable in the current container, so no local NativeAOT success is claimed.

## Status

- Shared serializer/security foundation: completed
- Exact legacy TCP framing: completed
- Per-session security/lifetime: completed
- Ordered actor/event pipeline: completed
- Verified local performer routing subset: completed
- Performer routing source-of-truth cleanup: completed
- Actor `UpdateUID` native behavior correction: completed
- Actor `GetFirstActorKey` native behavior correction: ported
- `KServerInfo` / `ServerIdentity` data boundary: ported-partial
- Login dispatch boundary: ported-partial
- Login role-specific opcode handlers: blocked

## Known partial / blocked areas

- Native `KProxyManager` / cross-server forwarding remains blocked until socket/session ownership is converted.
- `PC_CHARACTER` / room routing still requires the native manager contracts.
- `PC_SERVER` local BaseServer routing still requires the managed equivalent of native `KBaseServer` performer registration.
- `KBaseServer::SetServerInfo()` network side effects (`KNetLayer::SetPort`, `InitNCUDP`) remain blocked until the managed network layer owns those resources.
- Concrete Login opcode dispatch is blocked until typed event-payload contracts exist.
- `KEvent::SetData<T>` generic payload serialization remains blocked until a strongly typed payload contract is established from real callers.
- Native conditional build-profile selection is not bound to an effective managed runtime profile.
- Mail and several external service managers are not yet converted.

## Next subsystem

**Continue the source-proven `KBaseServer` registration boundary:** connect the newly ported `ServerIdentity` to the managed server-level performer/host lifecycle without claiming the unconverted network-layer side effects. Then resume `DBE_VERIFY_SERVER_CONNECT_ACK` only after its exact concrete packet declaration + serializer mapping are fully established.
