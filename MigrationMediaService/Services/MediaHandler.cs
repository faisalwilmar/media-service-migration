using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest.Azure.Authentication;
using Microsoft.WindowsAzure.Storage.Blob;
using MigrationMediaService.Classes;
using MigrationMediaService.Classes.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MigrationMediaService.Services
{
    public class MediaHandler
    {
        private Dictionary<string, AzureMediaServicesClient> _clients = new Dictionary<string, AzureMediaServicesClient>();

        private readonly Lazy<MediaCredential> _mediaCredential = new Lazy<MediaCredential>(JsonConvert.DeserializeObject<MediaCredential>(Environment.GetEnvironmentVariable("MediaCredential")));
        private readonly string CREDENTIAL_NAME = "DefaultCredential";
        private readonly string TRANSFORM_NAME = MediaTransform.Name;

        private async Task<AzureMediaServicesClient> GetClient()
        {
            AzureMediaServicesClient _client;

            if (_clients.ContainsKey(CREDENTIAL_NAME))
            {
                _client = _clients[CREDENTIAL_NAME];
            }
            else
            {
                var credential = _mediaCredential.Value;

                var clientCredential = new ClientCredential(
                        credential.AadClientId,
                        credential.AadSecret);

                var cred = await ApplicationTokenProvider.LoginSilentAsync(
                        credential.AadTenantId,
                        clientCredential,
                        ActiveDirectoryServiceSettings.Azure);

                var client = new AzureMediaServicesClient(credential.ArmEndpoint, cred)
                {
                    SubscriptionId = credential.SubscriptionId,
                };

                client.LongRunningOperationRetryTimeout = 30;

                _clients.Add(CREDENTIAL_NAME, client);
                _client = _clients[CREDENTIAL_NAME];
            }

            return _client;
        }

        public async Task SaveVideo(Models.Resource resource, MemoryStream stream)
        {
            var client = await GetClient();
            var credential = _mediaCredential.Value;

            var contentAddress = new ContentAddress(resource.ContentAddress);
            var sourceName = $"raw-{contentAddress.ContainerName}";

            Asset asset = await client.Assets.GetAsync(credential.ResourceGroup, credential.AccountName, sourceName);
            if (asset != null)
            {
                await client.Assets.DeleteAsync(credential.ResourceGroup, credential.AccountName, sourceName);
            }
            do
            {
                asset = await client.Assets.GetAsync(credential.ResourceGroup, credential.AccountName, sourceName);
            } while (asset != null);

            asset = await client.Assets.CreateOrUpdateAsync(credential.ResourceGroup, credential.AccountName,
                sourceName, new Asset(name: sourceName, container: sourceName));

            var response = await client.Assets.ListContainerSasAsync(
                credential.ResourceGroup,
                credential.AccountName,
                asset.Name,
                permissions: AssetContainerPermission.ReadWrite,
                expiryTime: DateTime.UtcNow.AddHours(4).ToUniversalTime());

            var sasUri = new Uri(response.AssetContainerSasUrls.First());
            CloudBlobContainer container = new CloudBlobContainer(sasUri);
            var blob = container.GetBlockBlobReference(Path.GetFileName(resource.Filename));

            // Use Strorage API to upload the file into the container in storage.
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task SaveVideo(Models.Resource resource, Uri uri)
        {
            try
            {
                var client = await GetClient();
                var credential = _mediaCredential.Value;

                var contentAddress = new ContentAddress(resource.ContentAddress);
                var sourceName = $"raw-{contentAddress.ContainerName}";

                Asset asset = await client.Assets.GetAsync(credential.ResourceGroup, credential.AccountName, sourceName);
                if (asset != null)
                {
                    await client.Assets.DeleteAsync(credential.ResourceGroup, credential.AccountName, sourceName);
                }
                do
                {
                    asset = await client.Assets.GetAsync(credential.ResourceGroup, credential.AccountName, sourceName);
                } while (asset != null);

                asset = await client.Assets.CreateOrUpdateAsync(credential.ResourceGroup, credential.AccountName,
                    sourceName, new Asset(name: sourceName, container: sourceName));

                var response = await client.Assets.ListContainerSasAsync(
                    credential.ResourceGroup,
                    credential.AccountName,
                    asset.Name,
                    permissions: AssetContainerPermission.ReadWrite,
                    expiryTime: DateTime.UtcNow.AddHours(4).ToUniversalTime());

                var sasUri = new Uri(response.AssetContainerSasUrls.First());
                CloudBlobContainer container = new CloudBlobContainer(sasUri);
                var blob = container.GetBlockBlobReference(Path.GetFileName(resource.Filename));

                // Use Strorage API to upload the file into the container in storage.
                await blob.StartCopyAsync(uri);
            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("pending"))
                {
                    throw new PendingException($"Pending copying {uri}");
                }
                throw ex;
            }
        }

        public async Task<List<Exception>> DeleteResource(Models.Resource resource)
        {
            var contentAddress = new ContentAddress(resource.ContentAddress);
            var sourceName = $"raw-{contentAddress.ContainerName}";
            var resultName = $"result-{contentAddress.ContainerName}";
            var jobName = $"job-{contentAddress.ContainerName}";
            var locatorName = $"locator-{contentAddress.ContainerName}";

            var client = await GetClient();
            var credential = _mediaCredential.Value;

            var error = new List<Exception>();
            try
            {
                await client.StreamingLocators.DeleteAsync(credential.ResourceGroup, credential.AccountName, locatorName);
            }
            catch (Exception e)
            {
                error.Add(new Exception($"error deleting streaming locator {resource.Id} -- {e.Message}\r\n{e.InnerException.Message}"));
            }

            try
            {
                await client.Jobs.DeleteAsync(credential.ResourceGroup, credential.AccountName, TRANSFORM_NAME, jobName);
            }
            catch (Exception e)
            {
                error.Add(new Exception($"error deleting job {resource.Id} -- {e.Message}\r\n{e.InnerException.Message}"));
            }

            try
            {
                await client.Assets.DeleteAsync(credential.ResourceGroup, credential.AccountName, sourceName);
            }
            catch (Exception e)
            {
                error.Add(new Exception($"error deleting source asset {resource.Id} -- {e.Message}\r\n{e.InnerException.Message}"));
            }

            try
            {
                await client.Assets.DeleteAsync(credential.ResourceGroup, credential.AccountName, resultName);
            }
            catch (Exception e)
            {
                error.Add(new Exception($"error deleting result asset {resource.Id} -- {e.Message}\r\n{e.InnerException.Message}"));
            }

            StreamingLocator locator = null;
            Job job = null;
            Asset source = null;
            Asset result = null;
            do
            {
                locator = await client.StreamingLocators.GetAsync(credential.ResourceGroup, credential.AccountName, locatorName);
                job = await client.Jobs.GetAsync(credential.ResourceGroup, credential.AccountName, TRANSFORM_NAME, jobName);
                source = await client.Assets.GetAsync(credential.ResourceGroup, credential.AccountName, sourceName);
                result = await client.Assets.GetAsync(credential.ResourceGroup, credential.AccountName, resultName);
            }
            while (locator != null || job != null || source != null || result != null);

            return error.Count > 0 ? error : null;
        }

        public async Task<Job> StartEncode(Models.Resource resource)
        {
            var client = await GetClient();
            var credential = _mediaCredential.Value;

            var contentAddress = new ContentAddress(resource.ContentAddress);
            var sourceName = $"raw-{contentAddress.ContainerName}";
            var resultName = $"result-{contentAddress.ContainerName}";
            var jobName = $"job-{contentAddress.ContainerName}";

            await client.Assets.DeleteAsync(credential.ResourceGroup, credential.AccountName, resultName);
            Asset resultAsset;
            do
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                resultAsset = client.Assets.Get(credential.ResourceGroup, credential.AccountName, resultName);
            }
            while (resultAsset != null);

            resultAsset = await client.Assets.CreateOrUpdateAsync(credential.ResourceGroup, credential.AccountName,
                resultName, new Asset(name: resultName, container: resultName));

            await client.Transforms.CreateOrUpdateAsync(
                credential.ResourceGroup,
                credential.AccountName,
                TRANSFORM_NAME,
                new List<TransformOutput>{
                    new TransformOutput(MediaTransform.Preset)
                });

            JobInput jobInput = new JobInputAsset(sourceName);
            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(resultName),
            };

            Job job = await client.Jobs.GetAsync(credential.ResourceGroup, credential.AccountName,
                TRANSFORM_NAME, jobName);

            if (job != null)
            {
                await client.Jobs.DeleteAsync(credential.ResourceGroup, credential.AccountName,
                    TRANSFORM_NAME, jobName);
            }

            do // poll job status
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                job = await client.Jobs.GetAsync(credential.ResourceGroup, credential.AccountName,
                TRANSFORM_NAME, jobName);
            }
            while (job != null);

            try
            {
                var tt = await client.Jobs.CreateWithHttpMessagesAsync(credential.ResourceGroup, credential.AccountName,
                TRANSFORM_NAME, jobName,
                parameters: new Job(input: jobInput, outputs: jobOutputs));
            }
            catch (ApiErrorException aee)
            {
                throw new Exception(aee.Message);
            }
            catch (Exception e)
            {
                throw e;
            }

            return job;
        }

        public async Task<Job> GetJob(Models.Resource resource)
        {
            var client = await GetClient();
            var credential = _mediaCredential.Value;

            var contentAddress = new ContentAddress(resource.ContentAddress);
            string jobName = $"job-{contentAddress.ContainerName}";

            var job = await client.Jobs.GetAsync(
                credential.ResourceGroup,
                credential.AccountName,
                TRANSFORM_NAME,
                jobName);

            return job;
        }

        public async Task<List<CloudBlockBlob>> GetContainerItems(string containerName)
        {
            var client = await GetClient();
            var credential = _mediaCredential.Value;

            var sas = await client.Assets.ListContainerSasAsync(credential.ResourceGroup, credential.AccountName, containerName, permissions: AssetContainerPermission.Read,
                expiryTime: DateTime.UtcNow.AddMinutes(5).ToUniversalTime());

            BlobResultSegment segment = await new CloudBlobContainer(new Uri(sas.AssetContainerSasUrls.FirstOrDefault()))
                .ListBlobsSegmentedAsync(null);

            List<CloudBlockBlob> blobs = new List<CloudBlockBlob>();

            foreach (IListBlobItem blobItem in segment.Results)
            {
                if (!(blobItem is CloudBlockBlob blob)) continue;
                blobs.Add(blob);
            }
            return blobs;
        }
    }
}
