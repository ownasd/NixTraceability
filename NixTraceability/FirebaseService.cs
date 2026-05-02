using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NixTraceability
{
    public class FirebaseService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static string? _cachedConfigUrl;
        private static string? _cachedApiKey;

        /// <summary>
        /// Get Firebase config from settings
        /// </summary>
        private static (string configUrl, string apiKey)? GetFirebaseConfig()
        {
            try
            {
                string configUrl = Database.GetSetting("FirebaseConfigUrl", "");
                string apiKey = Database.GetSetting("FirebaseApiKey", "");

                if (string.IsNullOrEmpty(configUrl) || string.IsNullOrEmpty(apiKey))
                    return null;

                _cachedConfigUrl = configUrl;
                _cachedApiKey = apiKey;
                return (configUrl, apiKey);
            }
            catch (Exception ex)
            {
                Logger.LogError("FirebaseService.GetFirebaseConfig", ex);
                return null;
            }
        }

        /// <summary>
        /// Check if a barcode exists in Firebase for the given part code
        /// </summary>
        public static async Task<bool> CheckBarcodeInFirebaseAsync(string partCode, string barcodeValue)
        {
            try
            {
                var config = GetFirebaseConfig();
                if (!config.HasValue)
                {
                    Logger.LogInfo("FirebaseService", "Firebase not configured - skipping check");
                    return false; // Not configured, skip validation
                }

                // Build Firebase query URL
                // Format: https://{configUrl}/{partCode}.json?orderBy="barcode"&equalTo="{value}"&print=pretty
                string queryUrl = $"{config.Value.configUrl}/{partCode}.json?orderBy=\"barcode\"&equalTo=\"{barcodeValue}\"";

                using (var request = new HttpRequestMessage(HttpMethod.Get, queryUrl))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", $"key={config.Value.apiKey}");
                    
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using (var response = await httpClient.SendAsync(request, cts.Token))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string content = await response.Content.ReadAsStringAsync();
                            
                            // If response is empty or "null", barcode not found
                            if (string.IsNullOrWhiteSpace(content) || content == "null")
                            {
                                Logger.LogInfo("FirebaseService", $"Barcode {barcodeValue} NOT found in Firebase for {partCode}");
                                return false; // Not found in Firebase
                            }

                            // Parse JSON response - if it has data, barcode exists
                            var json = JToken.Parse(content);
                            if (json.HasValues)
                            {
                                Logger.LogInfo("FirebaseService", $"Barcode {barcodeValue} FOUND in Firebase for {partCode}");
                                return true; // Found in Firebase
                            }

                            return false;
                        }
                        else
                        {
                            Logger.LogError("FirebaseService.CheckBarcodeInFirebaseAsync", 
                                new Exception($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"));
                            return false; // On error, skip validation
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("FirebaseService.CheckBarcodeInFirebaseAsync", ex);
                return false; // On exception, skip validation (fail-safe)
            }
        }

        /// <summary>
        /// Synchronous wrapper for CheckBarcodeInFirebaseAsync
        /// </summary>
        public static bool CheckBarcodeInFirebase(string partCode, string barcodeValue)
        {
            try
            {
                return CheckBarcodeInFirebaseAsync(partCode, barcodeValue).Result;
            }
            catch (AggregateException ex) when (ex.InnerException != null)
            {
                Logger.LogError("FirebaseService.CheckBarcodeInFirebase", ex.InnerException);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError("FirebaseService.CheckBarcodeInFirebase", ex);
                return false;
            }
        }

        /// <summary>
        /// Test Firebase connection
        /// </summary>
        public static async Task<bool> TestConnectionAsync()
        {
            try
            {
                var config = GetFirebaseConfig();
                if (!config.HasValue)
                    return false;

                string testUrl = $"{config.Value.configUrl}/.json?shallow=true";
                
                using (var request = new HttpRequestMessage(HttpMethod.Get, testUrl))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", $"key={config.Value.apiKey}");
                    
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using (var response = await httpClient.SendAsync(request, cts.Token))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("FirebaseService.TestConnectionAsync", ex);
                return false;
            }
        }
    }
}
