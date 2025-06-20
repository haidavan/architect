using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using System.Text;
using System.Text.Json;

namespace UniversityApi.Services
{
    public interface IElasticsearchService
    {
        Task<List<int>> SearchLectures(string term);
    }


    public class ElasticsearchService : IElasticsearchService
    {
        private readonly ElasticsearchClient _client;
        private readonly ILogger<ElasticsearchService> _logger;

        public ElasticsearchService(IConfiguration config, ILogger<ElasticsearchService> logger)
        {
            var host = config["Elasticsearch:Host"] ?? "localhost";
            var port = config.GetValue<int>("Elasticsearch:Port", 9200);
            var user = config["Elasticsearch:User"];
            var password = config["Elasticsearch:Password"];

            var settings = new ElasticsearchClientSettings(new Uri($"http://{host}:{port}"))
                .Authentication(new BasicAuthentication(user, password))
                .ServerCertificateValidationCallback((sender, cert, chain, errors) => true)
                .DefaultIndex("lecture_materials")
                .EnableDebugMode()
                .DisableDirectStreaming()
                //.EnableApiVersioningHeader(false)
                .OnRequestCompleted(apiCallDetails =>
                {
                    if (apiCallDetails.RequestBodyInBytes != null)
                    {
                        logger.LogDebug($"Request: {Encoding.UTF8.GetString(apiCallDetails.RequestBodyInBytes)}");
                    }
                    if (apiCallDetails.ResponseBodyInBytes != null)
                    {
                        logger.LogDebug($"Response: {Encoding.UTF8.GetString(apiCallDetails.ResponseBodyInBytes)}");
                    }
                });

            _client = new ElasticsearchClient(settings);
            _logger = logger;
        }

        public async Task<List<int>> SearchLectures(string term)
        {
            try
            {
                _logger.LogInformation($"Searching lectures for term: '{term}'");
                var response = await _client.SearchAsync<JsonElement>(s => s
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                sh => sh.Match(m => m
                                    .Field("lectureName")
                                    .Query(term)
                                    .Boost(3.0f)
                                    .Analyzer("russian")
                                    .Fuzziness(new Fuzziness(2))
                                ),
                                sh => sh.Match(m => m
                                    .Field("courseName")
                                    .Query(term)
                                    .Boost(2.0f)
                                    .Analyzer("standard")
                                    .Fuzziness(new Fuzziness(2))
                                ),
                                sh => sh.Match(m => m
                                    .Field("content")
                                    .Query(term)
                                    .Analyzer("standard")
                                    .Fuzziness(new Fuzziness(2))
                                ),
                                sh => sh.Match(m => m
                                    .Field("keywords")
                                    .Query(term)
                                    .Analyzer("standard")
                                    .Fuzziness(new Fuzziness(2))
                                )
                            )
                        )
                    )
                    .Size(1000)
                );

                if (!response.IsValidResponse)
                {
                    _logger.LogError($"Invalid response: {response.DebugInformation}");
                    if (response.TryGetOriginalException(out var ex))
                    {
                        _logger.LogError(ex, "Elasticsearch exception");
                    }
                    return new List<int>();
                }

                _logger.LogInformation($"Found {response.Total} documents for term '{term}'");
                var lectureIds = new List<int>();

                foreach (var hit in response.Hits)
                {
                    try
                    {
                        // Access properties through JsonElement API
                        if (hit.Source.ValueKind == JsonValueKind.Object &&
                            hit.Source.TryGetProperty("lectureId", out var idElement))
                        {
                            if (idElement.ValueKind == JsonValueKind.Number)
                            {
                                if (idElement.TryGetInt32(out int id))
                                {
                                    lectureIds.Add(id);
                                }
                                else if (idElement.TryGetInt64(out long longId))
                                {
                                    lectureIds.Add((int)longId);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("lectureId property not found in document");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error accessing lectureId");
                    }
                }

                return lectureIds.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Elasticsearch search failed for term: {term}");
                return new List<int>();
            }
        }
    }
}
