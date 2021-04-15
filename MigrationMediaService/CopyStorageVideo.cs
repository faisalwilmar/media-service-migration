using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using JobState = Microsoft.Azure.Management.Media.Models.JobState;
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
using System.Threading.Tasks;
using static MigrationMediaService.Repository.Repositories;
using System.IO;
using System.Xml;
using MigrationMediaService.Classes.MediaV3;
using Newtonsoft.Json;

namespace MigrationMediaService
{
    public class CopyStorageVideo
    {
        public static readonly string MAIN_FOLDER = "general_course_outline";

        private readonly CosmosClient _cosmosClient;
        private readonly StorageHandler _storageHandler;
        private readonly MediaHandler _mediaHandler;
        private readonly ResourceRepository repoResource;

        public CopyStorageVideo(CosmosClient cosmosClient, StorageHandler storageHandler, MediaHandler mediaHandler)
        {
            _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
            _storageHandler = storageHandler ?? throw new ArgumentNullException(nameof(storageHandler));
            _mediaHandler = mediaHandler ?? throw new ArgumentNullException(nameof(mediaHandler));
            repoResource = new ResourceRepository(_cosmosClient, "MigrationMedia");
        }

        [FunctionName("CopyStorageVideo_HttpStart")]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            int processingDataMax = 10;
            int.TryParse(Environment.GetEnvironmentVariable("ProcessingDataMax"), out processingDataMax);
            var sourceContainerName = Environment.GetEnvironmentVariable("SourceContainerName");

            Dictionary<string, object> inputs = new Dictionary<string, object>();
            inputs.Add("processingDataMax", processingDataMax);
            inputs.Add("sourceContainerName", sourceContainerName);

            await _mediaHandler.InitiateClient();

            string instanceId = await client.StartNewAsync("CopyStorageVideo_Orchestrator", null, inputs);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("CopyStorageVideo_Orchestrator")]
        public async Task<string> Orchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            Dictionary<string, object> inputs = context.GetInput<Dictionary<string, object>>();
            int processingDataMax = 10;
            if (inputs["processingDataMax"] != null) int.TryParse(inputs["processingDataMax"].ToString(), out processingDataMax);
            string sourceContainerName = (string)inputs["sourceContainerName"];

            try
            {
                var resources = await context.CallActivityAsync<List<Resource>>("CopyStorageVideo_GetResourcesActivity", processingDataMax);

                List<Task> tasklist = new List<Task>();
                foreach (var resource in resources)
                {
                    ContextDTO contextDTO = new ContextDTO();
                    var resPath = (resource.Path != null) ? $"{resource.Path.Trim('/')}/" : null;
                    var resLocation = (resource.Location != null) ? $"{resource.Location.Trim('/')}/" : null;
                    var addr = $"{resPath}{resLocation}{resource.Filename.Trim('/')}";
                    //var addr = $"{resource.Path.Trim('/')}/{resource.Location.Trim('/')}/{resource.Filename.Trim('/')}";
                    //var blobName = addr.Substring(addr.IndexOf(MAIN_FOLDER)); //klo pathnya panjang dan ada MAIN_FOLDER, startnya dari MAIN_FOLDER aja, depannya dibuang
                    var blobName = addr;

                    resource.ContentAddress = resource.Id;

                    contextDTO.resource = resource;
                    contextDTO.source = new ContentAddress(sourceContainerName, blobName);
                    contextDTO.target = new ContentAddress($"raw-{resource.Id}", resource.Filename);
                    tasklist.Add(context.CallActivityAsync("CopyStorageVideo_RunnerActivity", contextDTO));
                }
                await Task.WhenAll(tasklist.ToArray());
            }
            catch (Exception e)
            {
                log.LogError($"{e.Message}\r\n{e.InnerException.Message}");
            }

            return context.InstanceId;
        }

        [FunctionName("CopyStorageVideo_GetResourcesActivity")]
        public async Task<List<Resource>> GetResourcesActivity(
            [ActivityTrigger] int processingDataMax)
        {
            string qDef = $"SELECT TOP {processingDataMax} * FROM c WHERE c.type='Video' AND (c.status=null OR c.status='') AND c.fileName != null";
            
            var resources = await repoResource.GetAsync(sqlQuery: qDef);
            
            var resourceList = new List<Resource>();
            foreach (var item in resources.Items)
            {
                resourceList.Add(item);
            }

            return resourceList;
        }

        [FunctionName("CopyStorageVideo_RunnerActivity")]
        public async Task RunnerActivity(
            [ActivityTrigger] ContextDTO contextDTO,
            ILogger log)
        {
            Resource resource = contextDTO.resource;
            ContentAddress source = contextDTO.source;

            try
            {
                string q1 = $"SELECT * FROM c WHERE c.id='{resource.Id}'";
                var resTemp = await repoResource.GetAsync(sqlQuery: q1);
                var res = resTemp.Items.FirstOrDefault();

                if (!string.IsNullOrEmpty(res.Status)) return;

                resource.Status = "migrating";
                await repoResource.UpsertAsync(resource.Id, resource);

                var sourceEndpoint = Environment.GetEnvironmentVariable("SourceClientEndpoint");
                if (string.IsNullOrWhiteSpace(sourceEndpoint)
                || string.IsNullOrWhiteSpace(source.ContainerName)
                || string.IsNullOrWhiteSpace(source.BlobName))
                {
                    throw new Exception($"SourceEndpoint, SourceContainerName, SourceFilename " +
                        $"are required, any one of them are missing");
                }

                var sourceUri = new Uri($"{sourceEndpoint.Trim('/')}/{source.ContainerName}/{source.BlobName}");
                await _mediaHandler.SaveVideo(resource, sourceUri);

                // nama menggunakan prefix "migrating-" agar tidak bentrok dengan reguler 
                //  encoding saat user upload video dari device
                resource.Status = "migrating-encoding";

                //await UpdateOtherData(resource, log);

                await repoResource.UpsertAsync(resource.Id, resource);

                await _mediaHandler.StartEncode(resource);
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

                throw;
            }
        }

        [FunctionName("CopyStorageVideoPollStatus")]
        public async Task<IActionResult> PollEncodeStatus(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Encode/Status/{resourceId}")] HttpRequest req,
            string resourceId,
            ILogger log)
        {
            string qDef = $"SELECT * FROM c WHERE c.id='{resourceId}' and c.type='Video'";
            var resTemp = await repoResource.GetAsync(sqlQuery: qDef);
            var resource = resTemp.Items.FirstOrDefault();

            try
            {
                if (resource == null) throw new ArgumentException("resource not found");
                if (resource.Status != "migrating-encoding") return new OkObjectResult(resource.Status);

                var job = await _mediaHandler.GetJob(resource);
                if (job == null) return new OkObjectResult("no job");

                if (JobState.Finished == job.State)
                {
                    var manifest = new Manifest();
                    var tasklist = new List<Task>();

                    var blobs = await _mediaHandler.GetContainerItems($"result-{resource.Id}");
                    foreach (var blob in blobs)
                    {
                        var name = blob.Name.ToLower();
                        if (name.Contains("_manifest.json"))
                        {
                            manifest = JsonConvert.DeserializeObject<Manifest>(await blob.DownloadTextAsync());
                        }

                        if (name.Contains("jpg"))
                        {
                            tasklist.Add(Task.Run(async () =>
                            {
                                MemoryStream _stream = new MemoryStream();
                                await blob.DownloadToStreamAsync(_stream);
                                _stream.Position = 0;
                                await _storageHandler.SaveData("thumbnails", $"resource/thumbnail/{resource.Id}.jpg", _stream);
                            }));
                        }
                    }
                    await Task.WhenAll(tasklist);

                    var duration = XmlConvert.ToTimeSpan(manifest.AssetFile.FirstOrDefault().Duration);

                    resource.Status = "publish";
                    resource.Duration = ((int)Math.Round(duration.TotalSeconds)).ToString();
                }
                else if (JobState.Error == job.State)
                {
                    resource.Status = "error";
                    resource.StatusDescription = job.Description;
                }
                else if (JobState.Canceled == job.State)
                {
                    resource.Status = "canceled";
                    resource.StatusDescription = job.Description;
                }

                //await UpdateOtherData(resource, log);

                await repoResource.UpsertAsync(resource.Id, resource);
                return new OkObjectResult(resource.Status);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            catch (Exception ex)
            {
                resource.Status = "error";
                resource.StatusDescription = $"{ex.Message}\r\n{ex.InnerException?.Message}";
                resource.ProcessedDate = DateTime.UtcNow;

                //await UpdateOtherData(resource, log);

                await repoResource.UpsertAsync(resource.Id, resource);

                throw ex;
            }

        }
    }
}
