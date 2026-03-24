using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.Json;
using GGUFSharp;

namespace GGUFSharp.ConsoleApp
{
    internal static class Program
    {
        private static readonly ResourceManager ResourceManager = new("GGUFSharp.Console.Resources", typeof(Program).Assembly);

        internal static int Main(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            if (args.Length == 0)
            {
                WriteError(GetString("MissingFilePath"));
                WriteUsage();
                return 1;
            }

            if (args.Any(IsHelpSwitch))
            {
                WriteUsage();
                return 0;
            }

            var jsonOutput = false;
            string? filePath = null;
            foreach (var arg in args)
            {
                if (IsJsonSwitch(arg))
                {
                    jsonOutput = true;
                    continue;
                }

                if (filePath is null)
                {
                    filePath = arg;
                    continue;
                }

                WriteError(GetString("InvalidArguments"));
                WriteUsage();
                return 1;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                WriteError(GetString("MissingFilePath"));
                WriteUsage();
                return 1;
            }

            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath))
                {
                    WriteError(string.Format(CultureInfo.CurrentCulture, GetString("FileNotFound"), fullPath));
                    return 1;
                }

                var reader = new GGUFReader();
                var ggufFile = reader.Read(fullPath);
                if (jsonOutput)
                {
                    WriteFileStructureAsJson(ggufFile);
                }
                else
                {
                    WriteFileStructure(ggufFile);
                }

                return 0;
            }
            catch (ArgumentException ex)
            {
                WriteError(ex.Message);
                return 1;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteError(ex.Message);
                return 1;
            }
            catch (IOException ex)
            {
                WriteError(ex.Message);
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                WriteError(ex.Message);
                return 1;
            }
            catch (NotSupportedException ex)
            {
                WriteError(ex.Message);
                return 1;
            }
        }

        private static bool IsHelpSwitch(string arg) =>
            string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase);

        private static bool IsJsonSwitch(string arg) =>
            string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-j", StringComparison.OrdinalIgnoreCase);

        private static void WriteUsage()
        {
            Console.WriteLine(GetString("Description"));
            Console.WriteLine(GetString("Usage"));
            Console.WriteLine(GetString("JsonOption"));
        }

        private static void WriteFileStructure(GGUFFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            Console.WriteLine($"{GetString("FileLabel")}: {file.FilePath}");
            Console.WriteLine($"{GetString("VersionLabel")}: {file.Version}");
            Console.WriteLine($"{GetString("MetadataCountLabel")}: {file.MetaItems.Count}");
            Console.WriteLine($"{GetString("TensorCountLabel")}: {file.TensorInfos.Count}");
            Console.WriteLine($"{GetString("DataStartOffsetLabel")}: {file.DataStartOffset}");
            Console.WriteLine();
            Console.WriteLine(GetString("MetadataTitle"));

            if (file.MetaItems.Count == 0)
            {
                Console.WriteLine($"  {GetString("NoneValue")}");
            }
            else
            {
                foreach (var metaItem in file.MetaItems)
                {
                    Console.WriteLine($"  {FormatMetadataItem(metaItem)}");
                }
            }

            Console.WriteLine();
            Console.WriteLine(GetString("TensorsTitle"));
            if (file.TensorInfos.Count == 0)
            {
                Console.WriteLine($"  {GetString("NoneValue")}");
                return;
            }

            foreach (var tensorInfo in file.TensorInfos)
            {
                Console.WriteLine($"  {FormatTensorItem(tensorInfo)}");
            }
        }

        private static void WriteFileStructureAsJson(GGUFFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            var output = new
            {
                filePath = file.FilePath,
                version = file.Version,
                metadataCount = file.MetaItems.Count,
                tensorCount = file.TensorInfos.Count,
                dataStartOffset = file.DataStartOffset,
                metadata = file.MetaItems.Select(item => new
                {
                    name = item.Name,
                    dataType = GetEnumName(item.DataType),
                    arrayElementType = item.ArrayElementType is null ? null : GetEnumName(item.ArrayElementType.Value),
                    value = GetMetadataJsonValue(item)
                }),
                tensors = file.TensorInfos.Select(tensorInfo => new
                {
                    name = tensorInfo.Name,
                    dimensionCount = tensorInfo.DimensionCount,
                    dimensions = tensorInfo.Dimensions,
                    tensorType = tensorInfo.TensorType.ToString(),
                    offset = tensorInfo.Offset,
                    size = tensorInfo.Size
                })
            };

            var json = JsonSerializer.Serialize(output, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Console.WriteLine(json);
        }

        private static string FormatMetadataItem(GGUFMetaItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var typeName = item.DataType == GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_ARRAY && item.ArrayElementType is not null
                ? string.Format(CultureInfo.CurrentCulture, GetString("ArrayTypeNameFormat"), GetEnumName(item.ArrayElementType.Value))
                : GetEnumName(item.DataType);

            return string.Format(
                CultureInfo.CurrentCulture,
                GetString("MetadataEntryFormat"),
                item.Name,
                typeName,
                FormatMetadataValue(item));
        }

        private static string FormatMetadataValue(GGUFMetaItem item)
        {
            return item.DataType switch
            {
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_STRING => item.RawData is null ? GetString("EmptyValue") : Encoding.UTF8.GetString(item.RawData),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_ARRAY => FormatArrayValue(item),
                _ => item.RawData is null ? GetString("EmptyValue") : FormatScalarValue(item.DataType, item.RawData),
            };
        }

        private static object? GetMetadataJsonValue(GGUFMetaItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            return item.DataType switch
            {
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_STRING => item.RawData is null ? null : Encoding.UTF8.GetString(item.RawData),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_ARRAY => GetArrayJsonValue(item),
                _ => item.RawData is null ? null : ParseScalarValue(item.DataType, item.RawData),
            };
        }

        private static object GetArrayJsonValue(GGUFMetaItem item)
        {
            if (item.ArrayElementType == GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_STRING)
            {
                return item.ArrayStrings ?? Array.Empty<string>();
            }

            if (item.ArrayElementType is null || item.RawData is null || item.RawData.Length == 0)
            {
                return Array.Empty<object>();
            }

            var elementSize = item.ArrayElementType.Value.GetDataTypeSize();
            if (elementSize <= 0)
            {
                return Array.Empty<object>();
            }

            var elementCount = item.RawData.Length / elementSize;
            var values = new object[elementCount];
            for (var index = 0; index < elementCount; index++)
            {
                var offset = index * elementSize;
                values[index] = ParseScalarValue(item.ArrayElementType.Value, item.RawData.AsSpan(offset, elementSize));
            }

            return values;
        }

        private static string FormatArrayValue(GGUFMetaItem item)
        {
            if (item.ArrayElementType == GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_STRING)
            {
                return FormatStringArray(item.ArrayStrings);
            }

            if (item.ArrayElementType is null || item.RawData is null || item.RawData.Length == 0)
            {
                return GetString("EmptyArrayValue");
            }

            var elementSize = item.ArrayElementType.Value.GetDataTypeSize();
            if (elementSize <= 0)
            {
                return GetString("UnsupportedValue");
            }

            var elementCount = item.RawData.Length / elementSize;
            var displayCount = Math.Min(elementCount, 8);
            var values = new List<string>(displayCount);
            for (var index = 0; index < displayCount; index++)
            {
                var offset = index * elementSize;
                values.Add(FormatScalarValue(item.ArrayElementType.Value, item.RawData.AsSpan(offset, elementSize)));
            }

            var suffix = elementCount > displayCount
                ? string.Format(CultureInfo.CurrentCulture, GetString("ArrayValueWithEllipsisFormat"), string.Join(", ", values), GetString("Ellipsis"))
                : string.Format(CultureInfo.CurrentCulture, GetString("ArrayValueFormat"), string.Join(", ", values));

            return suffix;
        }

        private static string FormatStringArray(string[]? values)
        {
            if (values is null || values.Length == 0)
            {
                return GetString("EmptyArrayValue");
            }

            var displayValues = values.Take(8).ToArray();
            var joined = string.Join(", ", displayValues);
            return values.Length > displayValues.Length
                ? string.Format(CultureInfo.CurrentCulture, GetString("ArrayValueWithEllipsisFormat"), joined, GetString("Ellipsis"))
                : string.Format(CultureInfo.CurrentCulture, GetString("ArrayValueFormat"), joined);
        }

        private static string FormatScalarValue(GGUFDataTypeEnum dataType, ReadOnlySpan<byte> value)
        {
            return ParseScalarValue(dataType, value) switch
            {
                bool boolValue => boolValue.ToString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
                string stringValue => stringValue,
                _ => GetString("UnsupportedValue")
            };
        }

        private static object ParseScalarValue(GGUFDataTypeEnum dataType, ReadOnlySpan<byte> value)
        {
            return dataType switch
            {
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_UINT8 => value[0],
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_INT8 => unchecked((sbyte)value[0]),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_UINT16 => BinaryPrimitives.ReadUInt16LittleEndian(value),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_INT16 => BinaryPrimitives.ReadInt16LittleEndian(value),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_UINT32 => BinaryPrimitives.ReadUInt32LittleEndian(value),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_INT32 => BinaryPrimitives.ReadInt32LittleEndian(value),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_FLOAT32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(value)),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_BOOL => ParseBoolean(value[0]),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_UINT64 => BinaryPrimitives.ReadUInt64LittleEndian(value),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_INT64 => BinaryPrimitives.ReadInt64LittleEndian(value),
                GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_FLOAT64 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(value)),
                _ => GetString("UnsupportedValue"),
            };
        }

        private static string FormatBoolean(byte value)
        {
            return ParseBoolean(value) switch
            {
                bool boolValue => boolValue.ToString(),
                string stringValue => stringValue,
                _ => GetString("UnsupportedValue")
            };
        }

        private static object ParseBoolean(byte value)
        {
            return value switch
            {
                0 => false,
                1 => true,
                _ => string.Format(CultureInfo.CurrentCulture, GetString("InvalidBooleanValue"), value),
            };
        }

        private static string FormatTensorItem(GGUFTensorInfo tensorInfo)
        {
            ArgumentNullException.ThrowIfNull(tensorInfo);

            var dimensions = tensorInfo.Dimensions is { Length: > 0 }
                ? string.Join("x", tensorInfo.Dimensions.Select(dimension => dimension.ToString(CultureInfo.CurrentCulture)))
                : GetString("ScalarDimensions");

            return string.Format(
                CultureInfo.CurrentCulture,
                GetString("TensorEntryFormat"),
                tensorInfo.Name,
                tensorInfo.TensorType,
                dimensions,
                tensorInfo.Offset,
                tensorInfo.Size);
        }

        private static string GetEnumName<TEnum>(TEnum value)
            where TEnum : struct, Enum =>
            Enum.GetName(typeof(TEnum), value) ?? value.ToString();

        private static string GetString(string name) =>
            ResourceManager.GetString(name, CultureInfo.CurrentCulture) ?? name;

        private static void WriteError(string message)
        {
            Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, GetString("ErrorPrefix"), message));
        }
    }
}
