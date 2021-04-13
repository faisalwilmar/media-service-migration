using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MigrationMediaService.Classes
{
    public class ContentAddress
    {
        [JsonProperty("contentAddress")]
        private readonly string contentAddress;

        [JsonProperty("isBlob")]
        public readonly bool IsBlob;
        [JsonProperty("containerName")]
        public readonly string ContainerName;
        [JsonProperty("blobName")]
        public readonly string BlobName;

        [JsonConstructor()]
        public ContentAddress()
        { }

        public ContentAddress(string contentAddress)
        {
            if (contentAddress != "" && contentAddress != null && contentAddress?.Length > 0 && contentAddress?.Split().Length > 0)
            {
                var o = contentAddress.Split('/', 2);
                this.contentAddress = contentAddress;
                this.ContainerName = o.FirstOrDefault();

                if (this.IsBlob = (o.Length == 2))
                {
                    this.BlobName = o.LastOrDefault();
                }
            }
        }

        public ContentAddress(string containerName, string blobName) : this($"{containerName}/{blobName}")
        { }

        public override string ToString()
        {
            return contentAddress;
        }
    }
}
