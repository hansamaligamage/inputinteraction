using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace human_interaction
{
    public static class request_approval
    {
        [FunctionName("request_approval_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
         [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
         [DurableClient] IDurableOrchestrationClient starter,
         ILogger log)
        {
            string instanceId = await starter.StartNewAsync("request_approval", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("request_approval")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            outputs.Add(await context.CallActivityAsync<string>("RequestApproval", "request1"));
            using(var timeouts = new CancellationTokenSource())
            {
                DateTime dueTime = context.CurrentUtcDateTime.AddMinutes(1);
                Task durableTimeout = context.CreateTimer(dueTime, timeouts.Token);

                Task<bool> approvalEvent = context.WaitForExternalEvent<bool>("ApprovalEvent");
                if (approvalEvent == await Task.WhenAny(approvalEvent, durableTimeout))
                {
                    timeouts.Cancel();
                    outputs.Add(await context.CallActivityAsync<string>("ProcessApproval", approvalEvent.Result));
                }
                else
                {
                    outputs.Add(await context.CallActivityAsync<string>("Escalate", "timeout"));
                }
            }
            return outputs;
        }

        [FunctionName("RequestApproval")]
        public static string RequestApproval([ActivityTrigger] string request, ILogger log)
        {
            log.LogInformation($"Request ID - {request}.");
            return $"Request ID - {request}.";
        }

        [FunctionName("ProcessApproval")]
        public static string ProcessApproval([ActivityTrigger] string result, ILogger log)
        {
            log.LogInformation($"Request result - {result}.");
            return $"Request result - {result}.";
        }

        [FunctionName("Escalate")]
        public static string Escalate([ActivityTrigger]string result, ILogger log)
        {
            log.LogInformation($"Request is escalated due to {result}");
            return $"Request is escalated due to {result}";
        }


    }
}