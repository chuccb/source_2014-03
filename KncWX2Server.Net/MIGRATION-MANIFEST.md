# KncWX2Server migration manifest

Updated: 2026-08-27

## Scope

This stage covers the shared serializer/security foundation, TCP transport/session layer, and the server-side actor/event pipeline. `CA.exe.c` is explicitly excluded from this migration.

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
        ├─ deterministic actor insertion order
        ├─ actor Tick
        ├─ deferred delete
        └─ deferred add / temporary UID
        ↓
Role-specific event dispatch
        ↓
Login / Center / Channel / Game service state
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

Important native semantics preserved:

- `KPerformer::QueueingEvent` is FIFO and synchronized; `Tick()` consumes the queue until empty.
- `KActor` derives from `KSession` and supplies the multi-thread-safe FSM surface; the managed architecture keeps socket ownership in `KncServerSession` and makes actor state an explicit composition boundary.
- `KActorManager::Tick()` order is actor processing first, deferred delete second, deferred add third.
- `KActorManager::m_vecAct` is an ordered vector; managed actor processing therefore uses a list rather than dictionary enumeration for tick order.
- `KActorManager::ReserveAdd()` and `ReserveDelete()` are intentionally deferred mutations.
- temporary actor UID uses bit 62 as the marker with a 40-bit pure UID region.
- `FSMclass::StateTransition()` returns the current state when there is no transition, and changes to state 0 only when the current state itself cannot be resolved.
- `FSMstate` transition order and delete compaction are retained by the managed FSM conversion.

## Managed actor implementation

- `Common/ServerEventQueue.cs`: BCL concurrent FIFO with queue-depth statistics.
- `Common/ServerActor.cs`: explicit actor event queue and optional FSM state; event processing occurs only from manager ticks.
- `Common/ServerActorManager.cs`: ordered active actor list plus UID lookup, deferred add/delete, UID migration, multicast queueing, exact tick ordering, temporary UID generation and pre-add cancellation hardening.
- `Host/KncServerHost.cs`: creates one actor per accepted session, queues decoded session events into that actor, runs the manager tick loop, and reserves actor deletion when a session ends.

The manager was reworked after source-level review found that `ConcurrentDictionary.Values` would not preserve native `m_vecAct` insertion order. The current implementation therefore keeps a list for processing order and a dictionary solely for UID lookup.

## Regression coverage

`KncWX2Server.Core.RegressionTests` verifies:

- FIFO actor event order
- deferred actor insertion
- new actors are not ticked during the insertion tick
- temporary UID bit-62 marker
- deferred deletion
- pre-add deletion does not create a ghost actor
- deterministic native actor insertion order
- existing serializer, KEvent, TCP framing, SecureBuffer, ICV and replay regressions

## Build verification

Not executed successfully in the available execution environment. The container has no installed .NET SDK, so `dotnet build`, regression execution, and NativeAOT publish cannot truthfully be reported as successful.

## Current status

Serializer/security, exact legacy TCP framing, per-session security/lifetime, and the shared actor/event ownership pipeline are source-level cross-checked and committed on the rewrite branch. The business opcode layer is deliberately not fabricated.

## Remaining partial / blocked items

- Native `GSGameDBThread2nd` role-specific packet dispatch has not yet been ported because the effective `#ifdef` profile and complete packet declaration/serializer chain are not yet closed.
- Native `KEvent::SetData<T>` is only partially represented; no reflection/dynamic serializer registry has been introduced.
- Native `KSimObject::GetRefCount()` has no exact managed shared-pointer equivalent and remains partial.
- Mail/auth/billing subsystems remain inventory-only until their profile-specific dependencies are closed.

## Next subsystem

**Role-specific event dispatch** is now the highest-leverage next stage. Begin with the common/system event switch and the first role whose event declaration, packet layout, caller/callee, FSM guards and error/ACK path can all be proven from source. Do not fabricate handlers for unresolved build-profile branches.
