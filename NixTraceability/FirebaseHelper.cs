using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NixTraceability
{
    public static class FirebaseHelper
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task SyncDataAsync(string macId, object data)
        {
            try
            {
                if (Database.GetSetting("FirebaseSyncEnabled", "0") != "1") return;

                string baseUrl = Database.GetSetting("FirebaseUrl", "").Trim();
                if (string.IsNullOrEmpty(baseUrl) || (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://")))
                {
                    Logger.LogError("FirebaseHelper.SyncData", new Exception("Invalid Firebase URL."));
                    return;
                }

                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                // The data is pushed to: /EndToEndTraceability/{macId}/AssemblyApp.json
                string url = $"{baseUrl}EndToEndTraceability/{macId}/AssemblyApp.json";

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PutAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string resContent = await response.Content.ReadAsStringAsync();
                    Logger.LogError("FirebaseHelper.SyncData", new Exception($"Firebase returned: {response.StatusCode} - {resContent}"));
                }
                else
                {
                    Logger.LogInfo("FirebaseHelper.SyncData", $"Successfully synced MAC ID {macId} to Firebase.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("FirebaseHelper.SyncData", ex);
            }
        }

        /// <summary>
        /// Checks if a PCBA barcode exists in a Nixirtrace Firebase node.
        /// Nixirtrace uses list-based structure (PostAsync) where barcode is stored in "PcbaQR" field.
        /// We query using orderBy="PcbaQR"&equalTo="{barcode}".
        /// Falls back to direct key lookup for backward compatibility with older key-based data.
        /// </summary>
        public static async Task<bool> CheckIfPcbaExistsAsync(string barcode, string customNode = "")
        {
            try
            {
                if (Database.GetSetting("FirebaseValidationEnabled", "0") != "1") return true;

                string baseUrl = Database.GetSetting("FirebaseUrl", "").Trim();
                if (string.IsNullOrEmpty(baseUrl) || (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://")))
                {
                    Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync", "Firebase URL not configured — skipping validation.");
                    return true;
                }

                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                string node = customNode?.Trim() ?? "";
                if (string.IsNullOrEmpty(node))
                {
                    Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync", "Validation node is empty — skipping validation.");
                    return true;
                }

                // Properly encode the Firebase node path (handles spaces, special chars)
                string encodedNode = Uri.EscapeDataString(node);
                string encodedBarcode = Uri.EscapeDataString(barcode);

                // ── APPROACH 1: List-based query (Nixirtrace uses PostAsync with PcbaQR field) ──
                // URL: /{node}.json?orderBy="PcbaQR"&equalTo="{barcode}"
                string queryUrl = $"{baseUrl}{encodedNode}.json?orderBy=%22PcbaQR%22&equalTo=%22{encodedBarcode}%22";
                Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync",
                    $"Trying query-based lookup: barcode='{barcode}', node='{node}'");

                try
                {
                    HttpResponseMessage queryResponse = await client.GetAsync(queryUrl);
                    if (queryResponse.IsSuccessStatusCode)
                    {
                        string queryContent = await queryResponse.Content.ReadAsStringAsync();
                        Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync", $"Query response: {queryContent}");

                        // Firebase returns null or {} when no match found
                        if (!string.IsNullOrEmpty(queryContent) && queryContent != "null")
                        {
                            try
                            {
                                var parsed = JToken.Parse(queryContent);
                                // If it's an object with at least one key, record exists
                                if (parsed.Type == JTokenType.Object && ((JObject)parsed).Count > 0)
                                {
                                    Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync",
                                        $"✅ Found via PcbaQR query: '{barcode}' in '{node}'");
                                    return true;
                                }
                                // If it's an array with items
                                if (parsed.Type == JTokenType.Array && ((JArray)parsed).Count > 0)
                                {
                                    Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync",
                                        $"✅ Found via PcbaQR query (array): '{barcode}' in '{node}'");
                                    return true;
                                }
                            }
                            catch { /* JSON parse failed — try fallback */ }
                        }
                    }
                }
                catch (Exception qex)
                {
                    Logger.LogError("FirebaseHelper.CheckIfPcbaExistsAsync.QueryApproach", qex);
                }

                // ── APPROACH 2: Fallback — Direct key-based lookup (old format) ──
                // URL: /{node}/{barcode}.json
                string directUrl = $"{baseUrl}{encodedNode}/{encodedBarcode}.json";
                Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync",
                    $"Trying direct key lookup: {directUrl}");

                try
                {
                    HttpResponseMessage directResponse = await client.GetAsync(directUrl);
                    if (directResponse.IsSuccessStatusCode)
                    {
                        string directContent = await directResponse.Content.ReadAsStringAsync();
                        Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync", $"Direct response: {directContent}");

                        if (!string.IsNullOrEmpty(directContent) && directContent != "null")
                        {
                            Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync",
                                $"✅ Found via direct key lookup: '{barcode}' in '{node}'");
                            return true;
                        }
                    }
                }
                catch (Exception dex)
                {
                    Logger.LogError("FirebaseHelper.CheckIfPcbaExistsAsync.DirectApproach", dex);
                }

                Logger.LogInfo("FirebaseHelper.CheckIfPcbaExistsAsync",
                    $"❌ NOT FOUND: '{barcode}' in '{node}'");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError("FirebaseHelper.CheckIfPcbaExistsAsync", ex);
                return false;
            }
        }

        /// <summary>
        /// Test method for Settings page — returns detailed result string for debugging.
        /// </summary>
        public static async Task<(bool Found, string Details)> TestPcbaLookupAsync(string barcode, string node)
        {
            try
            {
                string baseUrl = Database.GetSetting("FirebaseUrl", "").Trim();
                if (string.IsNullOrEmpty(baseUrl) || (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://")))
                    return (false, "❌ Firebase URL not configured in settings.");

                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                string encodedNode = Uri.EscapeDataString(node.Trim());
                string encodedBarcode = Uri.EscapeDataString(barcode.Trim());

                // Test query-based lookup
                string queryUrl = $"{baseUrl}{encodedNode}.json?orderBy=%22PcbaQR%22&equalTo=%22{encodedBarcode}%22";
                HttpResponseMessage qRes = await client.GetAsync(queryUrl);
                string qContent = qRes.IsSuccessStatusCode ? await qRes.Content.ReadAsStringAsync() : $"HTTP {qRes.StatusCode}";

                bool queryFound = false;
                if (qRes.IsSuccessStatusCode && !string.IsNullOrEmpty(qContent) && qContent != "null")
                {
                    try
                    {
                        var parsed = JToken.Parse(qContent);
                        if (parsed.Type == JTokenType.Object && ((JObject)parsed).Count > 0) queryFound = true;
                        if (parsed.Type == JTokenType.Array && ((JArray)parsed).Count > 0) queryFound = true;
                    }
                    catch { }
                }

                // Test direct key-based lookup
                string directUrl = $"{baseUrl}{encodedNode}/{encodedBarcode}.json";
                HttpResponseMessage dRes = await client.GetAsync(directUrl);
                string dContent = dRes.IsSuccessStatusCode ? await dRes.Content.ReadAsStringAsync() : $"HTTP {dRes.StatusCode}";
                bool directFound = dRes.IsSuccessStatusCode && !string.IsNullOrEmpty(dContent) && dContent != "null";

                bool found = queryFound || directFound;

                string details = $"Node: {node}\n" +
                                 $"Barcode: {barcode}\n\n" +
                                 $"[Query-based (PcbaQR field)]\n  URL: {queryUrl}\n  Response: {(qContent.Length > 200 ? qContent.Substring(0, 200) + "..." : qContent)}\n  Found: {(queryFound ? "✅ YES" : "❌ NO")}\n\n" +
                                 $"[Direct key lookup]\n  URL: {directUrl}\n  Response: {(dContent.Length > 200 ? dContent.Substring(0, 200) + "..." : dContent)}\n  Found: {(directFound ? "✅ YES" : "❌ NO")}";

                return (found, details);
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }
    }
}
