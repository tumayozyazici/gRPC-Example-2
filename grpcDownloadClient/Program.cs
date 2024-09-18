using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using grpcFileTransportClient;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var channel = GrpcChannel.ForAddress("http://localhost:5202");
        var client = new FileService.FileServiceClient(channel);

        string downloadPath = @"C:";

        var fileInfo = new grpcFileTransportClient.FileInfo
        {
            FileExtension = ".mp4",
            FileName = "321"
        };

        FileStream fileStream = null;

        CancellationTokenSource tokenSource = new CancellationTokenSource();

        var request = client.FileDownload(fileInfo);

        decimal chunkSize = 0;
        int count = 0;

        while (await request.ResponseStream.MoveNext(tokenSource.Token))
        {
            if (count++ == 0)
            {
                fileStream = new FileStream($@"{downloadPath}\{request.ResponseStream.Current.Info.FileName}{request.ResponseStream.Current.Info.FileExtension}", FileMode.CreateNew);
                fileStream.SetLength(request.ResponseStream.Current.FileSize);
            }

            var buffer = request.ResponseStream.Current.Buffer.ToByteArray();
            await fileStream.WriteAsync(buffer, 0, request.ResponseStream.Current.ReadedByte);
            Console.WriteLine($"{Math.Round((chunkSize += request.ResponseStream.Current.ReadedByte) * 100 / request.ResponseStream.Current.FileSize)}%");
        }
        Console.WriteLine("Yüklendi");
        await fileStream.DisposeAsync();
        fileStream.Close();
    }
}