using Azure.AI.OpenAI;
using Azure;
using Azure.Search.Documents;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;

namespace chat
{
    internal class Readretrieveread : Approach
    {
        private SearchClient _searchClient;
        private string _gptDeployment;
        private string _sourcePage;
        private string _contentField;
        private string _followupQuestions;

        private string _promptPrefix;
        private string _queryPromptTemplate;
        public Readretrieveread(SearchClient searchClient, string gptDeployment, string sourcePage, string contentField) { 
            this._searchClient = searchClient;
            this._gptDeployment = gptDeployment;
            this._sourcePage = sourcePage; 
            this._contentField = contentField;

            this._promptPrefix = @"<|im_start|>system
                Assistant helps answer questions within a Porsche marketing document.Be brief in your answers.
                Answer ONLY with the facts listed in the list of sources below.If there isn't enough information below, say you don't know. Do not generate answers that don't use the sources below. If asking a clarifying question to the user would help, ask the question. 
                Each source has a name followed by colon and the actual information, always include the source name for each fact you use in the response. Use square brakets to reference the source, e.g. [info1.txt]. Don't combine sources, list each source separately, e.g. [info1.txt][info2.pdf].
                {follow_up_questions_prompt}
                {injected_prompt}
                    Sources:
                {sources}
                <| im_end |>
                {chat_history}
                ";

            this._followupQuestions = @"Generate three very brief follow-up questions that the user would likely ask next about their healthcare plan and employee handbook. 
                Use double angle brackets to reference the questions, e.g. << Are there exclusions for prescriptions ?>>.
                Try not to repeat questions that have already been asked.
                Only generate questions and do not generate any text before or after the questions, such as 'Next Questions'";

            this._queryPromptTemplate = @"Below is a history of the conversation so far, and a new question asked by the user that needs to be answered by searching in a knowledge base about employee healthcare plans and the employee handbook.
                    Generate a search query based on the conversation and the new question.
                    Do not include cited source filenames and document names e.g info.txt or doc.pdf in the search query terms.
                    Do not include any text inside[] or <<>> in the search query terms.
                    If the question is not in English, translate the question to English before generating the search query.

                    Chat History:
                    {chat_history}

                    Question:
                        {question}

                    Search query:";

        }

        public override async Task<string> run(dynamic history, dynamic overrides)
        {
            bool useSemanticCaptions = overrides["semantic_captions"];
            int top = overrides["top"];

            string prompt = getChatHistoryAsText(history, false, 1000);
            string q = await this.Completion(prompt, new string[] { "\n"});
            SearchOptions searchOptions = new SearchOptions();
            searchOptions.QueryType = Azure.Search.Documents.Models.SearchQueryType.Semantic;
            searchOptions.QueryLanguage = "en-us";
            searchOptions.QuerySpeller = "lexicon";
            searchOptions.SemanticConfigurationName = "default";
            searchOptions.Size = 3;
            //searchOptions.QueryCaption = "extractive|highlight-false";
            Response<SearchResults<dynamic>> searchResults = this._searchClient.Search<dynamic>(q, searchOptions);

            string content = "";
            List<string> searchResultsList = new List<string>();
            foreach (SearchResult<dynamic> result in searchResults.Value.GetResults())
            {
                var jsonData = JsonConvert.DeserializeObject(Convert.ToString(result.Document));
                content += "\n"  +jsonData.filename + " : " + jsonData.aggregatedResults.text;
                searchResultsList.Add(Convert.ToString(jsonData.filename) + " : " + Convert.ToString(jsonData.aggregatedResults.text));
            }

            string prompt2 = this._promptPrefix.Replace("{follow_up_questions_prompt}", this._followupQuestions).Replace("{injected_prompt}", "").Replace("{sources}", content).Replace("{chat_history}", this.getChatHistoryAsText(history, false, 1000));

            string answer = await this.Completion(prompt2, new string[] { "<|im_end|>", "<|im_start|>" });
            //if (overrides["semantic_ranker"])
            //{
            //    var r = this._searchClient.Search()
            //}
            Console.WriteLine(prompt2);
            var output = new { data_points = searchResultsList.ToArray(), answer = answer, thoughts = "Searched for:<br>"+q+ "<br><br>Prompt:<br>"+prompt.Replace("\n", "<br>") };
            return JsonConvert.SerializeObject(output);
        }

        private string getChatHistoryAsText(dynamic history, bool includeLastTurn, int approxMaxTokens)
        {   
            List<dynamic> myList = new List<dynamic>();
            
            foreach (var item in history)
            {
                myList.Add(item);
            }
            Array.Reverse(myList.ToArray());

            int historyLength = myList.Count;
            string historyText = "";
            for (int i = historyLength - 1; i > -1; i--)
            {
                dynamic h = myList[i];
                historyText += "<|im_start|>user" + "\n" + h["user"] + "\n" + "<|im_end|>" + "\n" + "<|im_start|>assistant" + "\n" + botResponse(h) + "\n" + historyText;
                if (historyText.Length > approxMaxTokens * 4)
                {
                    break;
                }
            }

            return historyText;
        }

        
        private async Task<string> Completion(string prompt, string[] stops)
        {
            using (var httpClient = new HttpClient())
            {
                string openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                string openAiEndpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
                var contentType = new MediaTypeWithQualityHeaderValue("application/json");
                var baseAddress = openAiEndpoint;
                var api = "/openai/deployments/"+this._gptDeployment+"/completions?api-version=2022-12-01";
                httpClient.BaseAddress = new Uri(baseAddress);
                httpClient.DefaultRequestHeaders.Accept.Add(contentType);
                httpClient.DefaultRequestHeaders.Add("api-key", openAiKey);

                var data = new Dictionary<string, dynamic>
                {
                    {"prompt",prompt},
                    {"max_tokens",32},
                    {"temperature",0.0},
                    {"stop",stops},
                };

                var jsonData = JsonConvert.SerializeObject(data);
                var contentData = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(api, contentData);

                if (response.IsSuccessStatusCode)
                {
                    var stringData = await response.Content.ReadAsStringAsync();
                    dynamic results = JsonConvert.DeserializeObject<dynamic>(stringData);
                    return results.choices[0].text;
                }
            }
            return null;
        }

        private string botResponse(dynamic h)
        {
            if (Object.ReferenceEquals(null, h["bot"]))
            {
                return "";
            }
            else
            {
                return h["bot"];
            }
        }

    }
}
