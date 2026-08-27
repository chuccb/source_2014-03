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
  │    ├─ AuthKey: 8 bytes
  │    ├─ CryptoKey: 8 bytes (DES)
  │    ├─ SequenceNumber: u32
  │    └─ ReplayWindowMask: u32
  │
  ├─ SecurityAssociationDatabase
  │    └─ SPI -> SA, with SPI 0 default SA
  │
  └─ SecureBuffer
       ├─ SPI: u16 LE
       ├─ Sequence: u32 LE
       ├─ IV: 8 bytes
       ├─ DES-CBC ciphertext
       └─ ICV: HMAC-MD5, first 10 bytes
             │
             ▼
Legacy TCP framing
  ├─ [TotalLength:u16 LE]  // includes the 2-byte length field
  └─ [SecureBuffer bytes]
             │
             ▼
KncServerHost
  ├─ TcpListener / BCL async socket implementation
  │    └─ Windows uses the platform async socket machinery; no Win32 IOCP API is required in managed code
  └─ session ownership
       │
       ▼
KncServerSession
  ├─ owns TcpClient + NetworkStream lifetime
  ├─ owns per-session SecurityAssociationDatabase
  ├─ serializes sends with one async gate
  ├─ receive loop -> frame parser -> SecureBuffer authentication/decryption
  ├─ KSerializer -> KEvent decode
  ├─ 15 s heartbeat monitor / 60 s receive timeout
  └─ packet-auth-failure counter / disconnect threshold
       │
       ▼
Role-specific event dispatch
  ├─ Login
  ├─ Center
  ├─ Channel
  └─ Game
       │
       └─ actor/session managers -> service handlers -> Persistence
```

## Transport evidence checked

### Declaration / implementation

- `KNetLayer` owns `KAccepter`, `KIocp`, UDP helpers and `IActorFactory`.
- `KSession` owns `KSkSession`, proxy/unproxy state, SPI, sequence/auth counters and lifecycle.
- `KSocketObject` owns the Windows socket, receive/send overlapped buffers and send queue.
- `KIocp` maps completion keys to `KSocketObject` instances.
- `KIOThread` drains completion packets and dispatches to socket objects.
- `KSkSession` bridges socket completion to `KSession`.
- Role NetLayer subclasses (`KLoginNetLayer`, `KChannelNetLayer`, `KGSNetLayer`, `KGlobalNetLayer`) do not override the core transport lifecycle; they only add Lua registration.

### Caller / callee chain

```text
KNetLayer::Init
  -> KAccepter::Init / Begin
  -> KncSecurity::InitSecurity
  -> KIocp::Init / BeginThread

KAccepter::Loop
  -> KNetLayer::OnAccept
  -> KNetLayer::OnAccept_
  -> IActorFactory::CreateActor
  -> KSocketObject::SetSocketInfo
  -> KIocp::AssociateSocket
  -> KSocketObject::InitRecv
  -> KSkSession::OnAcceptConnection
  -> KSession::OnAcceptConnection

IOCP thread
  -> KIocp::OnIOCompleted
  -> KSocketObject::OnRecvCompleted / OnSendCompleted
  -> KSkSession::OnRecvCompleted
  -> KSession::OnRecvCompleted
  -> KSerializer::Get(KEvent)
  -> QueueingEvent
```

The managed transport preserves the same observable sequence even though the implementation uses `TcpListener`/`NetworkStream` rather than manually exposing Windows IOCP objects.

## Wire contracts verified

### Outer TCP frame

Original `KSession::SendPacket` serializes the complete `KEvent`, wraps it in `KSecureBuffer`, then prepends a `USHORT` containing the **total frame size including the two-byte length field**. The receive path reads that same `USHORT`, waits for the complete frame, and passes only `length - sizeof(USHORT)` bytes to `KSecureBuffer`. This is implemented by `KncProtocol`.

### Security handshake

`KSession::OnAcceptConnection` creates a fresh SA/SPI, serializes `SPI + SecurityAssociation` into an `E_ACCEPT_CONNECTION_NOT` event, sends that event while the session still uses SPI 0/default security, and only then changes the session SPI to the fresh value. `KncServerSession` preserves this order exactly.

### Packet authentication

TCP uses the legacy **no-replay-window** path (`Create_notRWM` / `IsAuthentic_notRWM`). A non-zero SPI advances sequence numbers after send. SPI 0 does not advance the sequence. Authentication failure increments a session counter; the legacy session disconnects once failures become greater than the configured limit. The managed session preserves the same `>` comparison.

### Heartbeat

Server-side (unproxy) sessions do not actively send heartbeat packets. They refresh their receive timestamp only after an authenticated packet has been decrypted and decoded as `KEvent`, then treat a gap greater than 60 seconds as a zombie and reserve destruction. The managed session uses a 15-second periodic check against the same 60-second threshold.

### Nagle / socket behavior

Legacy `KAccepter` has Nagle enabled by default and only sets `TCP_NODELAY` when explicitly disabled. Managed `TcpClient.NoDelay` therefore defaults to `false`; `--no-delay` opts into the legacy `TCP_NODELAY` branch.

## Ownership / lifetime / concurrency decisions

- One `KncServerSession` owns one accepted TCP connection and its security state.
- Host tracks active session tasks in a `ConcurrentDictionary<long, Task>` and removes them when their run task completes.
- A linked cancellation token owns session shutdown; when the receive task or heartbeat monitor finishes, the other is cancelled immediately.
- Send operations are serialized by one `SemaphoreSlim`, preventing interleaving of complete legacy frames while allowing independent receive processing.
- Managed code does not recreate the old custom overlapped buffer/circular send queue. `NetworkStream.WriteAsync` provides the actual socket write/backpressure primitive; no invented packet transformation is inserted between KEvent and the wire frame.
- No `unsafe`, `dynamic`, reflection-based dispatch, or third-party networking library is required for this transport stage.

## Source evidence checked this stage

- `KncWX2Server/Common/NetLayer.h/.cpp`
- `KncWX2Server/Common/ActorFactory.h`
- `KncWX2Server/Common/ActorManager.h`
- `KncWX2Server/Common/Socket/Session.h/.cpp`
- `KncWX2Server/Common/Socket/SocketObject.h/.cpp`
- `KncWX2Server/Common/Socket/IOCP.h/.cpp`
- `KncWX2Server/Common/Socket/IOThread.h/.cpp`
- `KncWX2Server/Common/Socket/Overlapped.h`
- `KncWX2Server/Common/Socket/Accepter.h/.cpp`
- `KncWX2Server/Common/Event.h/.cpp`
- `KncWX2Server/Common/EventID_System.h`
- `KncWX2Server/Common/KncUidType.h`
- `KNCSDK/Include/KncSecurity/SecureBuffer.h/.cpp`
- `KNCSDK/Include_2010/KncSecurity/ByteStream.h/.cpp`
- `KNCSDK/Include_2010/KncSecurity/SecurityAssociation.h/.cpp`
- `KNCSDK/Include/KncSecurity/KncSecurity.h`
- `KNCSDK/Include/Serializer/Serializer.h`
- active C# conversion files under `KncWX2Server.Net/src/KncWX2Server.Core*` and `KncWX2Server.Net/src/KncWX2Server.Host`

## Current converted components

- `KncProtocol` — exact legacy TCP frame length semantics
- `KncServerSession` — accepted-session lifetime, handshake, auth/decrypt, event decode, heartbeat and send serialization
- `KncServerHost` — listener/session ownership and orderly shutdown
- `KPerformerInfo` — legacy 2000 UID limit
- `EventIds` — verified system-event prefix
- `SecurityAssociation` / `SecurityAssociationDatabase` / `SecureBuffer`
- `KSerializer` / `SerBuffer` / `ByteStream` / `KEvent`

## Regression coverage

`KncWX2Server.Core.RegressionTests` now also verifies:

- `KPerformerInfo` 2000-element limit
- exact KEvent serialization field order
- two-byte little-endian total-frame length
- secure frame payload placement
- intentionally short complete frames remaining eligible for security-layer validation
- previous serializer, compression, security, ICV and replay-window checks

## Known verification limitation

The execution environment currently has no installed .NET SDK (`dotnet --info` previously returned `command not found`). Therefore source-level and cross-source verification is complete, but this stage has not been truthfully reported as successfully built, executed, or NativeAOT-published.

## Next unlock

The next highest-leverage subsystem is **server-side event dispatch + actor/session manager ownership**. The transport contract is now stable enough to map `QueueingEvent`, `KPerformer`, `KActor`, `KActorManager`, FSM/event processing, deferred add/delete, and each role's event dispatch tables before converting Login/Center/Channel/Game handlers.
