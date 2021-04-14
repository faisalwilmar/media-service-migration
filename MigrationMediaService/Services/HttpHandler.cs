using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MigrationMediaService.Services
{
    public class HttpHandler
    {
        private Dictionary<string, HttpClient> _clients = new Dictionary<string, HttpClient>();

        public async Task GetAsync(string httpClientName, string requestUri)
        {
            HttpClient client;
            if(_clients.ContainsKey(httpClientName))
            {
                client = _clients[httpClientName];
            } else
            {
                _clients.Add(httpClientName, new HttpClient());
                client = _clients[httpClientName];
            }
            await client.GetAsync(requestUri);
        }

    }
}
