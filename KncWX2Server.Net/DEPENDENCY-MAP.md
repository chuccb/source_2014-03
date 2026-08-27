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

## This stage: local performer routing

Native `KncSend.cpp` first compares destination/current server class and then routes same-level events by performer class. Local users are found through `KActorManager`; DB/log performer classes are sent to `KDBLayer`; missing local user targets may be forwarded through `KProxyManager` when a trace exists; lower-level routing returns to the last sender UID; higher-level routing uses the proxy path.

The managed stage introduces an explicit routing contract without pretending that the not-yet-converted proxy, room, base-server or DB-layer implementations already exist.

## Evidence checked

### Declaration / implementation

- `KncWX2Server/Common/KncSend.h/.cpp`
- `KncWX2Server/Common/Performer.h/.cpp`
- `KncWX2Server/Common/Actor.h/.cpp`
- `KncWX2Server/Common/ActorManager.h/.cpp`
- `KncWX2Server/Common/SimLayer.h/.cpp`
- `KncWX2Server/Common/FSM/FSMclass.h/.cpp`
- `KncWX2Server/Common/FSM/FSMstate.h/.cpp`
- `KncWX2Server/Common/FSM/support_FSM.h`
- `KncWX2Server/Common/Event.h/.cpp`
- `KncWX2Server/Common/NetLayer.h/.cpp`
- `KncWX2Server/Common/Socket/Session.h/.cpp`
- `KncWX2Server/Common/KncUidType.h/.cpp`
- `KncWX2Server/Common/ActorFactory.h`
- `KncWX2Server/GameServer/GSGameDBThread2nd.h/.cpp`
- `KncWX2Server/Common/ServerPacket.h`
- `KncWX2Server/Common/ClientPacket.h`

### Routing chain

```text
Native KncSend
  -> compare server classes
  -> same-level performer switch
       -> PC_USER -> KActorManager
       -> DB/log classes -> KDBLayer
       -> PC_SERVER -> BaseServer/proxy
       -> PC_CHARACTER -> actor CID route
       -> PC_ROOM -> RoomManager
       -> default -> error
  -> lower server -> trace UID -> local actor / broadcast
  -> higher server -> proxy
```

Managed equivalent currently implemented for the provable local subset:

```text
ServerEventRouter
  -> PC_USER -> ServerActorManager
  -> PC_ACCOUNT_DB / PC_GAME_DB / PC_GAME_DB_2ND / PC_LOG_DB / PC_LOG_DB_2ND
       -> ServerPerformerManager
  -> lower server + trace -> local actor
  -> remote path -> RemoteRouteRequired
```

## Ownership / lifetime / threading

- `ServerActorManager` owns active actor identity and ordering; its list is the managed equivalent of native `m_vecAct`, while the dictionary supplies UID lookup equivalent to `m_mapUID`.
- `ServerPerformer` owns only internal event processing state and never owns a socket.
- `ServerPerformerManager` keeps explicit registration order for deterministic ticks.
- `ServerEventRouter` only enqueues; actual event processing stays on the corresponding actor/internal-performer tick loop.
- No reflection, dynamic dispatch, generated runtime code or unsafe memory is required for this routing layer.

## Performer ID / ABI

Native performer IDs use a `DWORD` bitfield. The managed representation is `uint` and preserves:

- performer mask `0x000000FF`
- server class mask `0x00000F00`
- send type mask `0x0000F000`
- server classes `SC_CLIENT` through `SC_GLOBAL`
- core performer classes used by `KncSend`
- `PC_GAME_DB_2ND` / `PC_LOG_DB_2ND` values introduced by the 2013 entry-point refactor

No packet binary layout, endian rule, encryption or serializer field order changed in this stage.

## Regression coverage

`KncWX2Server.Core.RegressionTests` now verifies:

- deterministic actor insertion-order processing
- deferred actor lifecycle
- local `PC_USER` routing
- mixed local/missing user targets return `RemoteRouteRequired` while preserving local delivery
- internal `PC_GAME_DB_2ND` routing into an internal performer FIFO
- existing serializer, UTF-16LE, compression, KEvent, frame, security and replay regressions

## Known partial / blocked areas

- Native `KProxyManager` / cross-server forwarding remains blocked until its socket/session ownership is converted.
- `PC_CHARACTER` and `PC_ROOM` routing need their native manager contracts.
- `PC_SERVER` local routing needs `KBaseServer` performer registration.
- Full role-specific opcode dispatch is not yet implemented.
- `KEvent::SetData<T>` remains blocked until its strongly typed payload contract is established from real callers.

## Next unlock

The highest-leverage next stage is **role-specific event dispatch**, starting with a handler whose packet declaration, serializer, caller, response, FSM constraints, and persistence/external-service effects can all be proven from source. Do not infer a handler from the event ID alone.
