using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace MigrationMediaService.Classes
{
    class MediaCredential
    {
        [JsonProperty("aadClientId")]
        public string AadClientId { get; set; }

        [JsonProperty("aadSecret")]
        public string AadSecret { get; set; }

        [JsonProperty("aadTenantId")]
        public string AadTenantId { get; set; }

        [JsonProperty("accountName")]
        public string AccountName { get; set; }

        [JsonProperty("aadEndpoint")]
        public Uri AadEndpoint { get; set; }

        [JsonProperty("armAadAudience")]
        public Uri ArmAadAudience { get; set; }

        [JsonProperty("armEndpoint")]
        public Uri ArmEndpoint { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; }

        [JsonProperty("resourceGroup")]
        public string ResourceGroup { get; set; }

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

    }
}
