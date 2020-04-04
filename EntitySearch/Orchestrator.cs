using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Entity
{
    public static class Orchestrator
    {
        private static RestSharp.RestClient client;

        [FunctionName("Orchestrator")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var outputs = new List<string>();

            Request requestData = context.GetInput<Request>();

            // Get Key Phrase from Azure Cognitive Services
            Model.KeyPhraseResponse keyPhraseResponse = new Model.KeyPhraseResponse(requestData.language);

            keyPhraseResponse.Text = await context.CallActivityAsync<string>("ExtractKeyPhrase", requestData);
            log.LogInformation("Completed ExtractKeyPhrase");

            // Add to DB
            await context.CallActivityAsync<string>("CosmosOutput", keyPhraseResponse.Text);
            log.LogInformation("Completed CosmosOutput");

            // Count DB
            var resultCountDb = await context.CallActivityAsync<string>("CosmosSearch", keyPhraseResponse.Text);
            log.LogInformation("Completed CosmosSearch");

            var resultTwitter = await context.CallActivityAsync<string>("GetTwitter", keyPhraseResponse.Text);

            string output = "{\"InternalHitCount\" : " + resultCountDb + ",  \"TwitterHitCount\" : " + resultTwitter + " }";


            return output;
        }

        [FunctionName("GetTwitter")]
        public static async Task<string> GetTwitter([ActivityTrigger] string request,
            ILogger log)
        {
            client = new RestSharp.RestClient("https://we-factsearch-fa.azurewebsites.net");

            RestSharp.RestRequest restRequest = new RestSharp.RestRequest("/api/SearchTwitter", RestSharp.Method.POST);

            restRequest.AddHeader("Content-Type", "application/json");
            Model.DocumentObject requestData = JsonConvert.DeserializeObject<Model.DocumentObject>(request);

            string query = string.Empty;
            foreach (var item in requestData.documents[0].keyPhrases)
            {
                query += item + " ";
            }

            Model.SearchQuery searchQuery = new Model.SearchQuery()
            {
                query = query
            };

            restRequest.AddJsonBody(searchQuery);

            var response = await client.ExecuteAsync(restRequest).ConfigureAwait(false);

            return response.Content;
        }

        [FunctionName("Orchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            object eventData = await req.Content.ReadAsAsync<Request>();
            string instanceId = await starter.StartNewAsync("Orchestrator", eventData);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}