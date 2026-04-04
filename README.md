# GGUFSharp

GGUFSharp is a small .NET library for inspecting GGUF files from C#. It focuses on reading the GGUF header, metadata entries, tensor descriptors, and raw tensor bytes so that .NET applications can analyze model files without depending on Python tooling.

Important note: the current implementation is a reader. The repository contains a sample Python script that generates a test GGUF file, but the C# library itself does not currently expose a GGUF writer API.

## Features

- Read GGUF file headers and version information.
- Enumerate metadata key/value entries.
- Enumerate tensor descriptors, including tensor name, shape, type, offset, and inferred size.
- Read tensor payloads as raw bytes for further processing.
- Inspect files from the included console application in plain text or JSON.
- Validate the basic reading flow with MSTest-based unit tests.

## Project Layout

- `src/GGUFSharp/GGUFSharp`: core library targeting `netstandard2.1`.
- `src/GGUFSharp/GGUFSharp.Console`: command-line inspector targeting `net8.0`.
- `src/GGUFSharp/GGUFSharp.Test`: automated tests using the sample GGUF file.
- `SampleFiles/example.gguf`: test fixture used by the unit tests.
- `SampleFiles/genTestFile.py`: helper script used to generate the sample GGUF file.

## Requirements

- .NET SDK 8.0 or later for building the solution, running the console application, and running tests.
- Any runtime capable of consuming a `netstandard2.1` library for the `GGUFSharp` package itself.

## Installation

If you want to reference the library from another solution, add a project reference or package reference after you publish it internally.

For local development inside this repository:

```bash
dotnet build src/GGUFSharp/GGUFSharp.sln
```

To reference the project directly:

```xml
<ItemGroup>
	<ProjectReference Include="path/to/GGUFSharp.csproj" />
</ItemGroup>
```

## Quick Start

```csharp
using System;
using System.Linq;
using GGUFSharp;

var reader = new GGUFReader();
var ggufFile = reader.Read("SampleFiles/example.gguf");

Console.WriteLine($"Version: {ggufFile.Version}");
Console.WriteLine($"Metadata entries: {ggufFile.MetaItems.Count}");
Console.WriteLine($"Tensors: {ggufFile.TensorInfos.Count}");

foreach (var meta in ggufFile.MetaItems)
{
		Console.WriteLine(meta);
}

var firstTensor = ggufFile.TensorInfos.First();
using var tensorBuffer = reader.ReadTensorData(ggufFile, firstTensor);

// ReadTensorData rents a buffer and returns it as IMemoryOwner<byte>.
// Dispose it when you are done. If the caller does not release it,
// the rented memory may stay occupied longer than necessary.
// The library returns raw tensor bytes.
// You are responsible for interpreting them based on TensorType and Dimensions.
var tensorBytes = tensorBuffer.Memory.Slice(0, (int)firstTensor.Size);
Console.WriteLine($"First tensor: {firstTensor.Name}, size = {tensorBytes.Length} bytes");
```

Resource management note: `ReadTensorData` returns `IMemoryOwner<byte>` from a shared memory pool. Callers should dispose it explicitly, preferably with `using` or `await using` where appropriate, to return the rented buffer as soon as possible.

## API Overview

### `GGUFReader`

The main entry point for reading GGUF files.

- `Read(string filePath)`: reads the GGUF header, metadata section, tensor descriptors, and calculates the tensor data start offset.
- `ReadTensorData(GGUFFile file, GGUFTensorInfo tensor)`: reads the raw bytes for a specific tensor and returns them as `IMemoryOwner<byte>`. The caller owns the returned buffer and must dispose it after use.

### `GGUFFile`

Represents the parsed file structure.

- `FilePath`: full path to the source file.
- `Version`: GGUF version read from the file header.
- `DataStartOffset`: byte position where the tensor data section begins.
- `MetaItems`: parsed metadata entries.
- `TensorInfos`: parsed tensor descriptors.

### `GGUFMetaItem`

Represents one metadata entry.

- `Name`: metadata key.
- `DataType`: GGUF metadata value type.
- `ArrayElementType`: element type for array metadata.
- `RawData`: raw bytes for scalar metadata or non-string arrays.
- `ArrayStrings`: decoded values for string arrays.

### `GGUFTensorInfo`

Represents one tensor descriptor.

- `Name`: tensor name.
- `DimensionCount`: number of tensor dimensions.
- `Dimensions`: tensor shape.
- `TensorType`: storage type in GGUF.
- `Offset`: relative tensor offset in the data section.
- `Size`: inferred byte size of the tensor payload.

## Command-Line Usage

The repository includes a console application that can print a human-readable summary or JSON.

Plain text output:

```bash
dotnet run --project src/GGUFSharp/GGUFSharp.Console -- SampleFiles/example.gguf
```

JSON output:

```bash
dotnet run --project src/GGUFSharp/GGUFSharp.Console -- SampleFiles/example.gguf --json
```

Help:

```bash
dotnet run --project src/GGUFSharp/GGUFSharp.Console -- --help
```

## Running Tests

```bash
dotnet test src/GGUFSharp/GGUFSharp.sln
```

The current tests verify:

- basic metadata parsing against the bundled sample file;
- tensor name discovery;
- reading tensor bytes and decoding the sample tensor as `float` values.

## Implementation Notes

- GGUF files are read through memory-mapped I/O for efficient file access.
- Tensor sizes are inferred from sorted tensor offsets and the final file length.
- Tensor data buffers are rented from `MemoryPool<byte>.Shared`, so callers should dispose the returned `IMemoryOwner<byte>` promptly after reading.
- Metadata string arrays are decoded into `ArrayStrings` for convenience.
- Scalar metadata values are exposed as raw bytes; formatting and numeric interpretation are left to the caller, except where the console app renders them for display.

## Current Limitations

- The library currently reads GGUF files; it does not write them.
- Nested metadata arrays are not supported.
- `ReadTensorData` currently supports tensor buffers up to `Int32.MaxValue` bytes.
- If a caller does not dispose the `IMemoryOwner<byte>` returned by `ReadTensorData`, rented memory can remain occupied and increase memory pressure.
- Tensor payloads are returned as raw bytes; there is no built-in high-level tensor decoding API.
- The reader currently assumes a 32-byte alignment when calculating the tensor data start offset. The code includes a TODO for reading alignment from metadata such as `general.alignment`.

## Sample Data

The sample file in `SampleFiles/example.gguf` is generated by `SampleFiles/genTestFile.py`. It contains:

- a small set of metadata entries;
- three example tensors;
- predictable floating-point values that make the tests easy to validate.

This makes the repository suitable for learning the file layout, validating parser changes, and using the console app as a quick inspector.

## Contributing

Contributions are easiest to review when they keep the reader behavior well-defined and covered by tests. If you extend the parser, update the sample file or add new fixtures when the format scenario changes.
