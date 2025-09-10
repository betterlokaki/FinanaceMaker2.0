namespace FinanceMaker.Common.Models.Algorithms.Analyzers;

/// <summary>
/// Contains the nesseray data for analyzing  
/// </summary>
public class NewsAnalyzed
{
    public static NewsAnalyzed Empty
            => new(string.Empty, DateTime.Now, string.Empty, string.Empty);
    /// <summary>
    /// Article Url  
    /// </summary>
    public string News { get; set; }

    /// <summary>
    /// Article time (this is good to determine whether the news are relevant) 
    /// </summary>
    public DateTime NewsDate { get; set; }

    /// <summary>
    /// The ticker related for this analyzing 
    /// </summary>
    /// <value></value>
    public string Ticker { get; set; }
    /// <summary>
    /// Description for the analysis result (why because the news we get we wannna summerize it and save the data) 
    /// </summary>
    /// <value></value>
    public string Description { get; set; }

    public NewsAnalyzed(string news, DateTime newsDate, string ticker, string description)
    {
        News = news;
        Ticker = ticker;
        NewsDate = newsDate;
        Description = description;
    }
}
