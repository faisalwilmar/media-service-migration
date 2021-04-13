using Microsoft.Azure.Cosmos;
using MigrationMediaService.Models;
using Nexus.Base.CosmosDBRepository;
using System;
using System.Collections.Generic;
using System.Text;

namespace MigrationMediaService.Repository
{
    public class Repositories
    {
        private static readonly string C_CosmosDBEndpoint = Environment.GetEnvironmentVariable("CosmosDBEndPoint");
        private static readonly string C_CosmosDBKey = Environment.GetEnvironmentVariable("CosmosDBKey");

        public class ResourceRepository : DocumentDBRepository<Resource>
        {
            //Cara 1 define repository
            public ResourceRepository(CosmosClient client, string database) :
                base(databaseId: database, client, createDatabaseIfNotExist: false)
            { }
        }
    }
}
