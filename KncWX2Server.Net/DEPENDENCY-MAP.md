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
             │               ├─ Login [blocked: typed packet contract]
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

## This stage: Login role dispatch audit

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

The native source evidence establishes that role-specific dispatch is not merely `event-id -> callback`: the `_CASE` macro performs typed payload deserialization through `KSerializer`, then invokes a concrete handler and resets the event buffer. `KEvent::SetData<T>` uses the same generic serializer path for the payload.

The current managed host creates a generic `ServerActor` for each accepted session and currently installs a placeholder shared event processor. The Login project itself contains only `Program.cs` and the project file, so there is no existing managed `KLoginUser` or Login opcode dispatcher to re-use.

For this reason the full Login opcode switch is **blocked** rather than approximated. A correct implementation first needs a strongly typed event-payload contract and the concrete Login packet serializers, plus the corresponding Login-side service/performer ownership. Reflection, `dynamic`, a guessed opcode table, or a byte-buffer convention would change semantics and is explicitly disallowed by the migration rules.

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

- `KLoginServer::Init()` explicitly installs the Login network layer and a `KActorFactory<KLoginUser, KDefaultFSM, ...>`; the accepted server-side session therefore becomes a `KLoginUser` actor rather than a plain socket callback.
- `KLoginUser::ProcessEvent()` calls `RoutePacket()` before its role-local opcode switch.
- `RoutePacket()` distinguishes lower-level forwarding, higher-level/proxy routing, and same-level destination performer classes; it queues to BaseServer/DB/room/auth managers rather than blindly writing to a socket.
- `KLoginUser::OnDestroy()` unregisters its users from `KLoginSimLayer` and performs additional profile-dependent cleanup.
- `KLoginUser::ELG_USER_DISCONNECT_REQ` performs user unregister plus `DBE_UPDATE_IS_LOGIN_NOT`, and its ACK behavior depends on the request's embedded event id. This is therefore not a safe no-payload starter event.
- `DBE_VERIFY_SERVER_CONNECT_ACK` mutates actor identity/state (`SetName`, `UpdateUID`, authenticated transition) and performs duplicate UID protection, so it also depends on BaseServer/actor-registration semantics.

### Managed chain audited

```text
KncServerHost
  -> KncServerSession
  -> QueueSessionEventAsync
  -> ServerActor.QueueingEvent
  -> ServerActor.TickAsync
  -> ProcessActorEventAsync (currently shared placeholder)
```

`KncServerSession` performs packet authentication/decryption and `KEvent` deserialization before queueing. The host does not yet select a Login-specific processor, and no Login performer/service graph is present in the managed tree.

## This stage: performer routing source-of-truth cleanup

The audit found three managed performer-id definition files. Two were obsolete duplicates:

- `src/KncWX2Server.Core/Common/PerformerIds.cs`
- `src/KncWX2Server.Core/PerformerIds.cs`

The authoritative definitions are now kept in:

- `src/KncWX2Server.Core/Common/Routing/PerformerRouting.cs` — masks, server classes and performer-class constants.
- `src/KncWX2Server.Core/Common/Routing/PerformerIds.cs` — exact combined `PerformerId` values.

Existing routing implementations already consume the `Common.Routing` definitions. The obsolete duplicate files were removed so future Login/Center/Channel/Game routing work cannot silently drift between independent constant sets.

## Earlier shared routing evidence

Native `KncSend.cpp` first compares destination/current server class and then routes same-level events by performer class. Local users are found through `KActorManager`; DB/log performer classes are sent to `KDBLayer`; missing local user targets may be forwarded through `KProxyManager` when a trace exists; lower-level routing returns to the last sender UID; higher-level routing uses the proxy path.

The managed stage introduces an explicit routing contract without pretending that the not-yet-converted proxy, room, base-server or DB-layer implementations already exist.

## Ownership / lifetime / threading

- `ServerActorManager` owns active actor identity and ordering; its list is the managed equivalent of native `m_vecAct`, while the dictionary supplies UID lookup equivalent to `m_mapUID`.
- `ServerPerformer` owns only internal event processing state and never owns a socket.
- `ServerPerformerManager` keeps explicit registration order for deterministic ticks.
- `ServerEventRouter` only enqueues; actual event processing stays on the corresponding actor/internal-performer tick loop.
- `KncServerSession` owns one socket/security/lifetime domain; session shutdown cancels receive/heartbeat activity and closes the socket.
- No reflection, dynamic dispatch, generated runtime code or unsafe memory is required by the verified routing layer.

## Performer ID / ABI

Native performer IDs use a `DWORD` bitfield. The authoritative managed representation is `uint` and preserves:

- performer mask `0x000000FF`
- server class mask `0x00000F00`
- send type mask `0x0000F000`
- server classes `SC_CLIENT` through `SC_GLOBAL`
- verified performer classes, including `PC_GAME_DB_2ND` / `PC_LOG_DB_2ND`

`UidType` is native `__int64`, represented by managed `long` in the current event/actor layer. Do not substitute C# `ulong` without new native evidence.

## Protocol / serializer status

- Legacy outer TCP frame remains `[TotalLength:u16 LE, inclusive] + SecureBuffer`.
- Native KEvent field order remains destination performer info, trace[2], event id, then serialized payload buffer (plus profile-dependent source metadata only when that build feature is active).
- The typed `KEvent::SetData<T>` payload contract remains **blocked** until every Login starter event has a concrete packet declaration and serializer mapping.

## Regression coverage

`KncWX2Server.Core.RegressionTests` currently verifies:

- deterministic actor insertion-order processing
- deferred actor lifecycle
- local `PC_USER` routing
- mixed local/missing user targets
- internal `PC_GAME_DB_2ND` routing into an internal performer FIFO
- existing serializer, UTF-16LE, compression, KEvent, frame, security and replay regressions

No new test claims were added for Login role dispatch because no Login opcode handler was fabricated.

## Known partial / blocked areas

- Native `KProxyManager` / cross-server forwarding remains blocked until its socket/session ownership is converted.
- `PC_CHARACTER` and `PC_ROOM` routing need their native manager contracts.
- `PC_SERVER` local routing needs `KBaseServer` performer registration.
- Full Login role-specific opcode dispatch is **blocked** until the typed event-payload/serializer contract exists.
- `KEvent::SetData<T>` remains blocked until its strongly typed payload contract is established from real callers.
- Native conditional build-profile selection is still not bound to an effective managed runtime profile.

## Next unlock

The highest-leverage next stage is to establish the first **source-proven typed Login event contract** rather than translating the entire Login switch. `DBE_VERIFY_SERVER_CONNECT_ACK` is a candidate only after its packet declaration/serialization and `KBaseServer` performer-registration dependencies are completely traced. `ELG_USER_DISCONNECT_REQ` should not be chosen first because its packet contains routing-dependent state used by the handler and its ACK behavior depends on embedded event semantics.