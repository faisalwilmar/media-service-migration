using Azure.Storage.Blobs;
using MigrationMediaService.Classes.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MigrationMediaService.Services
{
    public class StorageHandler
    {
        private Dictionary<string, BlobServiceClient> _clients = new Dictionary<string, BlobServiceClient>();

        private readonly string SOURCE_CLIENT_NAME = "SourceClientName";
        private readonly string TARGET_CLIENT_NAME = "TargetClientName";

        public async Task CopyData(string sourceContainerName, string sourceFilename,
            string targetContainerName, string targetFilename)
        {
            // get target clients
            BlobServiceClient _targetClient;
            if (_clients.ContainsKey(TARGET_CLIENT_NAME))
            {
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }
            else
            {
                _clients.Add(TARGET_CLIENT_NAME, new BlobServiceClient(Environment.GetEnvironmentVariable("TargetClientConn")));
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }

            try
            {
                var sourceEndpoint = Environment.GetEnvironmentVariable("SourceClientEndpoint");

                if (sourceEndpoint == "" || sourceContainerName == "" || sourceFilename == "")
                {
                    throw new Exception($"SourceEndpoint, SourceContainerName, SourceFilename are required, any one of them are missing");
                }

                var sourceUri = new Uri($"{sourceEndpoint.Trim('/')}/{sourceContainerName}/{sourceFilename}");

                // Get a reference to a container named "sample-container" and then create it
                BlobContainerClient blobContainerTarget = _targetClient.GetBlobContainerClient(targetContainerName);
                BlobClient blob = blobContainerTarget.GetBlobClient(targetFilename);
                if (await FileIsExists(targetContainerName, targetFilename))
                {
                    DeleteData(targetContainerName, targetFilename);
                }
                await blob.StartCopyFromUriAsync(sourceUri);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("pending copy operation"))
                {
                    throw new PendingException($"Pending copying {sourceFilename}");
                }
                throw new Exception($"Unable to copy data '{sourceFilename}' from '{sourceContainerName}' to '{targetContainerName}'", ex);
            }
        }

        public async Task CopyData(Uri sourceUri, string targetContainerName, string targetFilename)
        {
            // get target clients
            BlobServiceClient _targetClient;
            if (_clients.ContainsKey(TARGET_CLIENT_NAME))
            {
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }
            else
            {
                _clients.Add(TARGET_CLIENT_NAME, new BlobServiceClient(Environment.GetEnvironmentVariable("TargetClientConn")));
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }

            try
            {
                BlobContainerClient blobContainerTarget = _targetClient.GetBlobContainerClient(targetContainerName);
                BlobClient blob = blobContainerTarget.GetBlobClient(targetFilename);
                if (await FileIsExists(targetContainerName, targetFilename))
                {
                    DeleteData(targetContainerName, targetFilename);
                }
                await blob.StartCopyFromUriAsync(sourceUri);

            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("pending copy operation"))
                {
                    throw new PendingException($"Pending copying {sourceUri}");
                }
                throw new Exception($"Unable to copy data '{sourceUri}' to '{targetContainerName}'", ex);
            }
        }

        public async Task<MemoryStream> GetDataVideo(string sourceContainerName, string sourceFilename)
        {
            // get source clients
            BlobServiceClient _sourceClient;
            if (_clients.ContainsKey(SOURCE_CLIENT_NAME))
            {
                _sourceClient = _clients[SOURCE_CLIENT_NAME];
            }
            else
            {
                _clients.Add(SOURCE_CLIENT_NAME, new BlobServiceClient(Environment.GetEnvironmentVariable("SourceClientConn")));
                _sourceClient = _clients[SOURCE_CLIENT_NAME];
            }

            try
            {
                MemoryStream ms = new MemoryStream();
                BlobContainerClient blobContainerSource = _sourceClient.GetBlobContainerClient(sourceContainerName);
                await blobContainerSource.GetBlobClient(sourceFilename).DownloadToAsync(ms);
                ms.Position = 0;

                return ms;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to get data '{sourceFilename}' from '{sourceContainerName}'", ex);
            }
        }

        public async Task SaveData(string targetContainerName, string targetFilename, MemoryStream ms, string connectionString = null)
        {
            // get source clients
            BlobServiceClient _targetClient;
            if (_clients.ContainsKey(TARGET_CLIENT_NAME))
            {
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }
            else
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = Environment.GetEnvironmentVariable("TargetClientConn");
                }
                _clients.Add(TARGET_CLIENT_NAME, new BlobServiceClient(connectionString));
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }

            try
            {
                // Get a reference to a container named "sample-container" and then create it
                BlobContainerClient blobContainerTarget = _targetClient.GetBlobContainerClient(targetContainerName);
                BlobClient blob = blobContainerTarget.GetBlobClient(targetFilename);
                if(await FileIsExists(targetContainerName, targetFilename))
                {
                    DeleteData(targetContainerName, targetFilename);
                }
                await blob.UploadAsync(ms);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to save data '{targetFilename}' to '{targetContainerName}'", ex);
            }
        }

        public async Task<bool> FileIsExists(string containerName, string filename)
        {
            BlobServiceClient _targetClient;
            if (_clients.ContainsKey(TARGET_CLIENT_NAME))
            {
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }
            else
            {
                _clients.Add(TARGET_CLIENT_NAME, new BlobServiceClient(Environment.GetEnvironmentVariable("TargetClientConn")));
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }

            BlobContainerClient blobContainerTarget = _targetClient.GetBlobContainerClient(containerName);
            BlobClient blob = blobContainerTarget.GetBlobClient(filename);
            return await blob.ExistsAsync();
        }

        public void DeleteData(string containerName, string filename)
        {
            // get target clients
            BlobServiceClient _targetClient;
            if (_clients.ContainsKey(TARGET_CLIENT_NAME))
            {
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }
            else
            {
                _clients.Add(TARGET_CLIENT_NAME, new BlobServiceClient(Environment.GetEnvironmentVariable("TargetClientConn")));
                _targetClient = _clients[TARGET_CLIENT_NAME];
            }

            try
            {
                BlobContainerClient blobContainerTarget = _targetClient.GetBlobContainerClient(containerName);
                BlobClient blob = blobContainerTarget.GetBlobClient(filename);
                blob.DeleteIfExists();
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to delete data '{filename}'", ex);
            }
        }
    }
}
