# KncWX2Server migration dependency map

Updated: 2026-08-27
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
  └─ Tick order: actors -> delete -> add
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
             ├─ local DB/Log performers → internal performer manager
             ├─ lower-server traced route → local actor when present
             └─ remote/proxy-required route → explicit blocked result
                     │
                     ▼
              service managers / persistence
```

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

The managed Login stage now has an explicit `LoginEventDispatcher` boundary. It classifies a Login-user destination as `TypedPayloadContractMissing` until a source-proven typed packet/serializer exists, and rejects non-Login destinations from the Login role boundary. It intentionally does not fabricate a concrete Login opcode handler.

## Login source chain audited

### Declaration / construction / lifecycle

- `KncWX2Server/LoginServer/LoginServer.h`
- `KncWX2Server/LoginServer/LoginServer.cpp`
- `KncWX2Server/LoginServer/LoginNetLayer.h/.cpp`
- `KncWX2Server/LoginServer/LoginUser.h/.cpp`
- `KncWX2Server/LoginServer/LoginServer_2010.vcxproj`
- `KncWX2Server/Common/Event.h`
- `KncWX2Server/Common/KncSend.h`
- `KncWX2Server/Common/KncUidType.h`
- `KncWX2Server/Common/CommonPacket.h`
- `KncWX2Server/Common/ServerPacket.h`
- `KncWX2Server/Common/ClientPacket.h`
- `KncWX2Server/Common/EventID_Server.h`

### Native Login ownership / routing observations

- `KLoginServer::Init()` installs the Login network layer and a `KActorFactory<KLoginUser, KDefaultFSM, ...>`.
- `KLoginUser::ProcessEvent()` calls `RoutePacket()` before the role-local opcode switch.
- `RoutePacket()` distinguishes lower-level forwarding, higher-level/proxy routing, and same-level performer classes; it queues to BaseServer/DB/room/auth managers rather than blindly writing to a socket.
- `KLoginUser::OnDestroy()` unregisters users from `KLoginSimLayer` and performs additional profile-dependent cleanup.
- `KLoginUser::ELG_USER_DISCONNECT_REQ` performs user unregister plus `DBE_UPDATE_IS_LOGIN_NOT`; ACK behavior depends on embedded event semantics and optional account-count data.
- `DBE_VERIFY_SERVER_CONNECT_ACK` mutates actor identity/state, checks duplicate UID, transitions to authenticated state, and depends on BaseServer performer registration plus the concrete packet contract.

### Managed chain audited

```text
KncServerHost
  -> KncServerSession
  -> QueueSessionEventAsync
  -> ServerActor.QueueingEvent
  -> ServerActor.TickAsync
  -> ProcessActorEventAsync (currently shared placeholder)
```

`KncServerSession` authenticates/decrypts and deserializes `KEvent` before queueing. The Host layer still does not know how to provide a concrete Login typed packet handler; the new Login dispatcher boundary therefore stays explicit and partial rather than silently absorbing the event.

## Performer source-of-truth correction

The repository previously contained three managed performer-id definition files. The two obsolete duplicates were removed:

- `src/KncWX2Server.Core/Common/PerformerIds.cs`
- `src/KncWX2Server.Core/PerformerIds.cs`

The authoritative definitions are:

- `src/KncWX2Server.Core/Common/Routing/PerformerRouting.cs` — masks, server classes and performer-class constants.
- `src/KncWX2Server.Core/Common/Routing/PerformerIds.cs` — exact combined `PerformerId` values.

## Ownership / lifetime / threading

- `ServerActorManager` owns active actor identity/order; its list corresponds to native `m_vecAct` while its dictionary provides UID lookup.
- `ServerPerformer` owns internal event processing state and never owns a socket.
- `ServerPerformerManager` preserves explicit registration order for deterministic ticks.
- `ServerEventRouter` only routes/enqueues; processing remains on the corresponding actor/internal-performer loop.
- `KncServerSession` owns one socket/security/lifetime domain; cancellation closes session resources.
- The Login boundary itself is stateless and alloc-free for the classification path.

## Performer ID / ABI

Native performer IDs are `DWORD` bitfields; managed routing uses `uint`.

- performer mask `0x000000FF`
- server class mask `0x00000F00`
- send type mask `0x0000F000`
- server classes `SC_CLIENT` through `SC_GLOBAL`
- verified performer classes, including `PC_GAME_DB_2ND` / `PC_LOG_DB_2ND`

Native `UidType` is `__int64`, represented by managed `long` in the current event/actor layer.

## Protocol / serializer status

- Outer TCP frame remains `[TotalLength:u16 LE, inclusive] + SecureBuffer`.
- Native KEvent field order remains destination performer info, trace[2], event id, then serialized payload buffer (plus feature-gated source metadata when active).
- `KEvent::SetData<T>` / `_CASE` remain blocked from a concrete managed Login payload implementation until packet declaration and serializer evidence are complete.

## Regression coverage

`KncWX2Server.Core.RegressionTests` now additionally verifies:

- Login-user destination enters the explicit `TypedPayloadContractMissing` boundary.
- Non-Login destinations do not enter Login role dispatch.

Existing routing/serializer/security/actor regressions remain unchanged.

## Known partial / blocked areas

- Native `KProxyManager` / cross-server forwarding remains blocked until socket/session ownership is converted.
- `PC_CHARACTER` and `PC_ROOM` routing need their native manager contracts.
- `PC_SERVER` local routing needs the managed `KBaseServer` performer-registration equivalent.
- Concrete Login opcode dispatch is blocked until typed event-payload contracts exist.
- `KEvent::SetData<T>` generic payload serialization remains blocked until a strongly typed payload contract is established from real callers.
- Native conditional build-profile selection is not bound to an effective managed runtime profile.
- Mail and several external service managers are not yet converted.

## Next unlock

The next implementation target remains the first source-proven typed Login event contract, with `DBE_VERIFY_SERVER_CONNECT_ACK` currently the strongest candidate. Before writing its handler, fully trace its concrete packet declaration, serializer mapping, `KBaseServer` performer-registration/ownership, state transition, duplicate UID behavior, caller/callee chain, shutdown behavior, and effective build-profile branches.