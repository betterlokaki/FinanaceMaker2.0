
using HtmlAgilityPack;

namespace FinanceMaker.Common.Extensions
{
    public static class HtmlNodesExtensions 
    {
        public static HtmlNode EmptyNode => new HtmlNode(HtmlNodeType.Document, new HtmlDocument(), 0);
        public static HtmlNodeCollection EmptyCollection => new HtmlNodeCollection(EmptyNode);   
        
        public static HtmlNodeCollection SelectInnerNodes(this HtmlNode node, string xpath)
        {
            var innerXpath = node?.XPath != "/#document" ? node?.XPath : "";

            if (node is null
                || string.IsNullOrEmpty(innerXpath)
                || string.IsNullOrEmpty(xpath)) return EmptyCollection;

            if (innerXpath.Contains('#'))
            {
                innerXpath = $"{innerXpath.Split("#")[0]}";

                if (xpath.StartsWith("//") && innerXpath.EndsWith("/"))
                {
                    innerXpath = innerXpath[..^1];
                }
            }
            var nodes = node.SelectNodes($"{innerXpath}{xpath}");

            if (nodes is null) return EmptyCollection;

            return nodes;
        }

        public static HtmlNode SelectInnerSingleNode(this HtmlNode node, string xpath)
        {
            var innerXpath = node?.XPath != "/#document" ? node?.XPath : "";

            if (node is null
                || string.IsNullOrEmpty(innerXpath)
                || string.IsNullOrEmpty(xpath)) return EmptyNode;

            if (innerXpath.Contains('#'))
            {
                innerXpath = $"{innerXpath.Split("#")[0]}";

                if (xpath.StartsWith("//") && innerXpath.EndsWith("/"))
                {
                    innerXpath = innerXpath[..^1];
                }
            }
            var foundNode = node.SelectSingleNode($"{innerXpath}{xpath}");

            if (foundNode is null) return EmptyNode;

            return foundNode;
        }
    }
}
