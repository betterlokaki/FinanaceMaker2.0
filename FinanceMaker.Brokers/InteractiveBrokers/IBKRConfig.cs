namespace FinanceMaker.Brokers.InteractiveBrokers;

public sealed class IBKRConfig
{
    public required string TokenAccess { get; init; }
    public required string TokenSecret { get; init; }
    public required string ConsumerKey { get; init; }
    public required string DiffieHellmanParamPath { get; init; }
    public required string DiffieHellmanPrivateEncryptionPath { get; init; }
    public required string DiffieHellmanPrivateSignaturePath { get; init; }
    public int UpdateSessionIntervalSeconds { get; init; } = 60 * 5;

    public string Realm => ConsumerKey == "TESTCONS" ? "test_realm" : "limited_poa";
    public string BaseUrl => "https://api.ibkr.com/v1/api";
    public string UserAgent => $"dotnet/{Environment.Version.Major}.{Environment.Version.Minor}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TokenAccess)) throw new ArgumentException("Token access is required");
        if (string.IsNullOrWhiteSpace(TokenSecret)) throw new ArgumentException("Token secret is required");
        if (string.IsNullOrWhiteSpace(ConsumerKey)) throw new ArgumentException("Consumer key is required");
        if (!File.Exists(DiffieHellmanParamPath)) throw new ArgumentException("DH param path must exist");
        if (!File.Exists(DiffieHellmanPrivateEncryptionPath)) throw new ArgumentException("DH private encryption path must exist");
        if (!File.Exists(DiffieHellmanPrivateSignaturePath)) throw new ArgumentException("DH private signature path must exist");
    }
}


