# pixockets
A rapid lightweight UDP library.
It is specialized for soft realtime applications with heavy per-connection traffic.

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
   * Windows, macOS, Linux, iOS, Android 

## Usage:
### Client
```csharp
var bufferPool = new ByteBufferPool();
var sock = new SmartSock(bufferPool, new ThreadSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub()), null);
sock.Connect(IPAddress.Loopback, 2345);
var cnt = 0;
var packet = new ReceivedSmartPacket();
while (!Console.KeyAvailable)
{
    while (sock.Receive(ref packet))
    {
        if (sock.State == PixocketState.Connected)
        {
            var buffer = BitConverter.GetBytes(cnt++);
            sock.Send(buffer, 0, buffer.Length, false);
        }
        if (!packet.InOrder)
        {
            Console.WriteLine("!!! OutOfOrder !!!");
        }
        var recv = BitConverter.ToInt32(packet.Buffer, packet.Offset);
        Console.WriteLine("Client Received: {0}", recv);
        bufferPool.Put(packet.Buffer);
    }

    sock.Tick();
    Thread.Sleep(50);
}

sock.Disconnect();
sock.Close();

```
### Server
```csharp
var bufferPool = new ByteBufferPool();
var sock = new SmartSock(bufferPool, new ThreadSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub()), null);
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
        Console.WriteLine("Server Received: {0}", recv);
        sock.Send(packet.EndPoint, packet.Buffer, packet.Offset, packet.Length, false);
        bufferPool.Put(packet.Buffer);
    }

    sock.Tick();
    Thread.Sleep(50);
}

sock.DisconnectAll();
sock.Close();

```
