# How to handle the human interaction in a function app
This sample code has showed how to get the user inputs and act based on that in durable function framework. The code is developed on .NET Core 3.1 and function v3 in Visual Studio 2019

## Installed Packages
Microsoft.NET.Sdk.Functions version 3 (3.0.5) and Microsoft.Azure.WebJobs.Extensions.DurableTask 2 (2.2.0)

## Code snippets
### Http trigger function
This is a http trigger function and the entry point for the application
```
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
```

### Orchestrator function to call the activity functions
In the orchestrator function, it waits for an external event (ApprovalEvent) and based on that perform the activity functions
```
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
                    outputs.Add(await context.CallActivityAsync<string>("ProcessApproval",
                      approvalEvent.Result));
                }
                else
                {
                    outputs.Add(await context.CallActivityAsync<string>("Escalate", "timeout"));
                }
            }
            return outputs;
        }
```
  
  ### Activity functions
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
  ```
