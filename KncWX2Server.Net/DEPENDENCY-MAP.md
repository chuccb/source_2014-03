# KncWX2Server migration dependency map

Date: 2026-08-27
Scope: `KncWX2Server` + its actual SDK/source dependencies. `CA.exe.c` is explicitly out of scope for this migration.

## Layer graph

```text
Legacy serializer primitives
  ├─ ByteOrder (network numeric = big-endian; wchar_t = Win32 UTF-16LE bytes)
  ├─ SerBuffer (raw buffer + compression metadata)
  └─ KSerializer
       │
       ├─ SecurityAssociation serialization state
       │
       └─ application/user-class serialization

KncSecurity
  ├─ SecurityAssociation
  │    ├─ 8-byte auth key
  │    ├─ 8-byte DES key
  │    ├─ 32-bit sequence number
  │    └─ 32-bit replay-window state
  │
  ├─ SecurityAssociationDatabase
  │    └─ SPI -> SecurityAssociation, with mandatory SPI 0 default SA
  │
  └─ SecureBuffer
       ├─ [SPI:u16 LE]
       ├─ [Sequence:u32 LE]
       ├─ [IV:8]
       ├─ [DES-CBC ciphertext]
       └─ [ICV:HMAC-MD5 truncated to 10]
             │
             ▼
       socket/session packet framing
             │
             ▼
       Login / Center / Channel / Game services
             │
             └─ Persistence / shared state
```

## Why this subsystem is next

The security/serialization foundation is the highest-leverage dependency because it sits below packet framing, session state and every service endpoint. Any endian, tag, size, SPI, sequence, padding, ICV or DES-CBC mistake would contaminate every subsequent subsystem.

## Evidence-checked invariants

- `SpiType` is `unsigned short` => exactly 2 bytes.
- `SeqType` is `unsigned int` => exactly 4 bytes.
- `SecurityAssociation`: auth key 8, crypto key 8, IV 8, ICV 10, DES block 8, max extra padding blocks 1.
- Serializer host is little-endian; network integer representation is explicitly big-endian.
- `KSecureBuffer` itself appends SPI and sequence as native Win32 integer bytes, therefore little-endian on the legacy platform.
- Received wire SPI is parsed for validation, but the expected/session `_spi` remains authoritative; the old source intentionally does not replace it with the wire SPI.
- SPI 0 default security association exists before any connection is accepted.
- Normal replay-window sequence checking is distinct from the TCP no-replay-window path.
- ICV authenticates every byte before the final 10-byte ICV.
- DES-CBC input is already padded and must be a multiple of 8 bytes.
- Legacy IV generation currently emits eight values in the range ASCII `A`..`h` (`65..104`), rather than using the commented secure RNG.
- Current legacy padding uses no extra random blocks because the active source sets `nRand = 0`; the maximum-size helper still reserves one possible extra block.

## Current conversion status

### Verified / present

- `KncWX2Server.slnx`
- common C# project configuration targeting `net11.0` with preview language mode
- `SecurityAssociation.cs`
- `SecurityAssociationDatabase.cs`
- `SecureBuffer.cs`
- `ByteStream.cs`
- `KSerializer.cs`

### Requires correction / hardening in this stage

- remove dead double-randomization in `SecureBuffer.GenerateIv`
- remove LINQ allocation from security-key generation
- reduce avoidable cryptographic temporary allocations where this can be done without changing wire behavior
- add regression coverage for serializer byte order, UTF-16LE wire bytes, security-buffer round trip, padding, ICV and sequence/replay semantics
- document that the security-buffer wire header uses little-endian independently from the serializer's big-endian numeric format

## Next subsystem after this stage

After the foundation passes regression validation, the next highest-unlock subsystem is the **session/transport packet framing and dispatch layer**: callers/callees around socket receive/send, packet header handling, sequence/security integration, initialization and shutdown. This layer should be mapped before touching individual Login/Center/Channel/Game handlers.
