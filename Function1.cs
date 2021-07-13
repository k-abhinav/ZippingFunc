using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO.Compression;
using Azure.Storage.Blobs;

namespace zipping_function
{
    public static class Function1
    {
        [FunctionName("zip-file")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string filename = data?.sourceFileName;
            string filenameWithoutExt = filename.Split('.')[0];
            string localPath = Path.GetTempPath();
            var downloadPath = Path.Combine(localPath, filename);
            var zippedPath = Path.Combine(localPath, filenameWithoutExt + ".zip");

            string sourceContainerName = data?.sourceContainerName;
            string destinationContainerName = data?.destinationContainerName;
            string connectionString = Environment.GetEnvironmentVariable("BlobConnectionString", EnvironmentVariableTarget.Process);
            BlobContainerClient sourceContainer = new BlobContainerClient(connectionString, sourceContainerName);
            BlobContainerClient destinationContainer = new BlobContainerClient(connectionString, destinationContainerName);
            try
            {
                BlobClient sourceBlobClient = sourceContainer.GetBlobClient(filename);
                sourceBlobClient.DownloadTo(downloadPath);
                using (var zip = ZipFile.Open(Path.ChangeExtension(downloadPath, ".zip"), ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(downloadPath, Path.GetFileName(downloadPath));
                }
                BlobClient destinationBlobClient = destinationContainer.GetBlobClient(filenameWithoutExt + ".zip");
                destinationBlobClient.Upload(zippedPath, true);
            }
            catch(Exception ex)
            {
                return new BadRequestObjectResult(ex?.Message);
            }
            finally
            {
                File.Delete(downloadPath);
                File.Delete(zippedPath);
            }
            string responseMessage = @"File zipped successfully";
            return new OkObjectResult(responseMessage);
        }
    }
}