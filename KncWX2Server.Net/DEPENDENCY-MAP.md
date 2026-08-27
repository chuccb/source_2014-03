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
  ├─ active UID -> actor registry
  ├─ deferred add
  ├─ deferred delete
  ├─ UID migration
  └─ Tick order: actors -> delete -> add
             │
             ▼
Role-specific event dispatch
  ├─ Login
  ├─ Center
  ├─ Channel
  └─ Game
             │
             └─ service managers / persistence
```

## This stage: actor/event ownership

Native `KPerformer` owns the event FIFO and consumes it from `Tick()`. Native `KActor` derives from `KSession` and adds a multi-thread-safe FSM surface; `KActorManager::Tick()` first ticks every currently registered actor, then applies deferred deletions, then deferred additions. The managed implementation preserves those observable ordering and lifetime boundaries while keeping socket ownership in `KncServerSession` rather than reproducing the old inheritance tree.

## Evidence checked

### Declaration / implementation

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

### Caller / callee chain

```text
KncServerSession authenticated packet
  -> KEvent decode
  -> actor.QueueingEvent
  -> ServerActorManager.TickAsync
       -> actor.TickAsync
            -> ProcessEvent delegate
       -> deferred delete
       -> deferred add / temporary UID
```

Native equivalent:

```text
KSkSession::OnRecvCompleted
  -> KSession::OnRecvCompleted
  -> QueueingEvent
  -> KSimLayer::Tick
  -> KActorManager::Tick
       -> KActor::Tick / KPerformer::Tick
       -> Delete reserved actors
       -> Add reserved actors
```

## Ownership / lifetime / threading

- `KncServerSession` remains the sole owner of the accepted TCP stream and security state.
- `ServerActor` owns only actor/event-processing state; this avoids inventing a second socket owner.
- `ServerActorManager.Create()` deliberately defers registry insertion, matching `KActorManager::ReserveAdd()`.
- `ReserveDelete(ServerActor)` uses actor identity so a session that dies before its deferred-add tick cannot leave a ghost actor. This is an explicit safety hardening of the same two-stage lifecycle.
- Actor event processing occurs on one manager tick loop, preventing concurrent `TickAsync()` execution for the same manager.
- `ConcurrentQueue` / `ConcurrentDictionary` replace the old `CRITICAL_SECTION` + STL containers at the implementation level while preserving FIFO and registry semantics.
- Temporary actor UIDs retain the native temporary-ID bit (`bit 62`) and 40-bit pure UID region. Exact random algorithm is intentionally not reproduced; the protocol-visible UID layout is retained.

## FSM evidence

Native `FSMclass` stores `int -> FSMstate*`, uses state ID `0` as an invalid/problem state, and returns the current state when an input has no matching transition. `FSMstate` stores unsorted fixed-capacity input/output arrays and shifts entries after deletion. The existing managed `Fsm` / `FsmState` conversion follows those semantics. Role-specific FSM definitions have not been invented because no service-specific state table was established in this stage.

## Managed files added/changed this stage

- `Common/ServerEventQueue.cs`
- `Common/ServerActor.cs`
- `Common/ServerActorManager.cs`
- `Host/KncServerHost.cs`
- `Core.RegressionTests/Program.cs`

## Regression coverage added

- FIFO actor event consumption
- new actors are not processed in the same tick in which they are deferred-added
- native-style temporary UID marker
- deferred actor deletion
- actor deletion before deferred-add leaves no ghost actor
- existing serializer, KEvent, framing, security and replay regressions remain covered

## Known verification limitation

The execution environment still has no installed .NET SDK, so `dotnet build`, runtime tests and NativeAOT publish cannot truthfully be reported as executed. Source-level consistency and native cross-checking were performed instead.

## Next unlock

The next subsystem should be **role-specific actor/event dispatch tables**, starting with the smallest shared/common event routing surface and then Login/Center/Channel/Game-specific `ProcessEvent` implementations. Before converting a handler, its event ID declaration, packet struct, sender/caller, receiver, FSM state guards, response packet and persistence side effects must all be mapped from the native source.
