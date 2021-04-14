using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MigrationMediaService.Classes;
using MigrationMediaService.Classes.Exceptions;
using MigrationMediaService.Models;
using MigrationMediaService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static MigrationMediaService.Repository.Repositories;

namespace MigrationMediaService
{
    public class CopyStorageFile
    {
        public static readonly string MAIN_FOLDER = "general_course_outline";

        private readonly CosmosClient _cosmosClient;
        private readonly StorageHandler _storageHandler;
        private readonly ResourceRepository repoResource;

        public CopyStorageFile(CosmosClient cosmosClient, StorageHandler storageHandler)
        {
            _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
            _storageHandler = storageHandler ?? throw new ArgumentNullException(nameof(storageHandler));
            repoResource = new ResourceRepository(_cosmosClient, "MigrationMedia");
        }

        [FunctionName("CopyStorageFile_HttpStart")]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            int processingDataMax = 10;
            int.TryParse(Environment.GetEnvironmentVariable("ProcessingDataMax"), out processingDataMax);
            var sourceContainerName = Environment.GetEnvironmentVariable("SourceContainerName");
            var targetContainerName = Environment.GetEnvironmentVariable("TargetContainerName");

            if (string.IsNullOrWhiteSpace(sourceContainerName) || string.IsNullOrWhiteSpace(targetContainerName))
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

            Dictionary<string, object> inputs = new Dictionary<string, object>();
            inputs.Add("processingDataMax", processingDataMax);
            inputs.Add("sourceContainerName", sourceContainerName);
            inputs.Add("targetContainerName", targetContainerName);

            string instanceId = await client.StartNewAsync("CopyStorageFile_Orchestrator", null, inputs);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            Console.WriteLine("Test123");
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("CopyStorageFile_Orchestrator")]
        public async Task<string> CopyStorageOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            Dictionary<string, object> inputs = context.GetInput<Dictionary<string, object>>();
            int processingDataMax = 10;
            if (inputs["processingDataMax"] != null) int.TryParse(inputs["processingDataMax"].ToString(), out processingDataMax);
            string sourceContainerName = (string)inputs["sourceContainerName"];
            string targetContainerName = (string)inputs["targetContainerName"];

            try
            {
                var resources = await context.CallActivityAsync<List<Resource>>("CopyStorageFile_GetResourcesActivity", processingDataMax);

                List<Task> tasklist = new List<Task>();
                foreach (var resource in resources)
                {
                    ContextDTO contextDTO = new ContextDTO();

                    //if (!string.IsNullOrEmpty(resource.Path) && !string.IsNullOrEmpty(resource.Location) && !string.IsNullOrEmpty(resource.Filename))
                    //{
                        var resPath = (resource.Path != null) ? $"{resource.Path.Trim('/')}/" : null;
                        var resLocation = (resource.Location != null) ? $"{resource.Location.Trim('/')}/" : null;
                        var addr = $"{resPath}{resLocation}{resource.Filename.Trim('/')}";
                        // var addr = $"{resource.Path.Trim('/')}/{resource.Location.Trim('/')}/{resource.Filename.Trim('/')}";
                        // var blobName = addr.Substring(addr.IndexOf(MAIN_FOLDER));
                        var blobName = addr;

                        resource.ContentAddress = $"{targetContainerName}/{blobName}";
                        contextDTO.source = new ContentAddress(sourceContainerName, blobName);
                        contextDTO.target = new ContentAddress(targetContainerName, blobName);
                    //}

                    contextDTO.resource = resource;
                    tasklist.Add(context.CallActivityAsync("CopyStorageFile_RunnerActivity", contextDTO));
                }
                await Task.WhenAll(tasklist.ToArray());
            }
            catch (Exception e)
            {
                log.LogError($"{e.Message}\r\n{e.InnerException.Message}");
            }

            return context.InstanceId;
        }

        [FunctionName("CopyStorageFile_GetResourcesActivity")]
        public async Task<List<Resource>> GetResourcesActivity(
            [ActivityTrigger] int processingDataMax)
        {
            string qDef = $"SELECT TOP {processingDataMax} * FROM c WHERE c.type='Article' AND (c.status=null OR c.status='')";
            var resources = await repoResource.GetAsync(sqlQuery: qDef);

            var resourceList = new List<Resource>();
            foreach (var item in resources.Items)
            {
                resourceList.Add(item);
            }

            return resourceList;
        }

        [FunctionName("CopyStorageFile_RunnerActivity")]
        public async Task RunnerActivity(
            [ActivityTrigger] ContextDTO contextDTO,
            ILogger log)
        {
            Resource resource = contextDTO.resource;
            ContentAddress source = contextDTO.source;
            ContentAddress target = contextDTO.target;

            try
            {
                string q1 = $"SELECT * FROM c WHERE c.id='{resource.Id}'";
                var resTemp = await repoResource.GetAsync(sqlQuery: q1);
                var res = resTemp.Items.FirstOrDefault();

                if (!string.IsNullOrEmpty(res.Status)) return;

                resource.Status = "processing";
                await repoResource.UpsertAsync(resource.Id, resource);

                if (source != null && target != null)
                {
                    await _storageHandler.CopyData(source.ContainerName, source.BlobName, target.ContainerName, target.BlobName);
                }

                resource.Status = "publish";
                resource.ProcessedDate = DateTime.UtcNow;

                //await UpdateOtherData(resource, log);
                await repoResource.UpsertAsync(resource.Id, resource);
            }
            catch (PendingException ex)
            {
                resource.Status = "pending";
                resource.StatusDescription = $"{ex.Message}\r\n{ex.InnerException?.Message}";
                resource.ProcessedDate = DateTime.UtcNow;

                //await UpdateOtherData(resource, log);
                await repoResource.UpsertAsync(resource.Id, resource);
            }
            catch (Exception ex)
            {
                resource.Status = "error";
                resource.StatusDescription = $"{ex.Message}\r\n{ex.InnerException?.Message}";
                resource.ProcessedDate = DateTime.UtcNow;
                resource.ContentAddress = null;

                //await UpdateOtherData(resource, log);
                await repoResource.UpsertAsync(resource.Id, resource);

                throw ex;
            }
        }
    }
}
