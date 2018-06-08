# pixockets
A rapid lightweight UDP library.

## Features
* Small memory traffic
* Pure C#
* Optional reliable
* Optional ordered
* Big (>MTU) messages fragmentation
* Can be used without any reliability/ordering/fragmentation overhead
* IPv6 support
* Platfroms:
   * Unity3d support
   * .NET Framework, Mono, .NET Core

## Usage:
### Client
```csharp
var bufferPool = new CoreBufferPool();
var sock = new SmartSock(bufferPool, new ThreadSock(bufferPool, AddressFamily.InterNetwork), null);
sock.Connect(IPAddress.Loopback, 2345);
var cnt = 0;
var packet = new ReceivedSmartPacket();
while (!Console.KeyAvailable)
{
    var buffer = BitConverter.GetBytes(cnt++);
    sock.Send(buffer, 0, buffer.Length, false);
    sock.Tick();
    while (sock.Receive(ref packet))
    {
        if (!packet.InOrder)
        {
            Console.WriteLine("!!! OutOfOrder !!!");
        }
        var recv = BitConverter.ToInt32(packet.Buffer, packet.Offset);
        Console.WriteLine("Recv: {0}", recv);
    }

    Thread.Sleep(50);
}

sock.Close();

```
### Server
```csharp
var bufferPool = new CoreBufferPool();
var sock = new SmartSock(bufferPool, new ThreadSock(bufferPool, AddressFamily.InterNetwork), null);
sock.Listen(2345);
var packet = new ReceivedSmartPacket();
while (!Console.KeyAvailable)
{
    while (sock.Receive(ref packet))
    {
        if (!packet.InOrder)
        {
            Console.WriteLine("!!! OutOfOrder !!!");
        }

        var recv = BitConverter.ToInt32(packet.Buffer, packet.Offset);
        Console.WriteLine("Recv: {0}", recv);
        sock.Send(packet.EndPoint, packet.Buffer, packet.Offset, packet.Length, false);
    }

    sock.Tick();
    Thread.Sleep(50);
}

sock.Close();

```
