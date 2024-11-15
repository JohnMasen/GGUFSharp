using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GGUFSharp
{
    public   class GGUFMetaItem
    {
        public GGUFDataTypeEnum DataType { get; set; }
        public GGUFDataTypeEnum? ArrayElementType { get; set; }
        public string Name { get; set; }
        public byte[] RawData { get; set; }
        public string[] ArrayStrings { get; set; }
        public override string ToString()
        {
            switch(DataType)
            {
                case GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_STRING:
                return Encoding.UTF8.GetString(RawData);
                case GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_ARRAY:
                    if (ArrayElementType==GGUFDataTypeEnum.GGUF_METADATA_VALUE_TYPE_STRING)
                    {
                        if (ArrayStrings.Length>10)
                        {
                            return $"{string.Join(", ", ArrayStrings.Take(10))}...";
                        }
                        else
                        {
                            return string.Join(", ", ArrayStrings);
                        }
                        
                    }
                    else
                    {
                        return $"[{Enum.GetName(typeof(GGUFDataTypeEnum), ArrayElementType)}]";
                    }
                default:
                    return Enum.GetName(typeof(GGUFDataTypeEnum), DataType);
            };
        }
    }
}
