namespace ExampleWithConfig.Config;

public class TestConfigStructure
{
    public string? TestString { get; init; } = "test string field";
    public int TestInt { get; init; } = 12345;
    public short TestShort { get; init; } = 12345;
    public byte TestByte { get; init; } = 123;
    public ushort TestUShort { get; init; } = 12345;
    public sbyte TestSByte { get; init; } = 123;
    public char TestChar { get; init; } = 'A';
    public ulong TestULong { get; init; } = 1234567890;
    public uint TestUInt { get; init; } = 12345;
    public long TestLong { get; init; } = 1234567890;
    public bool TestBool { get; init; } = true;
    public float TestFloat { get; init; } = 123.456f;
    public double TestDouble { get; init; } = 123.456;
    public decimal TestDecimal { get; init; } = 123.456m;
    
    public string[] TestArray { get; init; } = [ "test", "array" ];
}