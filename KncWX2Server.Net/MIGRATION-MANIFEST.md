# KncWX2Server migration manifest

Updated: 2026-08-27

## Scope

This stage covers the shared serializer/security foundation plus the server-side TCP transport/session layer. `CA.exe.c` is explicitly excluded from the migration scope.

## Target stack

- C# 15 preview
- .NET 11 preview (`net11.0`)
- NativeAOT for executable projects
- `.slnx` solution format

The repository is configured in `Directory.Build.props` for `net11.0`, preview C#, `IsAotCompatible=true`, server GC and Tiered PGO, with NativeAOT enabled for executable projects.

## Current dependency graph

```text
Serializer / SerBuffer / ByteStream
        ↓
SecurityAssociation / SADB / SecureBuffer
        ↓
KEvent / KPerformerInfo
        ↓
2-byte total-length TCP framing
        ↓
KncServerHost
        ↓
KncServerSession
        ├─ SPI 0 security handshake
        ├─ no-replay-window packet authentication
        ├─ DES-CBC decrypt / ICV validation
        ├─ KEvent deserialization + optional buffer decompression
        ├─ heartbeat timeout
        └─ serialized send path
        ↓
server event dispatch
        ↓
actor/session managers
        ↓
Login / Center / Channel / Game handlers
        ↓
Persistence / shared state
```

## Transport source cross-check

Checked the declarations and implementations of:

- `KncWX2Server/Common/NetLayer.h/.cpp`
- `KncWX2Server/Common/Socket/Session.h/.cpp`
- `KncWX2Server/Common/Socket/SocketObject.h/.cpp`
- `KncWX2Server/Common/Socket/IOCP.h/.cpp`
- `KncWX2Server/Common/Socket/IOThread.h/.cpp`
- `KncWX2Server/Common/Socket/Overlapped.h`
- `KncWX2Server/Common/Socket/Accepter.h/.cpp`
- `KncWX2Server/Common/Event.h/.cpp`
- `KncWX2Server/Common/EventID_System.h`
- `KncWX2Server/Common/ActorFactory.h`
- `KncWX2Server/Common/ActorManager.h`
- `KncWX2Server/Common/KncUidType.h`
- role NetLayer headers/implementation for Login, Channel, Global and Game

Also rechecked the security and serializer SDK sources, including `KNCSDK/Include/KncSecurity/SecureBuffer.h/.cpp`, `KNCSDK/Include_2010/KncSecurity/ByteStream.h/.cpp`, `KNCSDK/Include_2010/KncSecurity/SecurityAssociation.h/.cpp`, `KNCSDK/Include/KncSecurity/KncSecurity.h`, and `KNCSDK/Include/Serializer/Serializer.h`.

## Important corrections found during this stage

1. The earlier managed `Protocol.cs` used an invented `int payloadLength + ushort opcode + ushort flags` header. It was removed. The real server protocol is a little-endian `USHORT` total frame length followed by a `KSecureBuffer`.
2. `KPerformerInfo.MaxUidCount` was corrected from 100 to the native `MAX_UID_NUM=2000`.
3. Host socket defaults were corrected: legacy Nagle is enabled by default, and server-side sequence checking is disabled by default. Explicit options can enable `--no-delay` and `--sequence-check`.
4. Session heartbeat state now refreshes only after an authenticated/decrypted packet has been successfully deserialized as `KEvent`, matching the native unproxy session semantics.
5. Session lifetime now cancels the peer heartbeat/receive task immediately when either completes, preventing a completed socket from leaving a monitor task alive.
6. Complete but undersized frames are not rejected by the framing layer merely for being smaller than the secure-buffer minimum; they proceed to the security validation path, matching native receive behavior for such packets.
7. The session transport uses BCL async sockets instead of exposing the old WinSock/IOCP implementation directly. This changes the implementation mechanism but not the observable KNC frame/security contract.

## Managed transport implementation

- `KncProtocol.cs`: exact two-byte little-endian total-frame framing.
- `KncServerSession.cs`: per-connection ownership, SPI/security state, handshake, authenticated receive loop, decompression, heartbeat monitor, ordered sends and auth-failure threshold.
- `KncServerHost.cs`: listener ownership, Nagle option, session task registry and orderly cancellation.
- `EventIds.cs`: verified system-event prefix needed by the session layer.

## Regression coverage

`KncWX2Server.Core.RegressionTests` now checks:

- serializer primitive wire order
- UTF-16LE wide strings
- SerBuffer compression round trip
- legacy 2000 UID limit
- exact KEvent serialized field order
- two-byte little-endian total frame length
- complete short frame acceptance by the framing layer
- SecureBuffer round trip
- ICV tamper rejection
- no-replay-window duplicate rejection
- replay-window duplicate/out-of-order/zero-sequence behavior

## Build verification

Not executed successfully in the available execution environment. `dotnet --info` is unavailable because the container does not have the .NET SDK installed. No claim of successful `dotnet build`, test execution, or NativeAOT publish is made.

## Current status

The serializer/security foundation is cross-checked against the native implementation, and the shared server TCP transport/session boundary is implemented on the rewrite branch. The transport now consumes the exact native frame format rather than the earlier invented header.

## Next subsystem

**Server-side event dispatch and actor/session manager** is now the highest-leverage next subsystem. Before implementing individual Login/Center/Channel/Game business handlers, map `KPerformer`, `KActor`, `KPerformer::QueueingEvent`, actor manager add/delete/tick behavior, FSM/event routing, thread ownership, and service-specific dispatch tables.
