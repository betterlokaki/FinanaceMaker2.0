using FinanceMaker.Brokers.InteractiveBrokers;
using FinanceMaker.Brokers.InteractiveBrokers.Models;

namespace FinanceMaker.Publisher.Orders.Broker;

public static class InteractiveWebAPI
{
    public static async Task RunAsync()
    {
        var consumerKey = Environment.GetEnvironmentVariable("IBKR_CONSUMER_KEY") ?? "SHROZOSHR";
        var tokenAccess = Environment.GetEnvironmentVariable("IBKR_TOKEN") ?? "d583aac6335668f1f6b9";
        var tokenSecret = Environment.GetEnvironmentVariable("IBKR_TOKEN_SECRET") ?? "ni3WiVII+DhvPAav6OHnVGU87x/wAcc6qkp/l5gyVmfDfPnp7umsq8DWxjQ79UHE0TxdlgfeKzF1lHxCFIcRltCtPeQCl9uaHEG3cxkFMiziKmqrUNDnGAtPgvSwAeNJ0xDLCujvPh/HnK37av+7bRRI+3hDvty4C/XX4M/J3dWd8tH7ei/G6x6kiDfmWxGBERHw0DfrhqncUJ2ujWTkRy8UjGLgnH4oJrcPfnyBgMSY/OsFvMN2WbxRD8+TBPTu6DeMsZJc83m9nJC3HiAMzesfXae65iJgM0m366jSavQnrKPWOjp9EeuJZceui2sUsxUR3pVJJHQzK6LcG0hfKA==";

        var dhParamPath = Environment.GetEnvironmentVariable("IBKR_DH_PARAM") ?? "/Users/shaharrozolio/dhparam.pem";
        var dhPrivateEncryption = Environment.GetEnvironmentVariable("IBKR_DH_PRIVATE_ENCRYPTION") ?? "/Users/shaharrozolio/private_encryption.pem";
        var dhPrivateSignature = Environment.GetEnvironmentVariable("IBKR_DH_PRIVATE_SIGNATURE") ?? "/Users/shaharrozolio/private_signature.pem";

        var config = new IBKRConfig
        {
            ConsumerKey = consumerKey,
            TokenAccess = tokenAccess,
            TokenSecret = tokenSecret,
            DiffieHellmanParamPath = dhParamPath,
            DiffieHellmanPrivateEncryptionPath = dhPrivateEncryption,
            DiffieHellmanPrivateSignaturePath = dhPrivateSignature,
        };

        var client = new IBKRHttpClient(config);

        var accounts = await client.PortfolioAccountsAsync();
        Console.WriteLine($"Accounts: {System.Text.Json.JsonSerializer.Serialize(accounts)}");

        // Attempt to read first account id from response dictionary
        if (accounts[0] is not Dictionary<string, object> accountData || !accountData.TryGetValue("id", out var id)) return;
        string? accountId = id.ToString();
        if (!string.IsNullOrEmpty(accountId))
        {
            var positions = await client.GetPositionsAsync(accountId);
            Console.WriteLine($"Positions: {System.Text.Json.JsonSerializer.Serialize(positions)}");
        }
        else
        {
            Console.WriteLine("Could not parse account id from response");
        }
    }
}


