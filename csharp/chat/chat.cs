using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure;

namespace chat
{
    public static class Chat
    {
        [FunctionName("chat")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            var history = data?.history;
            var approach = data?.approach;
            var overrides = data?.overrides;

            SearchClient client = getSearchClient();

            Readretrieveread rrr = new Readretrieveread(client, "text-davinci-002","filename","something");

            //string responseMessage = "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.";
            string result = await rrr.run(history, overrides);

            return new OkObjectResult(JsonConvert.DeserializeObject(result));
        }

        private static SearchClient getSearchClient()
        {
            string serviceName = Environment.GetEnvironmentVariable("COGSEARCH_SERVICE");
            string apiKey = Environment.GetEnvironmentVariable("COGSEARCH_API_KEY");
            string indexName = Environment.GetEnvironmentVariable("COGSEARCH_INDEX");

            // Create a SearchIndexClient to send create/delete index commands
            Uri serviceEndpoint = new Uri($"https://{serviceName}.search.windows.net/");
            AzureKeyCredential credential = new AzureKeyCredential(apiKey);

            // Create a SearchClient to load and query documents
            SearchClient srchclient = new SearchClient(serviceEndpoint, indexName, credential);
            return srchclient;
        }
    }
}
