using Google.Protobuf;
using Grpc.Net.Client;
using grpcFileTransportClient;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var channel = GrpcChannel.ForAddress("http://localhost:5202", new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),  // Ping to keep connection alive
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30), // Timeout before considering connection dead
                EnableMultipleHttp2Connections = true           // Allow multiple HTTP/2 connections
            }
        });

        var client = new FileService.FileServiceClient(channel);

        string file = @"C:";

        using FileStream fileStream = new FileStream(file, FileMode.Open);
        var content = new BytesContent
        {
            FileSize = fileStream.Length,
            ReadedByte = 0,
            Info = new grpcFileTransportClient.FileInfo
            {
                FileName = Path.GetFileNameWithoutExtension(fileStream.Name),
                FileExtension = Path.GetExtension(fileStream.Name)
            }
        };

        var upload = client.FileUpload();

        byte[] buffer = new byte[2048];
        int bytesRead = 0;
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            content.Buffer = ByteString.CopyFrom(buffer,0,bytesRead);
            content.ReadedByte = bytesRead;
            await upload.RequestStream.WriteAsync(content);
        }

        await upload.RequestStream.CompleteAsync();
        fileStream.Close();
    }
}