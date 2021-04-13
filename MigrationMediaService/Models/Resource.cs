using Newtonsoft.Json;
using Nexus.Base.CosmosDBRepository;
using System;
using System.Collections.Generic;
using System.Text;

namespace MigrationMediaService.Models
{
    public class Resource : ModelBase, ICloneable
    {
        [JsonProperty("subject")]
        public string Subject { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("duration")]
        public string Duration { get; set; }


        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("fileName")]
        public string Filename { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("isAvailable")]
        public string IsAvailable { get; set; }

        [JsonProperty("academicCareer")]
        public string AcademicCareer { get; set; }

        [JsonProperty("institution")]
        public string Institution { get; set; }

        [JsonProperty("binusmayaInstitution")]
        public string BinusmayaInstitution { get; set; }

        [JsonProperty("binusmayaDescription")]
        public string BinusmayaDescription { get; set; }

        [JsonProperty("crseId")]
        public string CrseId { get; set; }

        [JsonProperty("courseId")]
        public string CourseId { get; set; }

        [JsonProperty("revision")]
        public string Revision { get; set; }



        [JsonProperty("contentId")]
        public string ContentId { get; set; }

        [JsonProperty("contentAddress")]
        public string ContentAddress { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("statusDescription")]
        public string StatusDescription { get; set; }


        [JsonProperty("createdBy")]
        public string CreatedBy { get; set; }

        [JsonProperty("createdDate")]
        public DateTime? CreatedDate { get; set; }

        [JsonProperty("createdDateUtc")]
        public DateTime? CreatedDateUtc { get; set; }


        [JsonProperty("modifiedBy")]
        public string ModifiedBy { get; set; }

        [JsonProperty("modifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        [JsonProperty("modifiedDateUtc")]
        public DateTime? ModifiedDateUtc { get; set; }




        // untuk migrasi
        [JsonProperty("processedDate")]
        public DateTime? ProcessedDate { get; set; }

        // untuk migrasi
        [JsonProperty("processId")]
        public string ProcessId { get; set; }
        public object Clone()
        {
            throw new NotImplementedException();
        }
    }
}
