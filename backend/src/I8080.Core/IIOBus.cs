namespace I8080.Core;

public interface IIOBus
{
    byte In(byte port);
    void Out(byte port, byte value);
}

public sealed class NullIOBus : IIOBus
{
    public byte In(byte port) => 0xFF;
    public void Out(byte port, byte value) { }
}
