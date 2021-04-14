using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using MigrationMediaService.Classes;
using MigrationMediaService.Models;
using MigrationMediaService.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static MigrationMediaService.Repository.Repositories;

namespace MigrationMediaService
{
    public class MigrationUpdaterMain
    {
        private readonly CosmosClient _cosmosClient;
        private readonly StorageHandler _storageHandler;
        private readonly MediaHandler _mediaHandler;
        private readonly HttpHandler _httpHandler;
        private readonly ResourceRepository repoResource;

        public MigrationUpdaterMain(CosmosClient cosmosClient, StorageHandler storageHandler, MediaHandler mediaHandler, HttpHandler httpHandler)
        {
            _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
            _storageHandler = storageHandler ?? throw new ArgumentNullException(nameof(storageHandler));
            _mediaHandler = mediaHandler ?? throw new ArgumentNullException(nameof(mediaHandler));
            _httpHandler = httpHandler ?? throw new ArgumentNullException(nameof(httpHandler));
            repoResource = new ResourceRepository(_cosmosClient, "MigrationMedia");
        }

        [FunctionName("CopyStorageFileTrigger")]
        public async Task CopyStorageFileTrigger([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            var copyStorageAPIName = "COPY_STORAGE_FILE_HTTP";
            var copyStorageAPIConn = Environment.GetEnvironmentVariable("CopyStorageFileAPIConn");

            await _httpHandler.GetAsync(copyStorageAPIName, copyStorageAPIConn);
        }

        [FunctionName("EncodeVideoTrigger")]
        public async Task CopyStorageVideoTrigger([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            var copyStorageVideoAPIName = "COPY_STORAGE_VIDEO_HTTP";
            var copyStorageVideoAPIConn = Environment.GetEnvironmentVariable("CopyStorageVideoAPIConn");

            await _httpHandler.GetAsync(copyStorageVideoAPIName, copyStorageVideoAPIConn);
        }

        [FunctionName("UpdateVideoStatusTrigger")]
        public async Task UpdateVideoStatus([TimerTrigger("*/30 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                var updateEncoderStatusApiConn = Environment.GetEnvironmentVariable("UpdateEncoderStatusAPIConn");
                if (string.IsNullOrEmpty(updateEncoderStatusApiConn)) throw new ArgumentException("config 'UpdateEncoderStatusAPIConn' cannot be empty");

                string query = "SELECT TOP 100 * FROM c WHERE c.type='Video' AND (c.status='migrating-encoding' OR c.status='pending')";
                var resourcesTemp = await repoResource.GetAsync(sqlQuery: query);

                var resourceList = new List<Resource>();
                foreach (var item in resourcesTemp.Items)
                {
                    resourceList.Add(item);
                }

                if (resourceList.Count == 0) return;

                var tasklist = new List<Task>();
                var increment = 0;
                foreach (var resource in resourceList)
                {
                    tasklist.Add(UpdateResourceVideoStatus(resource));
                    if (++increment % 10 == 0)
                    {
                        await Task.WhenAll(tasklist);
                        tasklist.Clear();
                    }
                }
                await Task.WhenAll(tasklist);
            }
            catch (ArgumentException ex)
            {
                log.LogWarning($"{ex.Message}\r\n{ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                log.LogError($"{ex.Message}\r\n{ex.InnerException?.Message}");
            }
        }

        public async Task UpdateResourceVideoStatus(Resource resource)
        {
            if (resource.Status == "migrating-encoding")
            {
                var updateEncoderStatusAPIName = "UPDATE_VIDEO_STATUS_HTTP";
                var uri = Environment.GetEnvironmentVariable("UpdateEncoderStatusAPIConn")?.Replace("{resourceId}", resource.Id);
                await _httpHandler.GetAsync(updateEncoderStatusAPIName, uri);
            }
            else if (resource.Status == "pending")
            {
                var blobs = await _mediaHandler.GetContainerItems($"raw-{resource.Id}");
                foreach (var blob in blobs)
                {
                    if (blob.Name.ToLower().Contains(resource.Filename))
                    {
                        resource.Status = "migrating-encoding";

                        //await UpdateOtherData(resource);
                        await repoResource.UpsertAsync(resource.Id, resource);
                        await _mediaHandler.StartEncode(resource);
                        return;
                    }
                }
            }
        }

        [FunctionName("UpdateBlobStatusTrigger")]
        public async Task UpdateBlobStatus([TimerTrigger("*/30 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                // check pending item
                string query = "SELECT TOP 100 * FROM c WHERE c.type='Article' AND c.status='pending'";
                var resourcesTemp = await repoResource.GetAsync(sqlQuery: query);

                var resourceList = new List<Resource>();
                foreach (var item in resourcesTemp.Items)
                {
                    resourceList.Add(item);
                }

                if (resourceList.Count == 0) return;

                var tasklist = new List<Task>();
                foreach (var resource in resourceList)
                {
                    tasklist.Add(UpdatePublishResource(resource));
                }
                await Task.WhenAll(tasklist);
            }
            catch (Exception ex)
            {
                log.LogError($"{ex.Message}\r\n{ex.InnerException?.Message}");
            }
        }

        public async Task UpdatePublishResource(Resource resource)
        {
            var ca = new ContentAddress(resource.ContentAddress);
            if (await _storageHandler.FileIsExists(ca.ContainerName, ca.BlobName))
            {
                resource.Status = "publish";
                resource.StatusDescription = null;

                //await UpdateOtherData(resource);
                await repoResource.UpsertAsync(resource.Id, resource);
            }
        }
    }
}
