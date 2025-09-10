using System.Formats.Asn1;
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using EasySharpIni.Converters;

namespace FinanceMaker.Brokers.InteractiveBrokers;

internal sealed class IBKRAuthenticator
{
    private readonly IBKRConfig _config;
    private string? _liveSessionToken;
    private long _liveSessionTokenExpirationUnixMs;
    private readonly HttpClient _httpClient;

    public IBKRAuthenticator(IBKRConfig config, HttpClient httpClient)
    {
        _config = config;
        _config.Validate();
        _httpClient = httpClient;
    }

    public Dictionary<string, string> GetHeaders(string method, string url)
    {
        UpdateLiveSessionTokenIfNeededAsync().GetAwaiter().GetResult();
        return GenerateStandardHeaders(method, url);
    }

    private async Task UpdateLiveSessionTokenIfNeededAsync()
    {
        var nowSec = DateTimeOffset.Now.ToUnixTimeSeconds();
        var needsFetch = _liveSessionToken == null || _liveSessionTokenExpirationUnixMs < (nowSec + _config.UpdateSessionIntervalSeconds) * 1000L;
        if (!needsFetch) return;

        (_liveSessionToken, _liveSessionTokenExpirationUnixMs) = await FetchLiveSessionTokenAsync();
    }

    private Dictionary<string, string> GenerateStandardHeaders(string method, string url)
    {
        var oauthParams = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = _config.ConsumerKey,
            ["oauth_nonce"] = RandomNumberGenerator.GetHexString(16).ToLowerInvariant(),
            ["oauth_signature_method"] = "HMAC-SHA256",
            ["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["oauth_token"] = _config.TokenAccess,
        };

        var paramsString = string.Join("&", oauthParams.Select(kv => $"{kv.Key}={kv.Value}"));
        var baseString = $"{method}&{Uri.EscapeDataString(url)}&{Uri.EscapeDataString(paramsString)}";
        var baseBytes = Encoding.UTF8.GetBytes(baseString);

        if (_liveSessionToken == null) throw new InvalidOperationException("Live session token not available");
        var keyBytes = Convert.FromBase64String(_liveSessionToken);
        using var hmac = new HMACSHA256(keyBytes);
        var sig = hmac.ComputeHash(baseBytes);
        var b64 = Convert.ToBase64String(sig);
        oauthParams["oauth_signature"] = Uri.EscapeDataString(b64);
        oauthParams["realm"] = _config.Realm;

        var header = "OAuth " + string.Join(", ", oauthParams.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = header,
            ["User-Agent"] = _config.UserAgent,
            ["Host"] = "api.ibkr.com",
            ["Accept"] = "*/*",
            ["Accept-Encoding"] = "gzip,deflate",
            ["Connection"] = "keep-alive",
        };
        return headers;
    }

    private async Task<(string token, long expirationMs)> FetchLiveSessionTokenAsync()
    {
        var method = "POST";
        var url = $"{_config.BaseUrl}/oauth/live_session_token";

        // Use a cryptographically-secure random 256-bit value for DH random
        byte[] rnd = RandomNumberGenerator.GetBytes(32);
        BigInteger dh_random = new BigInteger(rnd, isUnsigned: true, isBigEndian: true);

        var dhPath = _config.DiffieHellmanParamPath;
        string pem = File.ReadAllText(dhPath);
        string base64 = pem
            .Replace("-----BEGIN DH PARAMETERS-----", "")
            .Replace("-----END DH PARAMETERS-----", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Trim();
        byte[] dh_der_data = Convert.FromBase64String(base64);

        AsnReader asn1Seq = new AsnReader(dh_der_data, AsnEncodingRules.DER).ReadSequence();
        BigInteger dh_modulus = asn1Seq.ReadInteger();
        BigInteger dh_generator = asn1Seq.ReadInteger();
        // Generate our dh_challenge value
        BigInteger dh_challenge = BigInteger.ModPow(dh_generator, dh_random, dh_modulus);

        var oauthParams = new SortedDictionary<string, string>
        {
            ["diffie_hellman_challenge"] = dh_challenge.ToString("x"),
            ["oauth_consumer_key"] = _config.ConsumerKey,
            ["oauth_token"] = _config.TokenAccess,
            ["oauth_signature_method"] = "RSA-SHA256",
            ["oauth_timestamp"] = DateTimeOffset.Now.ToUnixTimeSeconds().ToString(),
            ["oauth_nonce"] = RandomNumberGenerator.GetHexString(32).ToLowerInvariant(),
        };

        // Decrypt access token secret to get prepend hex
        var prependHex = CryptoUtils.DecryptRsaPkcs1ToHex(_config.DiffieHellmanPrivateEncryptionPath, _config.TokenSecret);

        // Encoding helpers to mimic Python's quote() and quote_plus() behavior
        static string OAuthPercentEncode(string s) => Uri.EscapeDataString(s);
        static string QuotePlusForUrl(string s) => Uri.EscapeDataString(s).Replace("%20", "+");

        var paramsString = string.Join("&", oauthParams.Select(kv => $"{kv.Key}={kv.Value}"));
        var baseString = $"{prependHex}{method}&{QuotePlusForUrl(url)}&{OAuthPercentEncode(paramsString)}";
        var baseBytes = Encoding.UTF8.GetBytes(baseString);

        // Decrypt the token secret using the private encryption key
        RSACryptoServiceProvider bytes_decrypted_secret = new()
        {
            KeySize = 2048
        };

        string reader = File.ReadAllText(_config.DiffieHellmanPrivateEncryptionPath);
        PemFields pem_fields = PemEncoding.Find(reader);
        byte[] der_data = Convert.FromBase64String(reader[pem_fields.Base64Data]);
        bytes_decrypted_secret.ImportPkcs8PrivateKey(der_data, out _);

        byte[] encryptedSecret = Convert.FromBase64String(_config.TokenSecret);
        byte[] raw_prepend = bytes_decrypted_secret.Decrypt(encryptedSecret, RSAEncryptionPadding.Pkcs1);

        // Load the private signature PEM file and import it directly (handles PKCS#1 and PKCS#8 PEM)
        string signaturePem = File.ReadAllText(_config.DiffieHellmanPrivateSignaturePath);
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(signaturePem);

        // Compute SHA256 of the base string
        using var sha256 = SHA256.Create();
        byte[] baseHash = sha256.ComputeHash(baseBytes);

        // Sign the base-string hash using the imported RSA key and PKCS#1 v1.5 padding
        byte[] signatureBytes = rsa.SignHash(baseHash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string oauth_signature = Convert.ToBase64String(signatureBytes);
        Console.WriteLine("DEBUG: Base string (utf8 hex): " + BitConverter.ToString(baseBytes).Replace("-", "").ToLowerInvariant());
        Console.WriteLine("DEBUG: SHA256(base) (hex): " + BitConverter.ToString(baseHash).Replace("-", "").ToLowerInvariant());
        Console.WriteLine("DEBUG: Signature (base64): " + Convert.ToBase64String(signatureBytes));

        // url-encode signature when adding to header (mimic Python's quote_plus)
        oauthParams["oauth_signature"] = QuotePlusForUrl(oauth_signature);
        oauthParams["realm"] = _config.Realm;

        // Create OAuth header with all parameters sorted lexicographically
        var headerAuth = "OAuth " + string.Join(", ", oauthParams.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}=\"{kv.Value}\""));
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        // request.Headers.TryAddWithoutValidation("Authorization", headerAuth);
        // request.Headers.UserAgent.Clear();
        // request.Headers.UserAgent.ParseAdd(_config.UserAgent);
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = headerAuth,
            ["User-Agent"] = _config.UserAgent,
            ["Host"] = "api.ibkr.com",
            ["Accept"] = "*/*",
            ["Accept-Encoding"] = "gzip,deflate",
            ["Connection"] = "keep-alive",
        };
        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        request.Headers.ConnectionClose = false;

        // Debug: Print all headers being sent
        Console.WriteLine($"DEBUG: Request URL: {url}");
        Console.WriteLine($"DEBUG: Request method: {request.Method}");
        foreach (var reqHeader in request.Headers)
        {
            Console.WriteLine($"DEBUG: Header {reqHeader.Key}: {string.Join(", ", reqHeader.Value)}");
        }

        using var response = await _httpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var dhResponse = doc.RootElement.GetProperty("diffie_hellman_response").GetString()!;
        var lstSignature = doc.RootElement.GetProperty("live_session_token_signature").GetString()!;
        var expiration = doc.RootElement.GetProperty("live_session_token_expiration").GetInt64();

        var computedToken = ComputeLiveSessionToken(dhResponse, lstSignature, prependHex, dh_random, dh_modulus, expiration, _config.ConsumerKey);
        return (computedToken, expiration);
    }

    private string ComputeLiveSessionToken(string dhResponseHex, string lstSignature, string prependHex, BigInteger dh_random, BigInteger dh_modulus, long exprired, string consumerKey)
    {
        // Validate that our dh_response value has a leading sign bit, and if it's not there then be sure to add it.
        if (dhResponseHex[0] != 0)
        {
            dhResponseHex = "0" + dhResponseHex;
        }

        // Convert our dh_response hex string to a biginteger. 
        BigInteger B = BigInteger.Parse(dhResponseHex, NumberStyles.HexNumber);

        BigInteger a = dh_random;
        BigInteger p = dh_modulus;

        // K will be used to hash the prepend bytestring (the decrypted access token) to produce the LST.
        BigInteger K = BigInteger.ModPow(B, a, p);
        // Generate hex string representation of integer K. Be sure to strip the leading sign bit.
        string hex_str_k = K.ToString("X").ToLower(); // It must be converted to lowercase values prior to byte conversion.

        // If hex string K has odd number of chars, add a leading 0
        if (hex_str_k.Length % 2 != 0)
        {
            // Set the lead byte to 0 for a positive sign bit.
            hex_str_k = "0" + hex_str_k;
        }

        // Generate hex bytestring from hex string K.
        byte[] hex_bytes_K = Convert.FromHexString(hex_str_k);
        // Create HMAC SHA1 object
        HMACSHA1 bytes_hmac_hash_K = new()
        {
            // Set the HMAC key to our passed intended_key byte array
            Key = hex_bytes_K
        };
        //Generate bytestring from prepend hex str.
        byte[] prepend_bytes = Convert.FromHexString(prependHex);
        // Hash the SHA1 bytes of our key against the msg content.
        byte[] K_hash = bytes_hmac_hash_K.ComputeHash(prepend_bytes);

        // Convert hash to base64 to retrieve the computed live session token.
        string computed_lst = Convert.ToBase64String(K_hash);
        //Generate hex - encoded str HMAC hash of consumer key bytestring.
        // Hash key is base64 - decoded LST bytestring, method is SHA1
        byte[] b64_decode_lst = Convert.FromBase64String(computed_lst);

        // Convert our consumer key str to bytes
        byte[] consumer_bytes = Encoding.UTF8.GetBytes(consumerKey);

        // Hash the SHA1 bytes against our hex bytes of K.
        byte[] hashed_consumer = EasySha1(b64_decode_lst, consumer_bytes);

        // Convert hash to base64 to retrieve the computed live session token.
        string hex_lst_hash = Convert.ToHexString(hashed_consumer).ToLower();


        // If our hex hash of our computed LST matches the LST signature received in response, we are successful.
        if (hex_lst_hash == lstSignature)
        {
            string live_session_token = computed_lst;
            Console.WriteLine("Live session token computation and validation successful.");
            Console.WriteLine($"LST: {live_session_token}; expires: {exprired}\n");
        }
        else
        {
            Console.WriteLine("######## LST MISMATCH! ########");
            Console.WriteLine($"Hexed LST: {hex_lst_hash} | LST Signature: {lstSignature}\n");
        }
        return computed_lst;
    }

    private static byte[] EasySha1(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA1(key);
        return hmac.ComputeHash(data);
    }
}
