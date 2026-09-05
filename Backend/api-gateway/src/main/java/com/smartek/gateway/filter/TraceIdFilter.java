package com.smartek.gateway.filter;

import org.springframework.cloud.gateway.filter.GatewayFilterChain;
import org.springframework.cloud.gateway.filter.GlobalFilter;
import org.springframework.core.Ordered;
import org.springframework.http.server.reactive.ServerHttpRequest;
import org.springframework.stereotype.Component;
import org.springframework.web.server.ServerWebExchange;
import reactor.core.publisher.Mono;

import java.util.UUID;

/**
 * Generates a unique trace ID for every request and propagates it downstream
 * via the {@code X-Trace-Id} header. If the client already sends one (e.g. from
 * a previous hop), it is preserved — this makes distributed tracing work across
 * multiple gateways.
 *
 * The .NET services read this header in {@code TraceIdMiddleware} and include it
 * in every structured log line and error response, so one greppable ID spans the
 * entire request path: Angular → Gateway → .NET service.
 */
@Component
public class TraceIdFilter implements GlobalFilter, Ordered {

    public static final String HEADER_NAME = "X-Trace-Id";

    @Override
    public Mono<Void> filter(ServerWebExchange exchange, GatewayFilterChain chain) {
        ServerHttpRequest request = exchange.getRequest();

        // Preserve client-supplied trace ID if present; otherwise generate one.
        String traceId = request.getHeaders().getFirst(HEADER_NAME);
        if (traceId == null || traceId.isBlank()) {
            traceId = UUID.randomUUID().toString();
        }

        // Mutate the request to carry the trace ID downstream.
        final String finalTraceId = traceId;
        ServerHttpRequest mutatedRequest = request.mutate()
                .header(HEADER_NAME, finalTraceId)
                .build();

        return chain.filter(exchange.mutate().request(mutatedRequest).build());
    }

    @Override
    public int getOrder() {
        return -3; // Run before SecurityHeadersFilter (-2) and RateLimitFilter (-1)
    }
}
