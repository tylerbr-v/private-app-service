using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using DotNetEnv;


namespace Microsoft.BotBuilderSamples.Bots
{
    public class ChatHistoryItem
    {
        public ChatInput inputs { get; set; }
        public ChatOutput outputs { get; set; }
    }

    public class ChatInput
    {
        public string question { get; set; }
    }

    public class ChatOutput
    {
        public string answer { get; set; }
    }

    public class ApiResponse
    {
        public string Answer { get; set; }
    }

    public class BotStateService
    {
        public ConversationState ConversationState { get; }
        public IStatePropertyAccessor<List<ChatHistoryItem>> ChatHistoryAccessor { get; private set; }

        public BotStateService(ConversationState conversationState)
        {
            ConversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            InitializeAccessors();
        }

        private void InitializeAccessors()
        {
            // Initialize state property accessors
            ChatHistoryAccessor = ConversationState.CreateProperty<List<ChatHistoryItem>>("ChatHistory");
        }
    }
    public class HerbalifePOC : ActivityHandler
    {
        private readonly HttpClient _httpClient;
        private readonly BotStateService _botStateService;

        public HerbalifePOC(HttpClient httpClient, BotStateService botStateService)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _botStateService = botStateService ?? throw new ArgumentNullException(nameof(botStateService));
        }

        
        
        

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {

            var handler = new HttpClientHandler()
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback =
                        (httpRequestMessage, cert, cetChain, policyErrors) => { return true; }
            };
            using (var client = new HttpClient(handler))
            {
                // Request data goes here
                // The example below assumes JSON formatting which may be updated
                // depending on the format your endpoint expects.
                // More information can be found here:
                // https://docs.microsoft.com/azure/machine-learning/how-to-deploy-advanced-entry-script

                var chatHistory = await _botStateService.ChatHistoryAccessor.GetAsync(turnContext, () => new List<ChatHistoryItem>(), cancellationToken);
                string userQuestion = turnContext.Activity.Text;

                // Create an object with both the current question and the chat history
                var data = new
                {
                    question = userQuestion,
                    chat_history = chatHistory,
                    username = "Azure Bot"
                };

                // Serialize the object to JSON
                var requestBody = JsonConvert.SerializeObject(data);
                
                //Load .env file for local development
                Env.Load();

                // Load Environment variables to connect to correct endpoint
                // string apiKey = Environment.GetEnvironmentVariable("API_KEY");
                // string modelName = Environment.GetEnvironmentVariable("MODEL_NAME");
                string baseAddress = Environment.GetEnvironmentVariable("BASE_ADDRESS");

                //// Replace this with the primary/secondary key, AMLToken, or Microsoft Entra ID token for the endpoint
                // if (string.IsNullOrEmpty(apiKey))
                // {
                //     throw new Exception("A key should be provided to invoke the endpoint");
                // }
                // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", apiKey);

                //// This header will force the request to go to a specific deployment.
                //// Remove this line to have the request observe the endpoint traffic rules
                // content.Headers.Add("azureml-model-deployment", modelName);

                client.BaseAddress = new Uri(baseAddress);

                var content = new StringContent(requestBody);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");



                // Read the content as a string asynchronously
                string contentAsString = await content.ReadAsStringAsync();

                // Print the content to the console
                Console.WriteLine(contentAsString);

                // WARNING: The 'await' statement below can result in a deadlock
                // if you are calling this code from the UI thread of an ASP.Net application.
                // One way to address this would be to call ConfigureAwait(false)
                // so that the execution does not attempt to resume on the original context.
                // For instance, replace code such as:
                //      result = await DoSomeTask()
                // with the following:
                //      result = await DoSomeTask().ConfigureAwait(false)
                HttpResponseMessage response = await client.PostAsync("", content);



                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    // Append new message from this turn to the history
                    

                    Console.WriteLine("Result: {0}", result);
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(result);
                    string botAnswer = $"{apiResponse.Answer}";
                    chatHistory.Add(new ChatHistoryItem
                    {
                        inputs = new ChatInput { question = userQuestion },
                        outputs = new ChatOutput { answer = botAnswer }
                    });

                    // Sending API response back to the user
                    await turnContext.SendActivityAsync(MessageFactory.Text(apiResponse.Answer), cancellationToken);

                    // Save changes to conversation state
                    await _botStateService.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
                }
                else
                {
                    Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));

                    // Print the headers - they include the requert ID and the timestamp,
                    // which are useful for debugging the failure
                    Console.WriteLine(response.Headers.ToString());

                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseContent);
                }
            }

        }


        

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hi, I'm Doctor Luigi! I'm here to help you with any product related questions you might have.";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
