using System.Net;
using Markdig;

namespace Peeklism.Core.Viewing;

/// <summary>Untrusted documents are inert: no HTML, scripts, network requests or embedded frames.</summary>
public static class DocumentRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().DisableHtml().Build();

    public static string Render(string text, bool markdown, bool dark)
    {
        var body = markdown ? Markdown.ToHtml(text, Pipeline) : "<pre class=source>" + WebUtility.HtmlEncode(text) + "</pre>";
        var colors = dark
            ? "--bg:#202020;--fg:#f3f3f3;--muted:#bdbdbd;--line:#555;--code:#2c2c2c;--link:#8dc8ff;--selection:#245a86;"
            : "--bg:#fafafa;--fg:#202020;--muted:#555;--line:#bbb;--code:#efefef;--link:#005a9e;--selection:#cce6ff;";
        return """
            <!doctype html><html lang="zh-Hant"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src https://assets.peeklism.invalid; style-src 'unsafe-inline'; base-uri https://assets.peeklism.invalid; form-action 'none'">
            <base href="https://assets.peeklism.invalid/">
            <title>Peeklism</title><style>
            """ + ":root{" + colors + "color-scheme:" + (dark ? "dark" : "light") + ";}" + """
            *{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:16px/1.7 'Segoe UI',sans-serif}
            main{max-width:80ch;margin:auto;padding:32px 28px 80px;overflow-wrap:anywhere}
            main>:first-child{margin-top:0}h1,h2,h3,h4,h5,h6{line-height:1.25;margin:1.7em 0 .6em;font-weight:600}h1{font-size:2em}h2{font-size:1.5em}
            p,ul,ol,table,blockquote,pre{margin:0 0 1.2em}a{color:var(--link);text-underline-offset:3px}
            a:focus-visible{outline:2px solid var(--link);outline-offset:3px}::selection{background:var(--selection)}
            pre,code{font-family:'Cascadia Mono',Consolas,monospace;font-size:.9em}code{background:var(--code);padding:2px 4px;border-radius:3px}
            pre{background:var(--code);padding:16px;overflow:auto;border-radius:6px}pre code{padding:0;background:none}
            blockquote{margin-left:0;padding:12px 18px;background:var(--code);color:var(--muted)}blockquote p:last-child{margin:0}
            table{display:block;overflow:auto;border-collapse:collapse}th,td{padding:8px 12px;border:1px solid var(--line);text-align:start}
            img{max-width:100%;height:auto}hr{border:0;border-top:1px solid var(--line);margin:28px 0}
            .source{background:none;padding:0;white-space:pre-wrap;tab-size:4;font-size:14px}main:has(.source){max-width:none}
            input[type=checkbox]{accent-color:var(--link)}@media(max-width:600px){main{padding:20px 16px 48px}}
            @media print{body{background:white;color:black}main{max-width:none;padding:0}pre{white-space:pre-wrap}}
            </style></head><body><main>
            """ + body + "</main></body></html>";
    }
}
