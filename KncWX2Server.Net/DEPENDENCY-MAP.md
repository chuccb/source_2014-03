# KncWX2Server migration dependency map

Updated: 2026-08-28
Scope: `KncWX2Server` + its actual SDK/source dependencies. `CA.exe.c` is explicitly out of scope for this migration.

## Current dependency graph

```text
Legacy serializer / binary contracts
  ├─ ByteOrder
  │    ├─ serializer numeric values: network BIG-endian
  │    └─ Win32 wchar_t bytes: UTF-16LE
  ├─ SerBuffer
  └─ KSerializer
       │
       ├─ KPerformerInfo
       ├─ KEvent
       └─ SecurityAssociation serialization

KncSecurity
  ├─ SecurityAssociation
  ├─ SecurityAssociationDatabase (SPI 0 + per-session SPI)
  └─ SecureBuffer
       │
       ▼
Legacy TCP framing
  ├─ [TotalLength:u16 LE, inclusive]
  └─ [SecureBuffer]
       │
       ▼
KncServerSession
       ├─ socket ownership
       ├─ security handshake
       ├─ authenticated/decrypted event decode
       └─ event queue ingress
             │
             ▼
ServerActor
  ├─ FIFO event queue
  ├─ queue statistics
  └─ optional FSM state
             │
             ▼
ServerActorManager
  ├─ ordered active actor list
  ├─ UID lookup dictionary
  ├─ deferred add
  ├─ deferred delete
  ├─ pooled tick snapshot (ArrayPool<ServerActor>)
  ├─ source-proven native UpdateUID semantics
  └─ GetFirstActorKey() → minimum registered UID
             │
             ├─ server identity boundary
             │    ├─ ServerInfo
             │    └─ ServerIdentity
             │         └─ KBaseServer::SetServerInfo field application
             │
             ├─ server-level performer routing
             │    └─ PC_SERVER → registered ServerPerformer queue
             │
             ├───────────────┐
             ▼               ▼
       ServerEventRouter   Role-specific dispatch
             │               ├─ Login
             │               │    └─ explicit typed-payload boundary [blocked until packet contract]
             │               ├─ Center
             │               ├─ Channel
             │               └─ Game
             │
             ├─ local PC_USER → actor manager
             ├─ local PC_SERVER → server-level performer manager
             ├─ local DB/Log performers → internal performer manager
             ├─ lower-server traced route → local actor when present
             └─ remote/proxy-required route → explicit blocked result
                     │
                     ▼
              service managers / persistence
```

## Server identity source chain audited

Native sources re-checked for the current identity boundary:

- `KncWX2Server/Common/ServerPacket.h`
- `KncWX2Server/Common/CommonPacket.h`
- `KncWX2Server/Common/BaseServer.h`
- `KncWX2Server/Common/BaseServer.cpp`
- `KncWX2Server/LoginServer/LoginServer.h`
- `KncWX2Server/LoginServer/LoginServer.cpp`

`KServerInfo` is a packed data object. Its common fields are:

- `int m_iUID`
- `std::wstring m_wstrName`
- `int m_iServerGroupID`
- `int m_iServerClass`
- `std::wstring m_wstrIP` in the non-private-network build
- `u_short m_usMasterPort`
- `u_short m_usNCUDPPort`
- `int m_nMaxUser`
- `int m_nCurrentUser`
- `bool m_bOn`

Private-IP and server-role fields are feature-gated in the native declaration and are not claimed as active managed runtime fields until the effective build profile is bound.

`KBaseServer::SetServerInfo()` applies the identity fields to the local server, and additionally configures the native `KNetLayer` master/UDP ports. The managed stage currently ports the pure identity/data portion only. `KNetLayer::SetPort()` and `InitNCUDP()` remain outside this boundary because their socket/UDP ownership has not yet been migrated.

## Managed server identity implementation

- `Common/ServerInfo.cs`: source-proven managed representation of the common `KServerInfo` fields; native `int` stays `int`, native `u_short` stays `ushort`, and server class is an `int`-backed enum matching native values.
- `Common/ServerIdentity.cs`: mutable local server identity state with explicit field application and no socket/network ownership.

## Server-level performer routing

Native `KLoginUser::RoutePacket()` first compares destination server level. For a same-level destination with performer class `PC_SERVER`, it clones the event, pushes the current user UID into the trace, and calls `KBaseServer::GetKObj()->QueueingEvent()`. This is a server-level performer queue, not a user actor lookup.

The managed router now maps `PC_SERVER` to `ServerPerformerManager.QueueingTo()`. A regression registers the authoritative `LoginServer` performer ID and verifies the event is queued and processed locally. The route layer remains separate from concrete Login opcode dispatch.

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

Native `_CASE` performs concrete payload deserialization with `KSerializer`, invokes `ON_<event>(trace, packet)`, then resets the event payload buffer. `KEvent::SetData<T>` uses the same typed serializer path. Therefore role dispatch cannot safely be reduced to an opcode callback table without the packet contract.

The managed Login stage has an explicit `LoginEventDispatcher` boundary. It classifies a Login-user destination as `TypedPayloadContractMissing` until a source-proven typed packet/serializer exists, and rejects non-Login destinations from the Login role boundary. It intentionally does not fabricate a concrete Login opcode handler.

## Ownership / lifetime / threading

- `ServerActorManager` owns active actor identity/order; its list corresponds to native `m_vecAct` while its dictionary provides UID lookup.
- `ServerActorManager.TickAsync()` uses a per-call pooled snapshot, so concurrent invocation cannot corrupt a shared reusable snapshot; only the caller's snapshot is processed.
- `ServerPerformer` owns internal event processing state and never owns a socket.
- `ServerPerformerManager` preserves explicit registration order for deterministic ticks.
- `ServerEventRouter` only routes/enqueues; processing remains on the corresponding actor/internal-performer loop.
- `KncServerSession` owns one socket/security/lifetime domain; cancellation closes session resources.
- `ServerIdentity` owns only local server metadata and does not own `KnetLayer`/socket/UDP resources.

## Performer ID / ABI

Native performer IDs are `DWORD` bitfields; managed routing uses `uint`.

- performer mask `0x000000FF`
- server class mask `0x00000F00`
- send type mask `0x0000F000`
- server classes `SC_CLIENT` through `SC_GLOBAL`
- verified performer classes, including `PC_GAME_DB_2ND` / `PC_LOG_DB_2ND`

Native actor `UidType` is `__int64`, represented by managed `long` in the actor/event layer.
Native `KServerInfo::m_iUID` is a separate 32-bit `int` identity field and is therefore represented by managed `int` in `ServerInfo` / `ServerIdentity`.

## Protocol / serializer status

- Outer TCP frame remains `[TotalLength:u16 LE, inclusive] + SecureBuffer`.
- Native KEvent field order remains destination performer info, trace[2], event id, then serialized payload buffer (plus feature-gated source metadata when active).
- `KEvent::SetData<T>` / `_CASE` remain blocked from a concrete managed Login payload implementation until packet declaration and serializer evidence are complete.
- `KSerializer` explicitly encodes numeric primitives in network byte order and Win32 `wchar_t` code units as 2-byte UTF-16LE on the target platform; WString has an existing regression.
- `ServerInfo` is currently a domain model and is not yet claimed as a completed wire packet serializer.

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
- Concrete Login opcode dispatch is blocked until typed event-payload contracts exist.
- `KEvent::SetData<T>` generic payload serialization remains blocked until a strongly typed payload contract is established from real callers.
- Native conditional build-profile selection is not bound to an effective managed runtime profile.
- Mail and several external service managers are not yet converted.

## Next unlock

**Continue `KBaseServer` lifecycle/registration integration:** connect the local server-level performer queue to the actual managed host lifecycle and server identity without claiming native network-layer side effects. Then resume `DBE_VERIFY_SERVER_CONNECT_ACK` once its exact packet declaration + serializer mapping and state transition are source-proven.
