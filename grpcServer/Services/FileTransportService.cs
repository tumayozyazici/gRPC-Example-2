using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using grpcFileTransportServer;

namespace grpcServer.Services
{
    public class FileTransportService : FileService.FileServiceBase
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public FileTransportService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public override async Task<Empty> FileUpload(IAsyncStreamReader<BytesContent> requestStream, ServerCallContext context)
        {
            //Streamin yapılacağı dizini belirleyelim.
            string path = Path.Combine(_webHostEnvironment.WebRootPath, "files");

            //Eğer dizinde bu isimde bir dosya yoksa oluştur ifi bu.
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            FileStream fileStream = null;

            try
            {
                int count = 0;
                decimal chunkSize = 0;

                //Burası kısaca streamin sonuna geldiğinde false dönüyor. movenext mantığı bir sonraki buffera geçemiyorsa yani yoksa false dönmesi.
                while (await requestStream.MoveNext())
                {
                    if (count == 0)
                    {
                        //Burada FileStream nesnesini oluşturup, adını ve uzantısını belirterek orada hangi işlem yapılacağını belirtiyoruz ve boyutunu atıyoruz.
                        fileStream = new FileStream($"{path}/{requestStream.Current.Info.FileName}{requestStream.Current.Info.FileExtension}", FileMode.CreateNew);
                        fileStream.SetLength(requestStream.Current.FileSize);
                        count++;
                    }

                    //Buffer stream ile gelen parçalardan biri. burada byte'ını alıyoruz.
                    var buffer = requestStream.Current.Buffer.ToByteArray();
                    await fileStream.WriteAsync(buffer, 0, buffer.Length);

                    Console.WriteLine($"{Math.Round((chunkSize += requestStream.Current.ReadedByte) * 100 / requestStream.Current.FileSize)}%");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:{ex}");
            }
            finally
            {
                if (fileStream != null)
                {
                    await fileStream.DisposeAsync();
                }
            }

            return new Empty();
        }

        public override async Task FileDownload(grpcFileTransportServer.FileInfo request, IServerStreamWriter<BytesContent> responseStream, ServerCallContext context)
        {
            string path = Path.Combine(_webHostEnvironment.WebRootPath, "files");


            FileStream fileStream = new FileStream($"{path}/{request.FileName}{request.FileExtension}", FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[2048];

            BytesContent content = new BytesContent
            {
                FileSize = fileStream.Length,
                Info = new grpcFileTransportServer.FileInfo
                {
                    FileName = Path.GetFileNameWithoutExtension(fileStream.Name),
                    FileExtension = Path.GetExtension(fileStream.Name)
                },
                ReadedByte = 0
            };

            //Metot bool dönmüyor intager dönüyor. 0 a geldiğinde okuma bitmiş oluyor.
            while ((content.ReadedByte = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                //Bytestring e dönüşmesi lazım. o dönüşüm bu.
                content.Buffer = ByteString.CopyFrom(buffer);
                await responseStream.WriteAsync(content);
            }

            fileStream.Close();
        }
    }
}
