using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace Fennec.Client;

public static class Ipv4HttpHandler
{
    public static SocketsHttpHandler Create()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;
                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        if (Environment.GetEnvironmentVariable("FENNEC_SKIP_TLS_VERIFY") == "1")
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };

        return handler;
    }
}
