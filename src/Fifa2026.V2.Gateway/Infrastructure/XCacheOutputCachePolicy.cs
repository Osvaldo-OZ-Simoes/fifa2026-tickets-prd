using Microsoft.AspNetCore.OutputCaching;

namespace Fifa2026.V2.Gateway.Infrastructure;

/// <summary>
/// AC-6 — Marca <c>X-Cache: HIT</c> quando a resposta é servida do Output Cache.
///
/// O Output Cache do ASP.NET Core não expõe nativamente um header de hit/miss.
/// Esta policy seta <c>X-Cache: HIT</c> em <see cref="ServeFromCacheAsync"/>
/// (estágio em que os headers ainda são graváveis, antes do flush). O valor
/// default <c>MISS</c> é aplicado por <see cref="XCacheMiddleware"/> via
/// <c>Response.OnStarting</c>, evitando escrever em headers já commitados pelo
/// YARP no caminho de MISS. Paridade com <c>cache-lookup</c>/<c>cache-store</c>
/// do APIM (ADE-004 Invariante 3).
/// </summary>
public sealed class XCacheOutputCachePolicy : IOutputCachePolicy
{
    internal const string CacheHitHeader = "X-Cache";

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        // APENAS GET é cacheável (AC-6 — GET de status, keyed pelo correlationId no
        // path). POST NUNCA pode ser cacheado: o Output Cache usa method+path+query como
        // chave (NÃO o corpo), então POST /mcp (JSON-RPC) e POST /llm (proxy do chatbot)
        // — mesma URL, corpos diferentes — retornariam a PRIMEIRA resposta cacheada por
        // 30s, quebrando o protocolo MCP e a conversa do chatbot (F5). O cache de GET
        // mantém a paridade com cache-store do APIM (ADE-004 Inv 3).
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            context.AllowCacheLookup = false;
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        // Story 2.3 — com a validação de JWT ATIVADA (AC-6), as requisições v2 trazem
        // header Authorization. A policy default do Output Cache DESABILITA cache para
        // requisições autenticadas (segurança conservadora). Aqui reabilitamos
        // explicitamente lookup+storage: o GET de status é keyed pelo correlationId no
        // path e seu conteúdo não depende do usuário — cacheável com segurança por 30s
        // (paridade com cache-store do APIM mantida mesmo após F3).
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        // Servido do store: HIT (headers ainda graváveis neste estágio).
        context.HttpContext.Response.Headers[CacheHitHeader] = "HIT";
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
