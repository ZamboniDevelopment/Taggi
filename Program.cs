using NLog;
using NLog.Layouts;
using Tdf;

internal class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Logger.Info("Usage: ./Taggi <EBOOT.elf>");
            return;
        }

        uint baseAddr = 0x00010000;

        string filePath = args[0];

        if (!File.Exists(filePath))
        {
            Logger.Info("File not found: " + filePath);
            return;
        }

        var layout = new SimpleLayout("${message:withexception=true}");
        LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger()
                .WriteToConsole(layout)
                .WriteToFile("out.txt",layout);
        });

        byte[] fileBytes = File.ReadAllBytes(filePath);

        Logger.Info($"Loaded {fileBytes.Length} bytes.");
        Logger.Info(new string('=', 80));
        Logger.Info("");

        List<(int offset, Element element)> validElements = new();

        for (int i = 0; i <= fileBytes.Length - 6; i++)
        {
            byte[] candidateBytes = new byte[6];
            Array.Copy(fileBytes, i, candidateBytes, 0, 6);

            Element element = new Element(candidateBytes);

            if (element.IsSane())
            {
                if (IsValidElementStruct(fileBytes, i, element))
                {
                    validElements.Add((i, element));
                }
            }
        }

        foreach (var (offset, element) in validElements)
        {
            uint address = baseAddr + (uint)offset;
            Logger.Info($"[0x{address:X8}] Tag: {element.Tag,-4} | Type: {element.DataType + " (0x" + BitConverter.ToString(new byte[1] { element.TypeByte }) + ")",-25} | Next: {element.BytesToNextElement}");
            if (element.BytesToNextElement == 0)
            {
                Logger.Info(new string('=', 80));
                Logger.Info("");
            }
        }

        Logger.Info(new string('=', 80));
        Logger.Info($"Found {validElements.Count} valid elements.");
        Logger.Info($"Program ended.");
    }

    private static bool IsValidElementStruct(byte[] fileBytes, int currentOffset, Element currentElement)
    {
        if (currentElement.BytesToNextElement == 0)
            return true;

        int nextElementOffset = currentOffset + currentElement.BytesToNextElement;

        if (nextElementOffset + 6 > fileBytes.Length)
            return false;

        byte[] nextBytes = new byte[6];
        Array.Copy(fileBytes, nextElementOffset, nextBytes, 0, 6);


        Element nextElement = new Element(nextBytes);
        return nextElement.IsSane();
    }

    public class Element
    {
        public readonly byte[] TagBytes = new byte[3];
        public readonly byte SeparatorByte;
        public readonly byte TypeByte;
        public readonly byte BytesToNextElement;

        public readonly string Tag;
        public readonly DataType DataType;

        public Element(byte[] bytes)
        {
            if (bytes.Length != 6)
                throw new ArgumentException("Expecting 6 bytes.");

            TagBytes[0] = bytes[0];
            TagBytes[1] = bytes[1];
            TagBytes[2] = bytes[2];

            SeparatorByte = bytes[3];
            TypeByte = bytes[4];
            BytesToNextElement = bytes[5];

            DataType = (DataType)TypeByte;
            Tag = new TdfMember(TagBytes).ToString();
        }

        public bool IsSane()
        {
            if (Tag.Length <= 2 || Tag.Length > 4) //Tag possibly could be 1 letter, but its quite rare, and it produces a lot of false results here.
                return false;

            foreach (char c in Tag) //Check if characters are from the alphabet A-Z
            {
                if (c < 'A' || c > 'Z')
                    return false;
            }

            if (SeparatorByte != 0x00 && SeparatorByte != 0x01) //Usually its 0x00, rare cases 0x01.
                return false;

            // if (!Enum.IsDefined(typeof(DataType), TypeByte)) We haven't figured all type bytes so we will not filter with this yet.
            //     return false;

            return true;
        }

        public bool HasNext() => BytesToNextElement != 0;
    }

    public enum DataType : byte
    {
        SORTED_DICTIONARY = 0x00,
        LIST = 0x01,
        MAYBE_NUMBER_UINT = 0x02,
        BOOLEAN = 0x0F,
        STRING = 0x04,
        MAYBE_BLAZE_OBJECT = 0x06,
        STRUCT = 0x03,
        STRUCT2 = 0x0A,
        BYTEARRAY = 0x08,
        UNION = 0x09,
        BYTE2 = 0x10,
        BYTE = 0x11,
        USHORT_SHORT = 0x13,
        INT_UINT = 0x14,
        UINT_INT = 0x15,
        MAYBE_LONG = 0x16,
        ULONG = 0x17,
        LONG = 0x18,
        BLAZE_OBJECT_ID = 0x0C,
        ENUM2 = 0x0E,
        ENUM = 0x07,
    }
}