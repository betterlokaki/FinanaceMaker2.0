using System.Text.Json.Serialization;

namespace FinanceMaker.Brokers.InteractiveBrokers.Models;

public enum LogicBind { And, Or, End }
public enum Operator { GreaterOrEqual, LessOrEqual }

public enum ConditionType
{
    Price = 1,
    Time = 3,
    Margin = 4,
    Trade = 5,
    Volume = 6,
    MtaMarket = 7,
    MtaPosition = 8,
    MtaAccountDailyPnl = 9,
}

public enum TimeInForceType { GTC, GTD }

public abstract class TIFAlert
{
    public string Tif { get; }
    public string? ExpireTime { get; }

    protected TIFAlert(TimeInForceType tif, DateTime? expireTime)
    {
        Tif = tif.ToString();
        ExpireTime = expireTime?.ToString("yyyyMMdd-HH:mm:ss");
    }
}

public sealed class GTCAlert : TIFAlert
{
    public GTCAlert() : base(TimeInForceType.GTC, null) { }
}

public sealed class GTDAlert : TIFAlert
{
    public GTDAlert(DateTime expireTime) : base(TimeInForceType.GTD, expireTime) { }
}

public abstract class Condition
{
    public int Type { get; }
    public string? Value { get; }
    public string? Timezone { get; }

    protected Condition(ConditionType type, string? value, string? timezone = null)
    {
        Type = (int)type;
        Value = value ?? "*";
        Timezone = timezone;
    }
}

public sealed class PriceCondition : Condition
{
    public PriceCondition(double? value = null) : base(ConditionType.Price, value?.ToString()) { }
}

public sealed class MarginCondition : Condition
{
    public MarginCondition(double? value = null) : base(ConditionType.Margin, value?.ToString()) { }
}

public sealed class TradeCondition : Condition
{
    public TradeCondition() : base(ConditionType.Trade, null) { }
}

public sealed class AlertCondition
{
    [JsonPropertyName("conidex")] public string Conidex { get; }
    [JsonPropertyName("logicBind")] public string LogicBindCode { get; }
    [JsonPropertyName("operator")] public string OperatorCode { get; }
    [JsonPropertyName("triggerMethod")] public string TriggerMethod { get; } = "0";
    [JsonPropertyName("type")] public int Type { get; }
    [JsonPropertyName("value")] public string? Value { get; }
    [JsonPropertyName("timeZone")] public string? TimeZone { get; }

    public AlertCondition(int contractId, string exchange, LogicBind logicBind, Operator op, Condition condition)
    {
        Conidex = $"{contractId}@{exchange}";
        LogicBindCode = logicBind switch { Models.LogicBind.And => "a", Models.LogicBind.Or => "o", Models.LogicBind.End => "n", _ => "a" };
        OperatorCode = op switch { Models.Operator.GreaterOrEqual => ">=", Models.Operator.LessOrEqual => "<=", _ => ">=" };
        Type = condition.Type;
        Value = condition.Value;
        TimeZone = condition.Timezone;
    }
}

public sealed class Alert
{
    [JsonPropertyName("alertMessage")] public string AlertMessage { get; }
    [JsonPropertyName("alertName")] public string AlertName { get; }
    [JsonPropertyName("expireTime")] public string? ExpireTime { get; }
    [JsonPropertyName("alertRepeatable")] public int AlertRepeatable { get; }
    [JsonPropertyName("outsideRth")] public int OutsideRth { get; }
    [JsonPropertyName("sendMessage")] public int SendMessage { get; }
    [JsonPropertyName("email")] public string Email { get; }
    [JsonPropertyName("iTWSOrdersOnly")] public int ITWSOrdersOnly { get; }
    [JsonPropertyName("showPopup")] public int ShowPopup { get; }
    [JsonPropertyName("tif")] public string Tif { get; }
    [JsonPropertyName("conditions")] public List<AlertCondition> Conditions { get; }

    public Alert(
        string alertName,
        string alertMessage,
        bool alertRepeatable,
        bool outsideRth,
        bool sendMessage,
        string email,
        TIFAlert tifAlert,
        IEnumerable<AlertCondition> conditions,
        bool iTWSOrdersOnly = false,
        bool showPopup = false)
    {
        AlertName = alertName;
        AlertMessage = alertMessage;
        AlertRepeatable = alertRepeatable ? 1 : 0;
        OutsideRth = outsideRth ? 1 : 0;
        SendMessage = sendMessage ? 1 : 0;
        Email = email;
        Tif = tifAlert.Tif;
        ExpireTime = tifAlert.ExpireTime;
        ITWSOrdersOnly = iTWSOrdersOnly ? 1 : 0;
        ShowPopup = showPopup ? 1 : 0;
        Conditions = conditions.ToList();
    }
}


