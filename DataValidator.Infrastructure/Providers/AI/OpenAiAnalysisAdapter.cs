using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataValidator.Infrastructure.Providers.AI
{
    public class OpenAiAnalysisAdapter : IAiAnalysisProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiAnalysisAdapter> _logger;
        private readonly AIModelsConfiguration _aiConfig;
        
        // Performance metrics
        private static long _totalRequests = 0;
        private static long _successfulRequests = 0;
        private static long _failedRequests = 0;
        private static readonly object _metricsLock = new object();

        public OpenAiAnalysisAdapter(HttpClient httpClient, ILogger<OpenAiAnalysisAdapter> logger, IOptions<AIModelsConfiguration> aiConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _aiConfig = aiConfig?.Value ?? throw new ArgumentNullException(nameof(aiConfig));
            
            // Configure timeout from configuration
            _httpClient.Timeout = TimeSpan.FromSeconds(_aiConfig.AnalysisModel.TimeoutSeconds);
        }

        public string ProviderName => "OpenAI";
        
        /// <summary>
        /// Records performance metrics for monitoring
        /// </summary>
        /// <param name="success">Whether the request was successful</param>
        /// <param name="responseTime">Response time in milliseconds</param>
        /// <param name="requestId">Request ID for logging</param>
        private void RecordMetrics(bool success, double responseTime, string requestId)
        {
            lock (_metricsLock)
            {
                _totalRequests++;
                if (success)
                {
                    _successfulRequests++;
                }
                else
                {
                    _failedRequests++;
                }
            }
            
            _logger.LogInformation("[{RequestId}] Performance metrics - Success: {Success}, ResponseTime: {ResponseTime}ms, Total: {Total}, Success: {SuccessCount}, Failed: {FailedCount}",
                requestId, success, responseTime, _totalRequests, _successfulRequests, _failedRequests);
        }
        
        /// <summary>
        /// Gets current performance metrics
        /// </summary>
        /// <returns>Performance metrics summary</returns>
        public (long Total, long Successful, long Failed, double SuccessRate) GetPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                var successRate = _totalRequests > 0 ? (double)_successfulRequests / _totalRequests * 100 : 0;
                return (_totalRequests, _successfulRequests, _failedRequests, successRate);
            }
        }
        
        /// <summary>
        /// Validates the AI service state before making requests
        /// </summary>
        /// <param name="requestId">Request ID for logging</param>
        /// <returns>True if the service is available, false otherwise</returns>
        private async Task<(bool IsAvailable, string ErrorMessage)> ValidateAiServiceStateAsync(string requestId)
        {
            var config = _aiConfig.AnalysisModel;
            
            // Basic configuration validation
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                _logger.LogError("[{RequestId}] AI service validation failed: API key is not configured", requestId);
                return (false, "OpenAI API key is not configured");
            }
            
            if (string.IsNullOrEmpty(config.BaseUrl))
            {
                _logger.LogError("[{RequestId}] AI service validation failed: Base URL is not configured", requestId);
                return (false, "OpenAI Base URL is not configured");
            }
            
            // Test connectivity with a simple health check
            try
            {
                _logger.LogDebug("[{RequestId}] Performing AI service health check", requestId);
                
                var healthCheckRequest = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl}/models");
                healthCheckRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
                
                // Use a shorter timeout for health check
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var healthResponse = await _httpClient.SendAsync(healthCheckRequest, cts.Token);
                
                if (healthResponse.IsSuccessStatusCode)
                {
                    _logger.LogDebug("[{RequestId}] AI service health check passed", requestId);
                    return (true, string.Empty);
                }
                else
                {
                    _logger.LogWarning("[{RequestId}] AI service health check failed with status: {StatusCode}", requestId, healthResponse.StatusCode);
                    return (false, $"AI service health check failed: {healthResponse.StatusCode}");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[{RequestId}] AI service health check timed out", requestId);
                return (false, "AI service health check timed out");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "[{RequestId}] AI service health check failed with network error: {Message}", requestId, ex.Message);
                return (false, $"AI service network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{RequestId}] AI service health check failed with unexpected error: {Message}", requestId, ex.Message);
                return (false, $"AI service unexpected error: {ex.Message}");
            }
        }

        public async Task<ValidationAnalysisResult> AnalyzeDataAsync(string prompt)
        {
            var startTime = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString("N")[..8]; // Short request ID for tracking
            
            _logger.LogInformation("[{RequestId}] Starting OpenAI analysis request at {StartTime}", requestId, startTime);
            
            // Get configuration from appsettings
            var config = _aiConfig.AnalysisModel;
            
            // Validate AI service state before attempting requests
            var (isAvailable, errorMessage) = await ValidateAiServiceStateAsync(requestId);
            if (!isAvailable)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError("[{RequestId}] AI service validation failed: {ErrorMessage}", requestId, errorMessage);
                
                // Record failed metrics for validation failure
                RecordMetrics(false, duration.TotalMilliseconds, requestId);
                
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    Provider = ProviderName
                };
            }

            // Implement retry logic
            for (int attempt = 1; attempt <= config.MaxRetryAttempts; attempt++)
            {
                try
                {
                    _logger.LogDebug("[{RequestId}] Attempt {Attempt}/{MaxAttempts}", requestId, attempt, config.MaxRetryAttempts);
                    
                    var result = await ExecuteAnalysisRequestAsync(prompt, config, requestId, startTime);
                    
                    if (result.Success)
                    {
                        var duration = DateTime.UtcNow - startTime;
                        _logger.LogInformation("[{RequestId}] Analysis completed successfully on attempt {Attempt} in {Duration}ms", 
                            requestId, attempt, duration.TotalMilliseconds);
                        
                        // Record successful metrics
                        RecordMetrics(true, duration.TotalMilliseconds, requestId);
                        return result;
                    }
                    
                    // If not successful and not the last attempt, log and continue to retry
                    if (attempt < config.MaxRetryAttempts)
                    {
                        _logger.LogWarning("[{RequestId}] Attempt {Attempt} failed: {Error}. Retrying in {Delay} seconds...", 
                            requestId, attempt, result.ErrorMessage, config.RetryDelaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(config.RetryDelaySeconds));
                    }
                    else
                    {
                        // Last attempt failed
                        var duration = DateTime.UtcNow - startTime;
                        _logger.LogError("[{RequestId}] All {MaxAttempts} attempts failed. Final error: {Error}. Total duration: {Duration}ms", 
                            requestId, config.MaxRetryAttempts, result.ErrorMessage, duration.TotalMilliseconds);
                        
                        // Record failed metrics
                        RecordMetrics(false, duration.TotalMilliseconds, requestId);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    var duration = DateTime.UtcNow - startTime;
                    
                    if (attempt < config.MaxRetryAttempts)
                    {
                        _logger.LogWarning(ex, "[{RequestId}] Attempt {Attempt} threw exception: {Message}. Retrying in {Delay} seconds...", 
                            requestId, attempt, ex.Message, config.RetryDelaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(config.RetryDelaySeconds));
                    }
                    else
                    {
                        _logger.LogError(ex, "[{RequestId}] All {MaxAttempts} attempts failed with exceptions. Total duration: {Duration}ms", 
                            requestId, config.MaxRetryAttempts, duration.TotalMilliseconds);
                        
                        // Record failed metrics
                        RecordMetrics(false, duration.TotalMilliseconds, requestId);
                        
                        return new ValidationAnalysisResult
                        {
                            Success = false,
                            ErrorMessage = $"All retry attempts failed. Last error: {ex.Message}",
                            Provider = ProviderName
                        };
                    }
                }
            }
            
            // This should never be reached, but just in case
            return new ValidationAnalysisResult
            {
                Success = false,
                ErrorMessage = "Unexpected error in retry logic",
                Provider = ProviderName
            };
        }
        
        private async Task<ValidationAnalysisResult> ExecuteAnalysisRequestAsync(string prompt, AIModelConfig config, string requestId, DateTime startTime)
        {
            try
            {
                
                // Validate configuration
                if (string.IsNullOrEmpty(config.ApiKey))
                {
                    _logger.LogError("[{RequestId}] OpenAI API key is not configured", requestId);
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        ErrorMessage = "OpenAI API key is not configured",
                        Provider = ProviderName
                    };
                }
                
                if (string.IsNullOrEmpty(config.BaseUrl))
                {
                    _logger.LogError("[{RequestId}] OpenAI Base URL is not configured", requestId);
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        ErrorMessage = "OpenAI Base URL is not configured",
                        Provider = ProviderName
                    };
                }
                
                _logger.LogInformation("[{RequestId}] Configuration validated. Model: {Model}, BaseUrl: {BaseUrl}, MaxTokens: {MaxTokens}, Temperature: {Temperature}", 
                    requestId, config.Model, config.BaseUrl, config.MaxTokens, config.Temperature);
                
                _logger.LogDebug("[{RequestId}] Prompt length: {PromptLength} characters", requestId, prompt?.Length ?? 0);
                
                var requestBody = new
                {
                    model = config.Model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = $"Eres un analista experto en validación de datos con amplia experiencia en procesamiento de documentos y verificación de datos. Fecha actual: {DateTime.Now:yyyy-MM-dd HH:mm:ss UTC}. \n\nTus responsabilidades:\n- Analizar los datos proporcionados con meticulosa atención al detalle\n- Identificar inconsistencias, errores o información faltante\n- Validar formatos de datos, rangos y lógica de negocio\n- Considerar el contexto temporal al evaluar información sensible a fechas\n- Proporcionar resultados de validación completos en el formato JSON especificado\n- Señalar cualquier anomalía o patrón sospechoso\n\nMantén siempre la objetividad y proporciona un razonamiento claro para tus decisiones de validación."
                        },
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    max_completion_tokens = config.MaxTokens,
                    temperature = config.Temperature,
                    response_format = new { type = "json_object" }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("[{RequestId}] Request body size: {RequestSize} bytes", requestId, json.Length);

                // Set authorization header with API key from configuration
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
                
                _logger.LogInformation("[{RequestId}] Sending request to OpenAI API. Endpoint: {Endpoint} Timeout: {Timeout} seconds", 
                    requestId, $"{config.BaseUrl}/chat/completions", config.TimeoutSeconds);
                
                var requestStartTime = DateTime.UtcNow;
                var response = await _httpClient.PostAsync($"{config.BaseUrl}/chat/completions", content);
                var requestDuration = DateTime.UtcNow - requestStartTime;
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var totalDuration = DateTime.UtcNow - startTime;

                _logger.LogInformation("[{RequestId}] OpenAI API response received. Status: {StatusCode}, Request Duration: {RequestDuration}ms, Total Duration: {TotalDuration}ms, Response Size: {ResponseSize} bytes", 
                    requestId, response.StatusCode, requestDuration.TotalMilliseconds, totalDuration.TotalMilliseconds, responseContent?.Length ?? 0);

                if (response.IsSuccessStatusCode)
                {
                    // Validate that we received a valid JSON response
                    if (string.IsNullOrWhiteSpace(responseContent))
                    {
                        _logger.LogError("[{RequestId}] OpenAI API returned empty response after {Duration}ms", requestId, totalDuration.TotalMilliseconds);
                        return new ValidationAnalysisResult
                        {
                            Success = false,
                            ErrorMessage = "OpenAI API returned empty response",
                            ModelUsed = config.Model,
                            Provider = ProviderName
                        };
                    }
                    
                    _logger.LogDebug("[{RequestId}] Processing OpenAI response content", requestId);

                    try
                    {
                        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        
                        // Validate response structure
                        if (!jsonResponse.TryGetProperty("choices", out var choices) || 
                            choices.ValueKind != JsonValueKind.Array || 
                            choices.GetArrayLength() == 0)
                        {
                            _logger.LogError("[{RequestId}] OpenAI API response missing 'choices' array. Response structure: {ResponseKeys}", 
                                requestId, string.Join(", ", jsonResponse.EnumerateObject().Select(p => p.Name)));
                            _logger.LogDebug("[{RequestId}] Full response content: {Response}", requestId, responseContent);
                            return new ValidationAnalysisResult
                            {
                                Success = false,
                                ErrorMessage = "Invalid OpenAI API response structure",
                                ModelUsed = config.Model,
                                Provider = ProviderName
                            };
                        }
                        
                        _logger.LogDebug("[{RequestId}] Found {ChoiceCount} choices in OpenAI response", requestId, choices.GetArrayLength());

                        var firstChoice = choices[0];
                        if (!firstChoice.TryGetProperty("message", out var message) ||
                            !message.TryGetProperty("content", out var contentProperty))
                        {
                            _logger.LogError("[{RequestId}] OpenAI API response missing message content. Choice structure: {ChoiceKeys}", 
                                requestId, string.Join(", ", firstChoice.EnumerateObject().Select(p => p.Name)));
                            _logger.LogDebug("[{RequestId}] Full response content: {Response}", requestId, responseContent);
                            return new ValidationAnalysisResult
                            {
                                Success = false,
                                ErrorMessage = "Invalid OpenAI API response: missing message content",
                                ModelUsed = config.Model,
                                Provider = ProviderName
                            };
                        }

                        var analysisText = contentProperty.GetString();
                        
                        _logger.LogDebug("[{RequestId}] Extracted analysis text length: {TextLength} characters", requestId, analysisText?.Length ?? 0);
                        
                        // Validate that the analysis text is not empty
                        if (string.IsNullOrWhiteSpace(analysisText))
                        {
                            _logger.LogError("[{RequestId}] OpenAI API returned empty analysis content", requestId);
                            return new ValidationAnalysisResult
                            {
                                Success = false,
                                ErrorMessage = "OpenAI API returned empty analysis content",
                                ModelUsed = config.Model,
                                Provider = ProviderName
                            };
                        }

                        // Validate that the analysis text is valid JSON
                        try
                        {
                            var jsonValidation = JsonSerializer.Deserialize<JsonElement>(analysisText);
                            _logger.LogInformation("[{RequestId}] Successfully received and validated JSON response from OpenAI. JSON structure: {JsonKeys}", 
                                requestId, string.Join(", ", jsonValidation.EnumerateObject().Select(p => p.Name)));
                        }
                        catch (JsonException jsonEx)
                        {
                            _logger.LogError("[{RequestId}] OpenAI API returned invalid JSON in analysis content. Error: {JsonError}", requestId, jsonEx.Message);
                            _logger.LogDebug("[{RequestId}] Invalid JSON content: {Content}", requestId, analysisText);
                            return new ValidationAnalysisResult
                            {
                                Success = false,
                                ErrorMessage = "OpenAI API returned invalid JSON format",
                                Analysis = analysisText, // Include the raw response for debugging
                                ModelUsed = config.Model,
                                Provider = ProviderName
                            };
                        }

                        _logger.LogInformation("[{RequestId}] OpenAI analysis completed successfully. Total duration: {Duration}ms", 
                            requestId, totalDuration.TotalMilliseconds);
                        
                        return new ValidationAnalysisResult
                        {
                            Success = true,
                            Analysis = analysisText,
                            ModelUsed = config.Model,
                            Provider = ProviderName
                        };
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "[{RequestId}] Failed to parse OpenAI API response as JSON after {Duration}ms. Error: {JsonError}", 
                            requestId, totalDuration.TotalMilliseconds, ex.Message);
                        _logger.LogDebug("[{RequestId}] Unparseable response content: {Response}", requestId, responseContent);
                        return new ValidationAnalysisResult
                        {
                            Success = false,
                            ErrorMessage = $"Failed to parse OpenAI API response: {ex.Message}",
                            ModelUsed = config.Model,
                            Provider = ProviderName
                        };
                    }
                }
                else
                {
                    _logger.LogError("[{RequestId}] OpenAI API error after {Duration}ms. Status: {StatusCode}, Response size: {ResponseSize} bytes", 
                        requestId, totalDuration.TotalMilliseconds, response.StatusCode, responseContent?.Length ?? 0);
                    
                    // Try to parse error details from OpenAI response
                    string errorDetails = "Unknown error";
                    string errorType = "Unknown";
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        if (errorResponse.TryGetProperty("error", out var error))
                        {
                            if (error.TryGetProperty("message", out var errorMessage))
                            {
                                errorDetails = errorMessage.GetString() ?? "Unknown error";
                            }
                            if (error.TryGetProperty("type", out var errorTypeProperty))
                            {
                                errorType = errorTypeProperty.GetString() ?? "Unknown";
                            }
                        }
                        _logger.LogDebug("[{RequestId}] Parsed error details - Type: {ErrorType}, Message: {ErrorMessage}", 
                            requestId, errorType, errorDetails);
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogWarning("[{RequestId}] Could not parse error response: {ParseError}", requestId, parseEx.Message);
                        // If we can't parse the error, use the raw response
                        errorDetails = responseContent;
                    }
                    
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        ErrorMessage = $"OpenAI API error ({response.StatusCode}): {errorDetails}",
                        ModelUsed = config.Model,
                        Provider = ProviderName
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "[{RequestId}] Network error when calling OpenAI API after {Duration}ms. Error: {Message}", 
                    requestId, duration.TotalMilliseconds, ex.Message);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"Network error: {ex.Message}",
                    Provider = ProviderName
                };
            }
            catch (TaskCanceledException ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "[{RequestId}] Timeout when calling OpenAI API after {Duration}ms. Error: {Message}", 
                    requestId, duration.TotalMilliseconds, ex.Message);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = "Request timeout - OpenAI API did not respond in time",
                    Provider = ProviderName
                };
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "[{RequestId}] Unexpected error when calling OpenAI API after {Duration}ms. Error: {Message}", 
                    requestId, duration.TotalMilliseconds, ex.Message);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                    Provider = ProviderName
                };
            }
        }
    }
}
