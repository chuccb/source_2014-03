# KncWX2Server migration manifest

Updated: 2026-08-27

## Scope

This stage covers the shared serializer/security foundation used by the server conversion. `CA.exe.c` is explicitly excluded from the migration scope.

## Target stack

- C# 15 preview
- .NET 11 preview (`net11.0`)
- NativeAOT for executable projects
- `.slnx` solution format

As of 2026-08-27, Microsoft lists .NET 11 `11.0.0-preview.7` / SDK `11.0.100-preview.7` as the latest .NET 11 preview, and C# 15 as the current preview language version. The repository is already configured for `net11.0` + `LangVersion=preview`.

## This-stage dependency graph

`ByteOrder -> KSerializer / SerBuffer -> SecurityAssociation -> SecurityAssociationDatabase -> SecureBuffer -> session/transport framing -> service handlers`

Cross-version comparison with `KNCSDK/Include_2010/KncSecurity/*` found the security association and secure-buffer semantics materially aligned with the main SDK version for this layer.

## Source evidence checked

- `KNCSDK/Include/KncSecurity/SecureTypedef.h`
- `KNCSDK/Include/KncSecurity/KncSecurity.h/.cpp`
- `KNCSDK/Include/KncSecurity/SecurityAssociation.h/.cpp`
- `KNCSDK/Include/KncSecurity/SADatabase.h/.cpp`
- `KNCSDK/Include/KncSecurity/SecureBuffer.h/.cpp`
- `KNCSDK/Include/Serializer/SerDefs.h`
- `KNCSDK/Include/Serializer/ByteOrder.h`
- `KNCSDK/Include/Serializer/Serializer.h/.cpp`
- `KNCSDK/Include/Serializer/SerBuffer.cpp`
- corresponding `KNCSDK/Include_2010/KncSecurity/SecurityAssociation.*` and `SecureBuffer.cpp`

## Corrections applied

1. Confirmed `SpiType` is `unsigned short`; C# remains `ushort`.
2. Preserved the secure-buffer header as little-endian (`SPI:u16`, `Sequence:u32`) because the original `KByteStream::Append` path writes native Win32 integer bytes.
3. Preserved serializer numeric fields as big-endian because the legacy serializer explicitly uses `BYTEORDER_NET=BIG`.
4. Removed LINQ from random key generation; key bytes remain an inclusive `1..255` range and retain 8-byte sizes.
5. Removed dead `Random.Shared.NextBytes` work from IV generation. The active legacy code generates each IV byte from `A + rand()%40`, and the conversion now performs only that active operation.
6. Reduced ICV validation allocation by using a stack destination and `HMACMD5.HashData`, while retaining the 10-byte truncated HMAC-MD5 wire value.
7. Added a ciphertext-length block-alignment guard before DES-CBC decryption; malformed input now fails before reaching the cryptographic transform.
8. Kept the legacy SPI validation behavior: the wire SPI is parsed, but the expected/session SPI remains authoritative.

## Regression coverage added

`KncWX2Server.Core.RegressionTests` checks:

- serializer primitive tags and big-endian numeric bytes
- Win32 `wchar_t` / UTF-16LE byte layout
- SerBuffer compression round trip
- secure-buffer creation and payload round trip
- secure-buffer SPI/sequence little-endian layout
- HMAC/ICV tamper rejection
- TCP no-replay-window duplicate rejection
- replay-window duplicate/out-of-order/zero-sequence semantics

The regression project is included directly in `KncWX2Server.slnx` and therefore also inherits the repository's NativeAOT executable setting.

## Build verification

Not executed successfully in this environment: the execution container has no `dotnet` installation (`dotnet --info` returned command not found), and outbound GitHub cloning is unavailable. Therefore no claim of `dotnet build`, test execution, or NativeAOT publish success is made.

## Current status

Foundation implementation and regression source are committed to the active rewrite branch. Source-level checks and cross-version comparison completed; runtime/build verification remains pending on an environment with the .NET 11 preview SDK installed.

## Next subsystem

**Session / transport packet framing and dispatch** is the next highest-leverage subsystem. Before converting individual Login/Center/Channel/Game handlers, map socket ownership, receive/send loops, packet header parsing, security-buffer invocation, protocol dispatch tables, initialization and shutdown, and concurrency/lifetime rules.
