using Microsoft.Azure.Documents;
using MigrationMediaService.Classes;
using Nexus.Base.CosmosDBRepository;
using System;
using System.Collections.Generic;
using System.Text;

namespace MigrationMediaService.Models
{
    public class ContextDTO : ModelBase
    {
        public Resource resource { get; set; }
        public ContentAddress source { get; set; }
        public ContentAddress target { get; set; }
    }
}
